# Contributing to LMU Manager

Thanks for helping out! This project is open source under the [MIT license](LICENSE) - attribution is all we ask.

## Development workflow

1. **Branch from `dev`** - `main` only accepts merges from `dev` (enforced by CI and branch protection).
2. Make your changes on a feature branch: `git checkout -b feat/my-feature dev`
3. Open a **pull request targeting `dev`**.
4. All checks must pass before merge (they are blocking - see below).
5. Releases are cut from `main` by pushing a `v*` tag; a maintainer merges `dev` -> `main` for that.

## Branching model

```
feature branches  ->  dev  ->  main  ->  tag v*  ->  GitHub Release
```

`main` is always releasable. `dev` is the integration branch.

## Blocking checks (PRs into `dev` and `main`)

| Check | What it does |
|-------|--------------|
| `build` | Compiles the app in Release |
| `unit-tests` | Runs the xUnit suite (`LMUManager.Tests`) |
| `integration-tests` | Runs `--selftest` and `--startuptest` against a real build on Windows |
| `codeql` | Static security analysis (security-extended query pack) |
| `gitleaks` | Secret scanning on the whole history |
| `security-gate` | Fails on high/critical CodeQL alerts and high/critical vulnerable NuGet packages |
| `main-source-guard` | PRs into `main` must come from `dev` (only enforced on `main`) |

Testing and scanning run **in parallel**; only `security-gate` waits for the scanners so it can evaluate their findings.

## Local development

```
dotnet build
dotnet test
dotnet run --project LMUManager.csproj            # run the app
dotnet run --project LMUManager.csproj -- --selftest
```

Please add unit tests for new logic. Pure logic (config handling, path matching, Steam VDF parsing) belongs in `LMUManager.Tests`; anything touching real Windows state should be covered by the integration self-tests.

## Commit messages

Keep them short and imperative (`Add tray menu stop action`). No strict convention enforced.

## Reporting issues

Open a GitHub issue with your Windows version and the relevant log/status output. Do **not** paste API tokens or personal paths.
