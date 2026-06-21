# CLAUDE.md

Guidance for working in this repo. The plan of record is [PLAN.md](PLAN.md).

## Project
A scheduled console app that dumps MySQL → compresses → uploads to a Google Drive folder → rotates old backups → notifies via webhook. .NET 8, config via `appsettings.json` + user-secrets, interactive OAuth.

## Development workflow (TDD red-green) — REQUIRED

Follow this for **every** code change, every time:

1. **Run tests first.** Before writing any implementation, run `dotnet test` to confirm the suite is green. Never start on a red baseline you didn't cause.
2. **Red.** Write a minimal failing test that captures the next slice of behavior. Run `dotnet test` and confirm it fails for the expected reason.
3. **Green.** Write the minimal code to make that test pass. Run `dotnet test` and confirm all tests pass.
4. **Refactor.** Clean up while keeping tests green.
5. Keep tests minimal and focused — one behavior per test. Prefer fast, dependency-free unit tests; isolate MySQL/Drive/HTTP behind interfaces and fake them.

## Definition of Done
A change is **not done** until `dotnet test` passes with **0 failures**. This is enforced by a `Stop` hook (see [.claude/settings.json](.claude/settings.json)) that runs the suite and blocks completion while any test fails.

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project BackupMySQLGoogleDrive`

## Layout
- `BackupMySQLGoogleDrive/` — app (Program, Config/, Services/, BackupRunner)
- `BackupMySQLGoogleDrive.Tests/` — xUnit tests, mirroring the app's namespaces
