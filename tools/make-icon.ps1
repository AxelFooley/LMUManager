# Regenerates icon.ico (256/64/48/32/16). Requires Windows PowerShell with System.Drawing.
# Usage: powershell -File tools\make-icon.ps1 [-PreviewPath <png>]
param(
    [string]$OutFile = (Join-Path $PSScriptRoot "..\icon.ico"),
    [string]$PreviewPath = ""
)

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$s)
{
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    $r = [Math]::Max(2, [int]($s * 0.20))
    $d = $r * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($s - $d, 0, $d, $d, 270, 90)
    $path.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
    $path.AddArc(0, $s - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
        [System.Drawing.Color]::FromArgb(255, 13, 27, 46),
        [System.Drawing.Color]::FromArgb(255, 27, 58, 95), 55)
    $g.FillPath($brush, $path)

    # Le Mans speed stripes crossing the bottom-right corner, clipped to the tile
    $g.SetClip($path)
    $g.TranslateTransform($s * 0.75, $s * 0.75)
    $g.RotateTransform(-30)
    $span = $s * 2.2
    $stripeW = $s * 0.085
    $gap = $s * 0.055
    $colors = @(
        [System.Drawing.Color]::FromArgb(210, 43, 109, 212),
        [System.Drawing.Color]::FromArgb(225, 242, 245, 250),
        [System.Drawing.Color]::FromArgb(210, 210, 39, 48)
    )
    for ($i = 0; $i -lt 3; $i++)
    {
        $y = -$stripeW / 2.0 + $i * ($stripeW + $gap)
        $stripeBrush = New-Object System.Drawing.SolidBrush($colors[$i])
        $g.FillRectangle($stripeBrush, -$span / 2.0, $y, $span, $stripeW)
        $stripeBrush.Dispose()
    }
    $g.ResetTransform()
    $g.ResetClip()

    if ($s -ge 48)
    {
        $lmuSize = if ($s -ge 128) { $s * 0.28 } else { $s * 0.32 }
        $font = New-Object System.Drawing.Font('Segoe UI', $lmuSize,
            ([System.Drawing.FontStyle]::Bold -bor [System.Drawing.FontStyle]::Italic),
            [System.Drawing.GraphicsUnit]::Pixel)
        $fmt = New-Object System.Drawing.StringFormat
        $fmt.Alignment = [System.Drawing.StringAlignment]::Center
        $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
        $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 242, 245, 250))

        $lmuRect = New-Object System.Drawing.RectangleF -ArgumentList 0, ($s * 0.10), $s, ($s * 0.52)
        $g.DrawString('LMU', $font, $textBrush, $lmuRect, $fmt)
        $font.Dispose()

        if ($s -ge 64)
        {
            $subSize = $s * 0.105
            $subFont = New-Object System.Drawing.Font('Segoe UI', $subSize,
                [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $subRect = New-Object System.Drawing.RectangleF -ArgumentList 0, ($s * 0.58), $s, ($s * 0.14)
            $g.DrawString('M A N A G E R', $subFont, $textBrush, $subRect, $fmt)
            $subFont.Dispose()
        }

        $fmt.Dispose(); $textBrush.Dispose()
    }

    $brush.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

# 32bpp bottom-up DIB (XOR mask + empty AND mask) - the canonical small-size ICO entry
function ConvertTo-IcoDib([System.Drawing.Bitmap]$bmp)
{
    $s = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $pixels = New-Object byte[] ($stride * $s)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($stride * $s)); $bw.Write([int32]0); $bw.Write([int32]0)
    $bw.Write([uint32]0); $bw.Write([uint32]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $bw.Write($pixels, $y * $stride, $stride) }
    $maskStride = [int][Math]::Ceiling(([Math]::Ceiling($s / 8.0)) / 4.0) * 4
    $maskRow = New-Object byte[] $maskStride
    for ($y = 0; $y -lt $s; $y++) { $bw.Write($maskRow) }
    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose(); $ms.Dispose()
    return ,$bytes
}

$sizes = @(256, 64, 48, 32, 16)
$temp = Join-Path $env:TEMP ("lmu_icon_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null

$blobs = @()
foreach ($size in $sizes)
{
    $bmp = New-IconBitmap $size
    if ($size -ge 256)
    {
        $png = Join-Path $temp "256.png"
        $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = [System.IO.File]::ReadAllBytes($png)
        $isPng = $true
    }
    else
    {
        $bytes = ConvertTo-IcoDib $bmp
        $isPng = $false
    }
    if ($size -eq 256 -and $PreviewPath)
    {
        $bmp.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $bmp.Dispose()
    $blobs += ,[pscustomobject]@{ Size = $size; Bytes = $bytes; Png = $isPng }
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$blobs.Count)

$offset = 6 + 16 * $blobs.Count
foreach ($b in $blobs)
{
    $dim = if ($b.Size -ge 256) { 0 } else { $b.Size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$b.Bytes.Length); $bw.Write([uint32]$offset)
    $offset += $b.Bytes.Length
}
foreach ($b in $blobs) { $bw.Write($b.Bytes) }
$bw.Flush()

[System.IO.File]::WriteAllBytes($OutFile, $ms.ToArray())
$bw.Dispose(); $ms.Dispose()
Remove-Item $temp -Recurse -Force
Write-Output "icon written: $OutFile ($((Get-Item $OutFile).Length) bytes, sizes: $($sizes -join ', '))"
