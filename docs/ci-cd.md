# CI/CD design

This document explains how continuous integration, delivery and security scanning are set up for LMU Manager, and why.

## Pipelines at a glance

```
                        PR opened (target: dev or main)
                                     |
        +--------+--------+----------+-----------+---------+
        |        |        |          |           |         |
      build  unit-tests  |      codeql      gitleaks     |          <- all in parallel
        |        |     integration-   |           |         |
        |        |       tests        |           |         |
        +--------+--------+----------+-----+-----+---------+
                                         |
                                  security-gate     <- waits for codeql + gitleaks
                                         |
                              merge allowed if green
```

## Workflows

| File | Trigger | Purpose |
|------|---------|---------|
| `.github/workflows/ci.yml` | PRs targeting `dev` or `main` | All quality and security gates |
| `.github/workflows/release.yml` | Push of a `v*` tag | Tests, publishes the single-file exe, creates a GitHub release with the asset |
| `.github/dependabot.yml` | Schedule (weekly) | Dependency + Actions version update PRs against `dev` |

## Jobs in `ci.yml`

All jobs run **in parallel** except `security-gate`, which needs the scanners' results.

| Job | Runner | Blocking | Notes |
|-----|--------|----------|-------|
| `build` | windows | yes | `dotnet build -c Release` |
| `unit-tests` | windows | yes | xUnit suite in `LMUManager.Tests` |
| `integration-tests` | windows | yes | Runs `--selftest` and `--startuptest` against a real build; exercises config handling, Steam VDF parsing and real registry startup entries on a clean runner |
| `codeql` | windows | yes (see gate) | `security-extended` query pack, results uploaded as SARIF |
| `gitleaks` | ubuntu | yes | Secret scanning over full history |
| `security-gate` | windows | yes | 1) `dotnet list package --vulnerable --include-transitive` fails on High/Critical; 2) queries code-scanning alerts for the PR merge ref and fails on high/critical |
| `main-source-guard` | ubuntu | yes (on `main` only) | Fails unless the PR source branch is `dev` |

## Severity policy

- **CodeQL:** high and critical alerts block merging (enforced by `security-gate`; additionally a repository ruleset blocks on code-scanning findings at the same threshold).
- **Secrets (Gitleaks):** any detected secret blocks - there is no benign secret.
- **Dependencies:** high/critical vulnerable NuGet packages (including transitive) block merging via `dotnet list package --vulnerable`. Dependabot additionally opens weekly update PRs against `dev`; its dashboard alerts are monitored, but the merge-blocking enforcement lives in CI because the Actions `GITHUB_TOKEN` cannot read the Dependabot alerts API.

## Branch protection

- **`main`**: no direct pushes; PRs only; all checks required; `main-source-guard` ensures the source branch is `dev`.
- **`dev`**: PR checks required (same set minus `main-source-guard`).

GitHub cannot natively restrict the *source* branch of a PR, which is why `main-source-guard` exists as a required check.

## Releases

1. Merge `dev` into `main` (PR).
2. Push a tag: `git tag v1.2.3 && git push origin v1.2.3`.
3. `release.yml` runs the test suite, publishes the self-contained single-file exe and attaches `LMUManager-v1.2.3-win-x64.zip` to an auto-generated GitHub release.
4. Update `CHANGELOG.md` in the same merge that produces the release.

## Local equivalents

```
dotnet test                                   # unit-tests job
LMUManager.exe --selftest                     # part of integration-tests
LMUManager.exe --startuptest                  # part of integration-tests
dotnet list package --vulnerable --include-transitive   # dependency gate
```
