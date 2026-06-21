# Backup MySQL → Google Drive — Implementation Plan

## Goal
A scheduled **console application** that performs **one backup run** and exits, driven entirely by config. Windows Task Scheduler owns the timing.

Pipeline per run:
**dump MySQL → compress → upload to a target Drive folder → rotate old backups → notify via webhook**, with structured logging and a non-zero exit code on failure.

## Decisions (locked)
| Topic | Choice |
|---|---|
| Run mode | Scheduled console app (external scheduler) |
| Config | `appsettings.json` (non-secret) + .NET **user-secrets** (connection string, OAuth client id/secret) |
| Auth | Interactive **OAuth** — one-time browser consent, refresh token reused on scheduled runs |
| Target framework | Upgrade `net5.0` → **`net8.0`** (LTS) |
| Compression | Gzip dump to `.sql.gz` before upload |
| Drive target | Specific folder ID (supports Shared/Team Drives) |
| Rotation | keep-last-N and/or older-than-N-days, config-driven |
| Notifications | Single **webhook** POST (Discord/Slack/generic) on success + failure |

## Testing & Definition of Done
- TDD red-green for every change (run tests → write failing test → make it pass → refactor). See [CLAUDE.md](CLAUDE.md).
- **DoD: `dotnet test` passes with 0 failures.** Enforced by a `Stop` hook ([.claude/settings.json](.claude/settings.json) → [.claude/hooks/enforce-tests.ps1](.claude/hooks/enforce-tests.ps1)) that runs the suite and blocks turn completion while any test fails.
- Test project: `BackupMySQLGoogleDrive.Tests` (xUnit, net8.0), mirrors the app's namespaces. Isolate MySQL/Drive/HTTP behind interfaces and fake them.

## Target structure
```
BackupMySQLGoogleDrive/
├─ Program.cs                 // host setup, DI, config binding, run + exit code
├─ appsettings.json           // non-secret options
├─ Config/
│  └─ BackupOptions.cs        // strongly-typed config
├─ Services/
│  ├─ MySqlDumpService.cs     // dump DB to .sql (MySqlBackup.NET)
│  ├─ CompressionService.cs   // gzip .sql -> .sql.gz
│  ├─ GoogleDriveService.cs   // auth, upload to folder, list/delete
│  ├─ RotationService.cs      // enforce retention on Drive
│  └─ NotificationService.cs  // webhook POST
└─ BackupRunner.cs            // orchestrates steps, logging, exit code
```

---

## Tasks

### Phase 0 — Project hygiene & scaffolding
- [x] **T0.1** Upgrade `BackupMySQLGoogleDrive.csproj` `TargetFramework` `net5.0` → `net8.0`.
- [x] **T0.2** Add NuGet packages: `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Console`. Bump `Google.Apis.Drive.v3` and `MySql.Data` to current versions.
- [x] **T0.3** Add `appsettings.json`, `credentials.json`, `token.json`/`MyAppsToken/` to `.gitignore`. Add `appsettings.example.json` to repo as a template.
- [x] **T0.4** Enable user-secrets on the project (`<UserSecretsId>` in csproj).

### Phase 1 — Config model
- [x] **T1.1** Create `Config/BackupOptions.cs` with sections:
  - `MySql`: `ConnectionString` (secret), `DatabaseName`.
  - `GoogleDrive`: `ClientId` (secret), `ClientSecret` (secret), `FolderId`, `SupportsSharedDrives`, `ApplicationName`.
  - `Backup`: `TempDirectory`, `FileNamePrefix`, `Compress` (bool).
  - `Rotation`: `KeepLastN` (int?), `MaxAgeDays` (int?).
  - `Notifications`: `WebhookUrl`, `NotifyOnSuccess` (bool).
- [x] **T1.2** Create `appsettings.json` + `appsettings.example.json` with all non-secret defaults.
- [x] **T1.3** Bind config in `Program.cs` via Generic Host (`Host.CreateApplicationBuilder`), add user-secrets + env-var providers, register `IOptions<BackupOptions>` with validation (fail fast on missing required values).

### Phase 2 — MySQL dump
- [x] **T2.1** `MySqlDumpService.Dump()` — rework the old commented dump code into a service using `MySqlBackup.NET`.
- [x] **T2.2** Output to `TempDirectory` with timestamped name: `{prefix}_{yyyy-MM-dd_HHmm}.sql`. Return the file path.
- [x] **T2.3** Validate connection + surface a clear error if the DB is unreachable.

### Phase 3 — Compression
- [x] **T3.1** `CompressionService.GzipFile(path)` → `{path}.gz` using `System.IO.Compression.GZipStream`.
- [x] **T3.2** Delete the raw `.sql` after successful compression. Skip entirely if `Backup.Compress = false`.

### Phase 4 — Google Drive upload
- [x] **T4.1** `GoogleDriveService` — move auth out of `Program.cs`; use `DriveService.Scope.DriveFile` only and drop the dead `Scopes` field.
- [x] **T4.2** `Upload(filePath)` — set `body.Parents = [FolderId]`, `SupportsAllDrives = true`. Use **resumable upload** and check `request.GetProgress().Status` for success/failure.
- [x] **T4.3** Add simple retry (e.g. 3 attempts w/ backoff) on transient upload errors.

### Phase 5 — Rotation
- [x] **T5.1** `GoogleDriveService.ListBackups(folderId, prefix)` — query files in folder matching the name pattern, ordered by created time.
- [x] **T5.2** `RotationService.Apply()` — compute deletions from `KeepLastN` and `MaxAgeDays`; call `Files.Delete` for each. Log every deletion. No-op if both policies null.

### Phase 6 — Notifications
- [x] **T6.1** `NotificationService.Send(success, summary)` — POST JSON to `WebhookUrl` via `IHttpClientFactory`. Body shaped for Discord/Slack (`content`/`text`).
- [x] **T6.2** Always notify on failure; notify on success only if `NotifyOnSuccess = true`. Webhook failure must not crash the run (log + continue).

### Phase 7 — Orchestration & robustness
- [x] **T7.1** `BackupRunner.RunAsync()` — sequence: dump → compress → upload → rotate → notify. Structured `ILogger` at each step.
- [x] **T7.2** `try/finally` to delete local temp files (`.sql`/`.sql.gz`) regardless of outcome.
- [x] **T7.3** Return process exit code: `0` success, non-zero on failure (Task Scheduler alarming). Remove the old `Console.Read()` pause.
- [x] **T7.4** Catch-all at top level → send failure webhook + log full exception.

### Phase 8 — Docs & scheduling
- [x] **T8.1** `README.md`: setup (OAuth client creation, first-run consent), config reference, user-secrets commands.
- [x] **T8.2** Document Windows Task Scheduler setup (daily trigger, run-whether-logged-in, working directory) — optionally include a `.xml` task template or `schtasks` command.

---

## Build order / dependencies
```
T0 ─► T1 ─► T2 ─► T3 ─► T4 ─► T5 ─► T6 ─► T7 ─► T8
                   (T3 optional/skippable)
```
Recommended first PR: **Phase 0 + Phase 1** (upgrade + config scaffolding) — everything else depends on it.

## Open / deferred
- Service-account auth (zero-touch on fresh machines) — deferred; staying with OAuth.
- SMTP email notifications — deferred in favor of webhook.
- Encryption at rest of the dump before upload — not in scope unless requested.
