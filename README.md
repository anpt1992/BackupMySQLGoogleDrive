# Backup MySQL → Google Drive

A scheduled **console app** that performs **one backup run** and exits:

> dump MySQL → compress (gzip) → upload to a Google Drive folder → rotate old backups → notify via webhook

Timing is owned by an external scheduler (Windows Task Scheduler). The run returns exit code `0`
on success and a non-zero code on failure so the scheduler can alarm.

- **Framework:** .NET 8
- **Config:** `appsettings.json` (non-secret) + .NET user-secrets / environment variables (secrets)
- **Auth:** interactive Google OAuth — one-time browser consent; the refresh token is reused on later runs

See [PLAN.md](PLAN.md) for the implementation plan and [CLAUDE.md](CLAUDE.md) for the dev workflow.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A reachable MySQL server and a Drive folder you can write to
- A Google Cloud OAuth **Desktop app** client (see below)

## 1. Create a Google OAuth client

1. Open the [Google Cloud Console](https://console.cloud.google.com/) → create or select a project.
2. **APIs & Services → Library →** enable the **Google Drive API**.
3. **APIs & Services → OAuth consent screen →** configure it (External is fine for personal use) and
   add your Google account under **Test users**.
4. **APIs & Services → Credentials → Create credentials → OAuth client ID →** application type
   **Desktop app**. Copy the **Client ID** and **Client secret**.

### Find the Drive folder ID

Open the target folder in Drive — the ID is the last path segment of the URL:

```
https://drive.google.com/drive/folders/<THIS_IS_THE_FOLDER_ID>
```

Shared/Team Drives are supported (`GoogleDrive:SupportsSharedDrives = true`, the default).

## 2. Configure

Non-secret settings live in [`appsettings.json`](BackupMySQLGoogleDrive/appsettings.json); a template is in
[`appsettings.example.json`](BackupMySQLGoogleDrive/appsettings.example.json).

| Section | Key | Notes |
|---|---|---|
| `MySql` | `ConnectionString` | **secret** — e.g. `server=localhost;user=root;pwd=...;database=phr;charset=utf8;` |
| | `DatabaseName` | logical name used in logs / messages |
| `GoogleDrive` | `ClientId` / `ClientSecret` | **secret** — from the OAuth client above |
| | `FolderId` | target Drive folder |
| | `SupportsSharedDrives` | `true` to allow Shared/Team Drives (default) |
| | `ApplicationName` | shown to the Drive API |
| `Backup` | `TempDirectory` | where the dump is written before upload |
| | `FileNamePrefix` | backup file name prefix (also the rotation match key) |
| | `Compress` | `true` → upload `.sql.gz`; `false` → upload raw `.sql` |
| `Rotation` | `KeepLastN` | keep only the newest N backups (`null` = no limit) |
| | `MaxAgeDays` | delete backups older than N days (`null` = no limit) |
| `Notifications` | `WebhookUrl` | Discord/Slack/generic webhook (payload sends both `content` and `text`) |
| | `NotifyOnSuccess` | `false` → only notify on failure |

Rotation runs only if at least one policy is set; when both are set, their deletions are unioned.

### Store secrets

Do **not** put secrets in `appsettings.json`. Use user-secrets (per-user, outside the repo):

```bash
cd BackupMySQLGoogleDrive
dotnet user-secrets set "MySql:ConnectionString" "server=localhost;user=root;pwd=...;database=phr;"
dotnet user-secrets set "GoogleDrive:ClientId"     "<YOUR_CLIENT_ID>"
dotnet user-secrets set "GoogleDrive:ClientSecret" "<YOUR_CLIENT_SECRET>"
```

Environment variables also work (double-underscore separator), which is handy for scheduled runs:

```
MySql__ConnectionString=...
GoogleDrive__ClientId=...
GoogleDrive__ClientSecret=...
```

## 3. First run (interactive consent)

```bash
dotnet run --project BackupMySQLGoogleDrive
```

The first run opens a browser for Google consent. The resulting refresh token is cached in the
`MyAppsToken/` data store next to the executable and reused on every later run — so scheduled runs
need no interaction. (Both `MyAppsToken/` and `appsettings.json` are git-ignored.)

## 4. Schedule it (Windows Task Scheduler)

Run **once interactively first** so the OAuth token is cached, then create a daily task.

Publish a self-contained build (optional but simplest for scheduling):

```bash
dotnet publish BackupMySQLGoogleDrive -c Release -o C:\Apps\BackupMySQLGoogleDrive
```

Create a daily 02:00 task that runs whether or not you are logged in:

```bat
schtasks /Create ^
  /TN "MySQL Drive Backup" ^
  /TR "C:\Apps\BackupMySQLGoogleDrive\BackupMySQLGoogleDrive.exe" ^
  /SC DAILY /ST 02:00 ^
  /RL HIGHEST /RU "%USERNAME%" /RP * ^
  /F
```

Notes:
- **Working directory** must be the app folder so `appsettings.json` and `MyAppsToken/` resolve.
  In Task Scheduler GUI: *Actions → Edit → Start in*. (`schtasks` has no flag for this; set it via the
  GUI or an exported task `.xml`.)
- "Run whether user is logged on or not" requires the password (`/RP`).
- The task's **Last Run Result** reflects the process exit code — non-zero means the run failed; check
  the console log and/or the failure webhook.

## Commands

```bash
dotnet build                              # build
dotnet test                               # run the test suite (Definition of Done: 0 failures)
dotnet run --project BackupMySQLGoogleDrive   # one backup run
```
