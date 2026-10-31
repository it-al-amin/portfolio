# Database and upload backup & recovery

The site stores everything in one SQLite file (`portfolio.db`) plus an uploads folder (`uploads/documents`, `uploads/media`).

## Why data disappeared on Render

The service runs on Render's **Free** plan. Free web services have an ephemeral filesystem and **cannot use persistent disks** (disks need a paid instance type). Any local file change is lost when the service is redeployed, restarted, or **spun down after 15 minutes without traffic**. Because `/app/App_Data` was recreated empty each time, the app ran its migrations and seeder again, which brought back the original CV content and dropped every edit, message and upload.

Blueprint settings cannot add a disk to a Free service. The options are: keep Free and back up off-site (implemented here), or move to a paid instance and attach a disk (see the end of this page).

## How it works

| When | What happens |
|---|---|
| Startup, local `portfolio.db` **exists** | It is used as-is and **never overwritten**. If it fails `PRAGMA integrity_check` the app refuses to start and leaves the file alone. |
| Startup, local database **missing** | The newest snapshot is downloaded to a temp file, checked (`integrity_check`, `foreign_key_check`, EF migration history), then moved into place. If it fails the checks, older snapshots are tried. Migrations then run as usual, and the seeder skips because a profile exists. |
| Startup, storage **unreachable** | The app **refuses to start** instead of creating an empty database that would later be backed up as the newest copy. During a deploy Render keeps the previous instance serving; after a restart or spin-down the site stays down until storage is reachable again. |
| Startup, storage **empty** | First run only: a new database is created, seeded, and backed up after the first change. |
| Any change saved | About 10 seconds after the last change, a consistent snapshot is taken with SQLite's online backup API, verified, and uploaded as `db/portfolio-<UTC timestamp>.db`. Existing snapshots are never overwritten. Failed backups retry every 60 seconds. A final backup is attempted on shutdown. |
| Retention | The newest 30 snapshots are kept, plus the newest snapshot of each of the last 30 days. Older ones are deleted. |
| File upload | The file is copied to `uploads/<category>/<name>` in the bucket **before** the upload succeeds. If that copy fails, the upload is rejected. Missing files are downloaded on startup. |
| File deleted in admin | Only the local copy is removed. The bucket copy is kept so older snapshots still have their files (see "Purging deleted files"). |

The admin dashboard shows the last backup and has **Back up now** and **Verify** buttons. Verify checks the live database and the newest snapshot without changing anything.

### Limits on Free

- Changes saved in the ~10 seconds before a crash or forced kill are lost. Deploys and restarts send SIGTERM, which triggers a final backup. Spin-down only happens after 15 idle minutes, by which time every change has already been backed up.
- During a deploy the old and new instances overlap briefly. Edits made in that window on the old instance can be lost, so avoid editing content while a deploy is running.
- Run a single instance only (Free always does).
- Startup takes a few seconds longer while the restore runs.

## 1. Create the object storage

Any S3-compatible service works. **Cloudflare R2** has a 10 GB free tier and no egress fees. **Backblaze B2** has a 10 GB free tier.

**Cloudflare R2**
1. R2 → Create bucket, for example `portfolio-backups`. Keep it **private** and don't connect a public domain.
2. R2 → Manage API tokens → Create token → **Object Read & Write**, scoped to that bucket only.
3. Note the Access Key ID, the Secret Access Key, and the S3 endpoint `https://<account-id>.r2.cloudflarestorage.com`.

**Backblaze B2**
1. Create a **private** bucket.
2. Create an application key restricted to that bucket with read and write access.
3. The endpoint looks like `https://s3.us-west-004.backblazeb2.com`, and the region is the middle part (`us-west-004`).

Optional: turn on bucket versioning or object lock for extra protection against accidental deletion.

## 2. Environment variables

| Variable | Value | Notes |
|---|---|---|
| `Backup__Enabled` | `true` | In render.yaml |
| `Backup__Provider` | `S3` | In render.yaml |
| `Backup__ServiceUrl` | `https://<account-id>.r2.cloudflarestorage.com` | Secret. Leave empty for AWS S3 |
| `Backup__Region` | `auto` (R2), `us-west-004` (B2), or the AWS region | In render.yaml. For B2 or AWS, edit it there |
| `Backup__Bucket` | `portfolio-backups` | Secret |
| `Backup__AccessKeyId` | key ID | Secret |
| `Backup__SecretAccessKey` | secret key | Secret |
| `Backup__Prefix` | `portfolio/` | Folder inside the bucket |
| `Database__Path` | `/app/App_Data/portfolio.db` | Configurable SQLite path |
| `Storage__RootPath` | `/app/App_Data/uploads` | Uploads folder |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | your admin login | Only used when no admin exists yet, for example the first run |

Optional: `Backup__DebounceSeconds` (10), `Backup__RetainCount` (30), `Backup__RetainDays` (30), `Backup__RequireRestoreOnStartup` (true; don't change it), and `Backup__RestoreObjectKey` (see recovery).

If `Backup__Enabled=true` but the bucket or keys are missing, the app fails at startup with a message that names the missing variable.

## 3. Deploy steps

1. Create the bucket and keys (step 1).
2. In Render: open the **portfolio** service → **Environment** → add `Backup__ServiceUrl`, `Backup__Bucket`, `Backup__AccessKeyId` and `Backup__SecretAccessKey`. Render only prompts for `sync: false` values when a Blueprint is first created.
3. Commit and push the code and `render.yaml`. Render syncs the Blueprint and deploys.
4. Watch the logs for `Backup storage: s3://...`, then for `No database backup found ... Starting with a new database` on the very first run, or `Restored database from db/portfolio-...` after that.
5. Sign in to /admin, make a small edit, and wait about 15 seconds. The dashboard should show **Last backup**, and the bucket should contain `portfolio/db/portfolio-*.db`.
6. Test it: Render → **Manual Deploy → Restart service**. After it restarts, your edit should still be there and the logs should show `Restored database from ...`.

> The content that exists on Render right now has never been backed up, and it is lost the next time the service restarts. If you edited anything there that you want to keep, copy it out (or re-enter it) after step 5.

## 4. Local development

Nothing changes: with `Backup:Enabled` false the app uses `backend/Portfolio.Api/App_Data/portfolio.db` as before. To try backups locally without a cloud account:

```powershell
$env:Backup__Enabled="true"; $env:Backup__Provider="FileSystem"; $env:Backup__LocalPath="App_Data/backups"
dotnet run --project backend/Portfolio.Api
```

To use a different database file: `$env:Database__Path="C:\data\portfolio.db"`.

## 5. Manual recovery procedures

**List backups:** use the admin dashboard's Backups panel or `GET /api/admin/backup`, or in the bucket browse `portfolio/db/`. Names are UTC timestamps such as `portfolio-20261031T101500123Z.db`.

**Verify a backup:** select **Verify** on the dashboard, or `POST /api/admin/backup/verify?key=db/portfolio-....db`. To check offline, download the file and run:
```
sqlite3 portfolio-....db "PRAGMA integrity_check;" "PRAGMA foreign_key_check;" "SELECT MigrationId FROM __EFMigrationsHistory;"
```
The output should be `ok`, then no rows, then the migration list.

**Restore an older snapshot on Render** (for example, after a bad edit):
1. Optionally select **Back up now** first, so the current state is also kept.
2. Set `Backup__RestoreObjectKey=db/portfolio-<timestamp>Z.db` in the Environment tab, which restarts the service.
3. The logs show `Restored database from <key>`. About 10 seconds later the restored copy is saved again as the newest snapshot (`Database backed up to ...`).
4. **Remove `Backup__RestoreObjectKey`**. If you leave it set, every fresh start restores that same snapshot again.

Nothing is deleted during a restore. Newer snapshots stay in the bucket until normal retention prunes them.

**Restore locally / move data to your PC:**
1. Stop the app.
2. Rename the existing `backend/Portfolio.Api/App_Data/portfolio.db` (and any `-wal`/`-shm` files) rather than deleting them.
3. Copy the downloaded snapshot to `App_Data/portfolio.db`, and the bucket's `uploads/` folder to `App_Data/uploads/`.
4. Start the app.

**Seed a new or empty bucket from your local database:** with the app stopped, either upload a copy of `portfolio.db` to `portfolio/db/portfolio-<yyyyMMdd>T<HHmmssfff>Z.db` (UTC, for example `portfolio-20261031T120000000Z.db`) and the `uploads/` folders to `portfolio/uploads/`, or run locally with the S3 variables set and select **Back up now**. Note that uploads saved before backups were enabled are not mirrored automatically, so copy `App_Data/uploads/` across yourself.

**Storage outage:** the running site keeps working and backups retry every minute. A deploy during the outage fails safely and the old instance keeps serving. A restart or spin-up fails safely too, but the site stays down until storage is back. If you need to start anyway and accept an empty database, set `Backup__RequireRestoreOnStartup=false` temporarily. Turn it back on as soon as storage is reachable, and restore with `Backup__RestoreObjectKey`, because the empty database will have become the newest snapshot.

**Damaged local database (local or paid-disk hosting):** the app will not start and does not touch the file. Move it aside; the next start then restores the newest valid snapshot.

**Purging deleted files:** files deleted in the admin stay in `portfolio/uploads/` so older snapshots remain complete. To remove them permanently (for example an old CV), delete the object in the bucket. Restoring a snapshot that still references it will then return 404 for that file.

## Alternative: paid instance with a disk

On a paid instance type you can attach a disk mounted at, for example, `/var/data`, and set `Database__Path=/var/data/portfolio.db` and `Storage__RootPath=/var/data/uploads`. Keep the off-site backups enabled as well, since a disk alone doesn't protect against bad edits or corruption.
