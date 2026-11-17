# Penguin Plank: deployment, backup, and restore runbook (Phase A)

Scope: one private Windows host running the API (Kestrel, HTTPS) and SQL Server 2025 Express.
Requirements: A6 (8.1, 8.2, 8.3, 8.5, 8.6, 8.7). Scripts live in `deploy/`; nothing here contains a secret.
Every `<PLACEHOLDER>` is supplied by the operator at run time.

## 1. Recovery targets

| Target | Value | How it is met |
|---|---|---|
| RPO (data loss) | 24 hours or less | One full database + media backup every night (02:00 local). A failed run is alerted (section 7), so a missed night is noticed and re-run the same day. |
| RTO (time to recover) | 4 hours or less | Restore needs one script run per set (`Restore-PenguinPlank.ps1`), a runtime-login grant, and a smoke check. Rehearse it (section 6) and record the measured time. |

The off-host copy must always hold the latest set. Treat a failed off-host copy as a failed backup.

## 2. Deployment baseline

1. **HTTPS only, private host.** Copy `deploy/config/appsettings.Production.template.json` to the host as `appsettings.Production.json` and fill in the placeholders. Install the TLS certificate in `LocalMachine\My`; Kestrel reads it by subject, so the private key never sits in a file or in git. Restrict `AllowedHosts` to the private DNS name. Production enables HSTS and HTTP-to-HTTPS redirection.
2. **Secure cookies.** The staff session cookie is `Secure`, `HttpOnly`, `SameSite=Strict` in all environments (`AuthServiceCollectionExtensions`). Over plain HTTP the browser will not return it, so login fails by design.
3. **Secrets as environment variables** on the service account (never in files or git): `ConnectionStrings__PenguinPlankDatabase` (runtime login), `OwnerBootstrap__*` as defined by the owner-bootstrap options, and any connector or webhook secrets.
4. **Media folder** (`FileStore__RootPath`): a dedicated folder outside the web root. Grant the API service account modify rights and nobody else. Back up this folder with the database (section 4).
5. **Reverse proxy or firewall.** Allow inbound 443 only from the private network or VPN. If a proxy terminates TLS, configure forwarded headers so rate limiting sees real client addresses.
6. **Run the API** as a Windows service under a dedicated non-admin account.

### Database accounts (least privilege)

Create the empty database first, then run the SQL scripts with `sqlcmd` as a sysadmin. The scripts read names and passwords from environment variables (`sqlcmd` resolves `$(VAR)` from the environment), so no secret appears on a command line or in git.

| Account | Script | Rights | Used by |
|---|---|---|---|
| Migration login | `deploy/sql/01-create-migration-principal.sql` | `db_ddladmin`, `db_datareader`, `db_datawriter` (not `db_owner`) | `deploy/Apply-Migrations.ps1` only |
| Runtime login | `deploy/sql/02-create-runtime-principal.sql` | `db_datareader`, `db_datawriter`; DENY on DDL, user/role management, and backup | The API at run time |
| Backup account (Windows) | `deploy/sql/03-create-backup-principal.sql` | `db_backupoperator`, `VIEW ANY DEFINITION` | Scheduled tasks |

Order for a new deployment:

1. Create the empty database. Run `01`, then `Apply-Migrations.ps1` (migrations are an explicit step; the API never migrates on startup). Use `-ScriptOnly` first and review the generated SQL.
2. Run `02` after the migrations, then start the API with the runtime connection string.
3. Run `03` for the backup account.
4. Verify the output of `02` lists only `db_datareader` and `db_datawriter`. If a migration ever fails for lack of permission, grant the specific permission to the migration login. Do not add `db_owner`.

## 3. Rate limiting

The API throttles per client with fixed windows. Over-limit requests receive `429` as ProblemDetails with a correlation id.

| Policy | Applied to | Key | Default |
|---|---|---|---|
| `login` | `POST /api/v1/auth/login`, owner bootstrap | Remote address | 10 per 60 s |
| `costly` | Media upload, audit query | Authenticated user, else address | 30 per 60 s |

Tune under the `RateLimiting` configuration section. A request with no remote address (only possible with the in-process test server) is not throttled; Kestrel always supplies one. Account lockout (5 failures, 15 minutes) is configured separately in Identity.

## 4. Backup

**What is backed up.** Each nightly run produces one set `ppbackup-<UTC stamp>` containing `database.bak` (full backup with checksum, verified with `RESTORE VERIFYONLY`), `media\` (copy of the file store), and `manifest.json` (hash of the backup, media file count). Database and media are one unit: restoring one without the other is not supported.

**Consistency.** The database is backed up first, the media second. Media files are write-once with randomized keys, so every media row in the backup has its file. A file uploaded between the two steps shows up only as an orphan; the media orphan/missing-media detection (task 17.1) reports it after a restore.

**Off-host protected copy.** The finished set is copied to `PP_OFFHOST_ROOT` and verified by hash and file count. The destination must be on another host or storage account, readable only by the backup account and the owner, and encrypted at rest (BitLocker volume, encrypted share, or a cloud bucket with server-side encryption and versioning). The run fails if the copy cannot be verified.

**Retention.** 14 days locally and off-host (`-RetentionDays`). The newest set is never pruned, and only folders named `ppbackup-<stamp>` are touched.

**Scheduling (no SQL Server Agent).** Set these machine environment variables, then register the tasks once from an elevated prompt:

| Variable | Meaning |
|---|---|
| `PP_SQL_INSTANCE` | for example `.\SQLEXPRESS` |
| `PP_SQL_DATABASE` | application database name |
| `PP_MEDIA_ROOT` | same value as `FileStore__RootPath` |
| `PP_BACKUP_ROOT` | local staging folder; the SQL Server service account needs write access |
| `PP_OFFHOST_ROOT` | off-host protected destination |

```powershell
.\deploy\backup\Register-BackupTask.ps1 -RunAsUser '<DOMAIN\ppbackup$>'
```

This creates `PenguinPlank Nightly Backup` (02:00) and `PenguinPlank Capacity Check` (06:00). Prefer a group managed service account so no password is stored; otherwise pass `-Credential`. Run the first backup manually against a non-production instance and confirm the set and the off-host copy before relying on the schedule.

## 5. Capacity and health monitoring

`deploy/backup/Test-DatabaseCapacity.ps1` runs daily and compares database data-file size with the 50 GB Express limit:

| Level | Threshold | Exit code | Action |
|---|---|---|---|
| Ok | below 70% (35 GB) | 0 | None |
| Warning | 70% or more | 1 | Plan archiving or an upgrade to a licensed edition |
| Critical | 85% (42.5 GB) or more | 2 | Act now; writes fail at 100% |

It also warns when the backup volume has less than 15% free space. Warnings and errors are written to the Application event log (source `PenguinPlankOps`, event IDs 4000 backup and 4100 capacity). In Task Scheduler a non-zero last result marks the task as failed.

Review weekly: Task Scheduler last-run results for both tasks, free space on the data, media, and backup volumes, API health, and the event log for source `PenguinPlankOps`. Alert on any backup error event or missing nightly run.

## 6. Restore

Restore to a new database name and an empty media folder. Never restore over production without a deliberate decision.

1. Pick a set from the off-host location (or local staging).
2. Restore:
   ```powershell
   .\deploy\backup\Restore-PenguinPlank.ps1 -BackupSetPath '<path\ppbackup-stamp>' -TargetDatabase '<NewDbName>' -TargetMediaRoot '<EmptyFolder>'
   ```
   The script checks the manifest hash and media count, refuses to overwrite an existing database or a non-empty media folder unless `-Force` is passed, restores the database, and copies the media.
3. Run `deploy/sql/02-create-runtime-principal.sql` against the restored database to re-map the runtime login.
4. Point the API at the restored database (`ConnectionStrings__PenguinPlankDatabase`) and media folder (`FileStore__RootPath`), then start it.
5. Apply any migrations newer than the backup with `Apply-Migrations.ps1`.
6. **Smoke check:** sign in as the Owner, read a product list, open a media item (download succeeds), make a harmless edit (the write succeeds), and run the media orphan/missing-media detection. A missing-media finding is a failed restore; orphans are expected only for uploads made after the database backup.
7. Record the elapsed time against the 4-hour RTO and the age of the set against the 24-hour RPO.

Rehearse quarterly on a non-production instance and after any change to these scripts. The automated restore-to-fresh-environment smoke test is task 17.4.

## 7. Run locally (development)

The API hosts the Blazor WebAssembly client (`PenguinPlank.Web`) from the same origin, so one process serves both. Client routes fall back to `index.html`; unknown `/api/...` paths still return 404.

1. **Database.** `appsettings.Development.json` points `ConnectionStrings:PenguinPlankDatabase` at LocalDB (`(localdb)\MSSQLLocalDB`, database `PenguinPlank_Dev`, integrated security, no secret). To use SQL Express instead, override it for your shell only:
   ```powershell
   $env:ConnectionStrings__PenguinPlankDatabase = 'Server=.\SQLEXPRESS;Database=PenguinPlank_Dev;Integrated Security=True;TrustServerCertificate=True'
   ```
2. **Apply migrations** (the API never migrates on startup). Pass the same connection string you use above:
   ```powershell
   dotnet ef database update --project src\PenguinPlank.Infrastructure --startup-project src\PenguinPlank.Infrastructure --connection 'Server=(localdb)\MSSQLLocalDB;Database=PenguinPlank_Dev;Integrated Security=True;TrustServerCertificate=True'
   ```
3. **Owner bootstrap settings.** Set these in the shell that starts the API (never commit them). The password must satisfy the Identity policy (12+ characters, upper, lower, digit, symbol):
   ```powershell
   $env:OwnerBootstrap__Email = 'owner@example.test'
   $env:OwnerBootstrap__InitialPassword = '<LOCAL_DEV_PASSWORD>'
   ```
4. **Start the API** with the HTTPS profile (the session cookie is `Secure`, so plain HTTP login fails by design):
   ```powershell
   dotnet run --project src\PenguinPlank.Api --launch-profile https
   ```
   Browse to `https://localhost:7086/`. Run `dotnet dev-certs https --trust` once if the browser rejects the certificate.
5. **Run the one-time Owner bootstrap** from a second shell. The endpoint is anonymous but still requires a CSRF token, so keep one web session for both calls. It is idempotent; repeating it is a no-op.
   ```powershell
   $csrf = Invoke-RestMethod https://localhost:7086/api/v1/auth/csrf -SessionVariable pp
   Invoke-RestMethod https://localhost:7086/api/v1/owner-bootstrap -Method Post -WebSession $pp -Headers @{ 'X-CSRF-TOKEN' = $csrf.requestToken }
   ```
   Then sign in through the client with the Owner email and password.

## 8. Failure handling

| Symptom | Likely cause | Response |
|---|---|---|
| Backup task failed (event 4000, error) | SQL permission, disk full, off-host share unreachable | Fix the cause and re-run the task the same day to stay within the RPO |
| Off-host verification failed | Network or permission problem; partial copy | Delete the partial off-host set folder, fix, re-run |
| Restore: hash mismatch | Corrupt or altered set | Use the previous set; investigate the storage |
| Capacity Critical | Database near 50 GB | Archive or purge old data, or move to a licensed edition |
| Many `429` responses | Attack or misconfigured client | Check source addresses; tune `RateLimiting` only if the traffic is legitimate |
