# 06b — Deploying the UI and the API separately

The other shape of deployment. [06 — Build & Deployment](06-deployment.md) installs one Windows
service that serves both halves; this one puts the **UI on Cloudflare Pages** and the **API on IIS**
on your own machine, published through a Cloudflare tunnel.

| | |
|---|---|
| UI | `https://cops.sivayaantechnologies.com` — Cloudflare Pages, static files |
| API | `https://copsapi.sivayaantechnologies.com` — IIS on your server, via a Cloudflare tunnel |
| API locally | `http://localhost:6061` |
| Database | SQL Server, database **SCOPS**, login `SivayaanHMS` |

Two hosts means two things must agree, and if either is wrong the site loads and every request
fails:

1. the **UI is built** knowing the API's absolute address, because on Cloudflare there is no `/api`
   to be relative to;
2. the **API lists** the UI's origin in `Cors:Origins`, because the call is now cross-origin.

`Publish.ps1` bakes in (1) and this guide sets (2). They are the same two strings written twice — if
you change one hostname, change both.

---

## Step 1 — Build

On a machine with the .NET SDK and Node, from a clean checkout:

```bash
powershell -File deploy\scripts\Publish.ps1 -ApiBaseUrl https://copsapi.sivayaantechnologies.com
```

Out comes `deploy\out\`:

| Folder | Where it goes |
|---|---|
| `frontend\` | Upload to Cloudflare Pages |
| `app\` | Copy into the IIS site |
| `sql\` | Run on the SQL Server |
| `scripts\` | Helpers — only needed for the Windows-service route |

The build runs twice on purpose. `app\wwwroot` holds a copy of the UI built against a *relative*
`/api`, so `http://localhost:6061` on the server is a complete working site you can sign in to
before anything is uploaded. `frontend\` is the same source built against the absolute API origin.

To confirm the address really was baked in:

```bash
findstr /C:"copsapi.sivayaantechnologies.com" deploy\out\frontend\assets\*.js
```

---

## Step 2 — The database

On the SQL Server, as a sysadmin. Creates the database, the login, and its rights:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -b -i deploy\out\sql\01-create-database.sql -v AppPassword="<password>"
```

Then the schema — all 43 tables, 184 indexes, 64 foreign keys:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -b -d SCOPS -i deploy\out\sql\03-schema.sql
```

Both are idempotent: run them twice and the second run changes nothing. `-b` matters — without it
`sqlcmd` reports success even when a batch failed, and a half-applied schema looks like a clean run.

Check what you got:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -d SCOPS -Q "SELECT COUNT(*) AS Tables FROM sys.tables;"
```

43 is right (42 plus `__EFMigrationsHistory`).

**The schema is not the data.** The platform operator, the founding company, the expense heads,
units and the material taxonomy are seeded by the application the first time it starts — there is no
SQL for them. An empty-looking database after step 2 is expected.

If you would rather the application create the schema itself, skip `03-schema.sql`; it does the same
work on first start, provided the login has `CREATE TABLE` and `ALTER ON SCHEMA::dbo`. Running the
script by hand means it never needs either.

---

## Step 3 — The API on IIS

### 3.1 Install the hosting bundle

IIS cannot run an ASP.NET Core app on its own — it needs the module that hands requests to it. This
is the step people miss; the symptom is HTTP 500.19 or 502.5 with nothing useful in the log.

```bash
winget install --id Microsoft.DotNet.HostingBundle.10
```

```bash
iisreset
```

### 3.2 Create the site

```bash
New-Item -ItemType Directory -Force -Path C:\Swarnakshi\app, C:\Swarnakshi\data\uploads
```

```bash
Copy-Item .\deploy\out\app\* C:\Swarnakshi\app\ -Recurse -Force
```

The application pool must be **No Managed Code** — the app brings its own runtime and .NET Framework
must not be loaded into it:

```bash
Import-Module WebAdministration; New-WebAppPool -Name Swarnakshi; Set-ItemProperty IIS:\AppPools\Swarnakshi -Name managedRuntimeVersion -Value ''
```

Keep the worker process alive. By default IIS shuts an idle app down after 20 minutes, and the next
visitor waits for a cold start plus the seed check:

```bash
Set-ItemProperty IIS:\AppPools\Swarnakshi -Name processModel.idleTimeout -Value ([TimeSpan]::Zero); Set-ItemProperty IIS:\AppPools\Swarnakshi -Name startMode -Value AlwaysRunning
```

```bash
New-Website -Name Swarnakshi -PhysicalPath C:\Swarnakshi\app -ApplicationPool Swarnakshi -Port 6061 -HostHeader localhost
```

### 3.3 File permissions

The app pool identity writes uploaded attachments and reads the settings file:

```bash
icacls C:\Swarnakshi\data /grant "IIS AppPool\Swarnakshi:(OI)(CI)M" /T
```

```bash
icacls C:\Swarnakshi\app\appsettings.Production.json /grant "IIS AppPool\Swarnakshi:R"
```

(The second one comes after step 4 has created the file.)

---

## Step 4 — The settings file

**This is the only configuration, and the only file you edit.** Copy the template:

```bash
Copy-Item .\deploy\out\appsettings.Production.template.json C:\Swarnakshi\app\appsettings.Production.json
```

```bash
notepad C:\Swarnakshi\app\appsettings.Production.json
```

Four things to set:

| Key | Value |
|---|---|
| `ConnectionStrings:Default` | `Server=.\SQLEXPRESS;Database=SCOPS;User ID=SivayaanHMS;Password=<yours>;TrustServerCertificate=True` |
| `Jwt:Key` | A random string of 32+ characters. Set it once and leave it — it signs every token, so changing it signs everyone out. |
| `Cors:Origins` | `["https://cops.sivayaantechnologies.com"]` — **this is what lets the Cloudflare UI call the API.** |
| `Storage:LocalRoot` | `C:\\Swarnakshi\\data\\uploads` |

To generate a signing key:

```bash
powershell -c "$b=New-Object byte[] 48;(New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b);[Convert]::ToBase64String($b)"
```

**`Urls` is ignored under IIS** — IIS supplies the binding, and the site's port from step 3.2 is what
decides it. Leave the key or delete it; it makes no difference here. It matters only for the
Windows-service route in guide 06.

Then start the site and check it:

```bash
Start-WebSite -Name Swarnakshi; Start-Sleep 5; Invoke-RestMethod http://localhost:6061/health
```

`{"status":"ok"}` means the API is up and reached the database. Because `app\wwwroot` carries a
same-origin copy of the UI, `http://localhost:6061/` in a browser on the server is a full working
site — sign in there and confirm the whole stack before publishing anything.

---

## Step 5 — The tunnel

`cloudflared` connects outward, so nothing inbound is opened and the machine needs no public IP.

```bash
winget install --id Cloudflare.cloudflared
```

```bash
cloudflared tunnel login
```

```bash
cloudflared tunnel create swarnakshi
```

```bash
cloudflared tunnel route dns swarnakshi copsapi.sivayaantechnologies.com
```

`%USERPROFILE%\.cloudflared\config.yml`:

```yaml
tunnel: swarnakshi
credentials-file: C:\Users\<you>\.cloudflared\<tunnel-id>.json

ingress:
  - hostname: copsapi.sivayaantechnologies.com
    service: http://localhost:6061
    originRequest:
      # IIS was bound with a localhost host header in step 3.2; the tunnel must send one that
      # matches, or IIS answers 404 for a site that is running perfectly well.
      httpHostHeader: localhost
  - service: http_status:404
```

Try it in the foreground, then install it as a service so it survives a reboot:

```bash
cloudflared tunnel run swarnakshi
```

```bash
cloudflared service install
```

```bash
curl https://copsapi.sivayaantechnologies.com/health
```

---

## Step 6 — The UI on Cloudflare Pages

Upload the **contents** of `deploy\out\frontend\` — `index.html` at the root of the upload, not a
folder containing it.

In the Cloudflare dashboard: **Workers & Pages → Create → Pages → Upload assets**, then set the
custom domain to `cops.sivayaantechnologies.com`.

Or from the command line:

```bash
npx wrangler pages deploy deploy\out\frontend --project-name=swarnakshi
```

Two files in that folder do quiet but necessary work:

- `_redirects` sends every unmatched path to `index.html` with a 200. Without it, refreshing on
  `/projects/<id>` returns Cloudflare's 404 — the app works until someone reloads a deep link.
- `_headers` caches the hashed assets forever and `index.html` never, so a browser cannot hold an
  old shell that asks for asset files the new deploy has already replaced.

---

## Step 7 — First sign-in

Open `https://cops.sivayaantechnologies.com`.

| Account | Login | For |
|---|---|---|
| Owner | `owner@swarnakshi` | The company's first Owner |
| Platform operator | `EnterpriseAdmin` | Licence renewal and password resets only; never sees company data |

Both carry the application's default passwords until you change them. **Change them now.** The
seeder sets a password only when it creates the row, so a changed password is never reset by a
restart or a later deployment.

---

## Later releases

```bash
powershell -File deploy\scripts\Publish.ps1 -ApiBaseUrl https://copsapi.sivayaantechnologies.com
```

**API** — back up first, then stop the site, copy, restart. IIS holds the DLLs, so a copy over a
running site fails:

```bash
powershell -File deploy\scripts\Backup-Database.ps1 -Label pre-upgrade
```

```bash
Stop-WebSite -Name Swarnakshi
```

```bash
Copy-Item .\deploy\out\app\* C:\Swarnakshi\app\ -Recurse -Force -Exclude appsettings.Production.json
```

```bash
Start-WebSite -Name Swarnakshi
```

The app applies any new migrations on start. `-Exclude` is what keeps your settings file; without it
the copy would overwrite it with the template's placeholders and the site would refuse to start.

Empty `wwwroot` before copying if a release removed an asset — a stale hashed file is served forever
otherwise:

```bash
Remove-Item C:\Swarnakshi\app\wwwroot -Recurse -Force
```

**UI** — upload `deploy\out\frontend\` again. Cloudflare keeps the previous deployment, so rolling
back is a click in the dashboard.

Keep them in step. The UI and API are versioned together, and a UI newer than its API will call
endpoints that do not exist yet.

---

## When it does not work

| Symptom | Cause | Fix |
|---|---|---|
| UI loads, every request fails, console says CORS | `Cors:Origins` missing the UI origin | Add `https://cops.sivayaantechnologies.com`, restart the site |
| Requests go to `cops.…/api` and 404 | UI built without `-ApiBaseUrl` | Rebuild with it and re-upload |
| HTTP 500.19 or 502.5 | Hosting bundle not installed | Step 3.1, then `iisreset` |
| 500.30 on start | Bad connection string or missing `Jwt:Key` | Run `C:\Swarnakshi\app\Swarnakshi.Api.exe --migrate` by hand — it prints the real error |
| Tunnel returns 404, `localhost:6061` is fine | Host header mismatch | `httpHostHeader: localhost` in the ingress rule |
| Refreshing a deep link 404s | `_redirects` missing from the upload | Re-upload the whole `frontend\` folder |
| First request each morning is slow | App pool idling out | Step 3.2's `idleTimeout` and `startMode` |
| `Login failed for user 'SivayaanHMS'` | Password in the settings file no longer matches | `02-rotate-password.sql`, then edit the connection string to match |
