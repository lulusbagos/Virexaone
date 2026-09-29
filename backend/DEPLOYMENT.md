# Backend deployment

Release boundary (2026-09-27): the cabin communication, road-audit, and migration 004 instructions below describe locally prepared source. They are not proof that the matching code was committed to this repository, copied to `C:\Apps\Backend`, enabled, or tested on devices. Check the actual release contents and server response before following a feature-specific procedure. The existing fleet/MTC deployment remains a separate, previously verified baseline.

## cobacoba.idccoal.id rollout

The cabin's Data sumber panel uses `GET /api/v1/cabin/hexagon/{unitName}`. It reads existing Hexagon equipment, traveling, hauling, assignment, and location rows without writing them. Raw IDs remain raw and missing values remain unavailable. After publishing the new backend, run `./verify_deployment.ps1 -CabinUnit DT5107 -RequireComms` from an authorized network (substitute a real site unit). The comms check fails until the server's private `CabinComms` settings are enabled.

The in-cabin Flutter client reads `/api/v1/fleet/live`, `/api/v1/mtc/live`, `/api/v1/locations/actual`, and `/api/v1/weather/current`. The fleet response now includes `heading_available` so a missing source heading cannot be mistaken for true north. `/locations/actual` queries Hexagon locations without cached or generated fallback. Deploy this backend before distributing a new mobile build. The client requires HTTPS and does not embed a Cloudflare Access service token or database credential; authorize cabin devices through a suitable Access/VPN flow. A 502 from the public domain is an upstream/tunnel failure, not a mobile GPS problem.

`GET /api/v1/fms/roads/audit` is a read-only quality check of current Hexagon road records. It reports missing or invalid distance, endpoints, width, speed, and trajectory. `scope=priority` limits rows to geometry/endpoint issues; `scope=all` includes missing metadata. Use `search`, `offset`, and `limit` (10-100) to inspect the result. Counts can change while source roads are edited. This endpoint does not validate routing, alter Hexagon data, or publish Astha drafts.

The Unity main scene already uses `https://cobacoba.idccoal.id` as its API base URL. Changing the Unity URL does not update the backend process running on that host. Publish this `Backend` project as .NET 10 for the server's OS, configure the required environment variables below, and point the HTTPS reverse proxy at the Kestrel process. Do not publish `appsettings.Development.json` as production configuration or put database credentials in the WebGL files.

From the canonical `Backend` folder, run `./publish_backend_server.ps1`. Copy the entire resulting `../Builds/Backend_Windows_x64_<timestamp>` folder to `C:\Apps\Backend` on the Windows server. The package contains a self-contained Windows x64 executable and public application settings; no .NET installation or BAT file is needed. It deliberately excludes `appsettings.Local.json`. Preserve the existing private `C:\Apps\Backend\appsettings.Local.json` on the server, or set `FmsSettings__DbPassword` in the service environment before starting the new EXE. Do not copy the source tree, `bin`, or `obj`. Protect the deployment folder so only the backend service account and administrators can read it.

On the Windows host, run `Virexaone.FMS.Backend.exe` directly. No password prompt appears. Kestrel binds to `0.0.0.0:8000`, so it is reachable through `172.16.1.92:8000` when that IP belongs to the host and firewall permits it. Startup exceptions appear in the console and `backend-error.log` beside the EXE. Restrict inbound port 8000 in the Windows firewall to trusted LAN/proxy IPs; do not expose this plain HTTP port to the Internet. The private JSON file contains the database credential in plaintext. Never commit it, upload it to a public site, or include it in WebGL files; use a dedicated least-privileged database account and rotate the current password before production use. A Windows service manager should restart the process after failure.

This site's `Auth:Mode` is `LocalProxy`: Google ID is not required. Direct clients are limited in the application to loopback and private IPv4 ranges; exact additional proxy IPs can be listed as `Backend__TrustedClientIps__0`, etc. **The reverse proxy or VPN must still restrict access to the API before exposing the domain publicly.** A public proxy on the same host reaches the backend as a trusted local client, regardless of the original Internet user's IP. `Google` mode remains available by setting `Auth__Mode=Google` and configuring `Google__ClientId` plus `Access__AllowedGoogleSubjects__0`. Set `Cors__AllowedOrigins__0` if the WebGL page is hosted on a different origin.

The Astha site's host, database name, and `postgres` username are in `appsettings.json`. The password belongs in the existing private server-side `appsettings.Local.json` or a service environment variable; it is not in the publish package. Environment variables override both files. The startup error `Missing FmsSettings:DbHost` means the server is running an older deployment or is missing the updated settings. If the EXE starts but `/health` reports `degraded`, inspect backend logs and database/GPS availability next. Replace the `postgres` superuser with a dedicated least-privileged account before production use.

After deployment, run `./verify_deployment.ps1` from an authorized network. The expected `/` response contains only app, version, and status; `/health` reports `healthy` only when the database and GPS feed are current. `/api/v1/site/profile`, `/api/v1/fleet/live`, `/api/v1/fms/catalog`, and `/api/v1/fms/map-drafts` should respond in LocalProxy mode. The FMS catalog is read-only and preserves the database's disabled menu flags; it does not enable planned features. A 404 on a FMS route means the old backend is still running; a 502 means the reverse proxy or upstream service needs attention.

The map editor stays read-only by default. To enable create/update/archive for the dedicated Astha site, set `FmsMapEditor__Enabled=true` and a unique random `FmsMapEditor__Key` of at least 32 characters in the server environment or its private local settings. Restart the backend. The Unity editor sends that key in `X-FMS-Editor-Key` only for writes. This shared key does not identify individual users; keep Cloudflare Access/VPN and the port 8000 firewall restriction in place. The draft endpoint never publishes shapes into the live Hexagon road graph.

### Cabin messages and voice

Before enabling communication, stop the server. On a machine with this backend source, its Release DLLs, and access to the same site database, run `pwsh -NoProfile -File .\Migrations\Apply-FmsPreparation.ps1` and then the same command with `-Apply`. The server publish package does not include the migration runner. Publish/copy the updated backend, set `CabinComms__Enabled=true` and a randomly generated `CabinComms__DispatcherKey` of at least 32 characters only in the server environment/private local settings, then restart the EXE. `GET /api/v1/comms/status` must return `enabled: true`; a 404 means the old EXE is still running. Keep Cloudflare Access/VPN and the port 8000 firewall restriction active. Do not place this key in a WebGL build, APK, repository, or deployment package.

In Unity, open Pesan Operasi, enter the dispatcher key for the session, select a real unit, and choose Pasangkan unit. Copy the displayed code into that unit's mobile Pesan menu. Verify a cabin text reply appears in Unity and a dispatcher text message appears in mobile, including the mobile popup and Indonesian text-to-speech. Select the same unit for radio on both sides, then test live push-to-talk in both directions. The client requests a single-use, 30-second WebSocket ticket with its own credential; audio is PCM16 mono at 16 kHz and is relayed in memory, not saved. Only one side can speak on a unit channel at a time. The mobile app reconnects and polls message history if its socket drops. Keep the app in the foreground for live radio; background audio and delivery acknowledgements are not implemented. The old recorded-audio API routes have been removed; historical table rows are left untouched.

Confirm the public `GET /api/v1/comms/status` returns `enabled:true` and `mode:"live_pcm16_ws"` after replacing the server EXE; `enabled:false` means the private feature switch or dispatcher key is absent. Confirm the Cloudflare Tunnel/Access path permits WebSocket upgrades to `/api/v1/comms/live` and HTTPS requests to `/api/v1/comms/live-ticket`. Test both over the public hostname, not just localhost. A valid Access session is still required for both devices. Rotating a unit pairing code blocks new connections with the old code; existing sockets must be disconnected by restarting the backend until active-session revocation is added.

For a 403, inspect the response header `X-Virexa-Denied-By`. The value `backend-ip-policy` means the request reached Kestrel but the direct peer IP was rejected; the same IP and path are logged as a warning. Add only the exact trusted reverse-proxy IP to `Backend:TrustedClientIps` (or `Backend__TrustedClientIps__0`) after confirming that proxy enforces Access/VPN. A 403 without this header originated at Cloudflare, IIS, Nginx, or another upstream layer. Never whitelist arbitrary public clients or trust an unverified `X-Forwarded-For` header to bypass the backend policy. Cloudflare cannot connect directly to a private `172.16.x.x` origin over the public Internet; use a correctly configured tunnel or reachable HTTPS reverse proxy.

For Cloudflare Tunnel, set the public hostname service to `http://127.0.0.1:8000` when `cloudflared` and the EXE run on the same Windows host. First verify `http://127.0.0.1:8000/health` locally on that host. If `cloudflared` runs on another LAN host, use `http://172.16.1.92:8000` only after verifying that host can connect to TCP 8000 and limiting the Windows firewall rule to the tunnel host's IP. A 502 from Cloudflare usually means the tunnel cannot reach its configured service; check the `cloudflared` logs and hostname ingress. A 403 without `X-Virexa-Denied-By` means Cloudflare Access/WAF denied the browser before it reached this backend; verify the Access application policy and the user's session. Keep Access/VPN enforcement enabled for this unauthenticated LocalProxy backend.

The current deployment boundary is **one site, one dedicated PostgreSQL database, one backend process**. The API must not serve two companies from a shared database until every query, cache, stream, and asset is tenant-scoped and isolation tests are in place. A tenant ID supplied by a client is not an authorization mechanism.

## Required configuration

Set secrets in the host environment or another private configuration provider. Do not commit them, embed them in Unity, or put them in a public WebGL page.

| Environment variable | Purpose |
| --- | --- |
| `FmsSettings__DbHost` | PostgreSQL host |
| `FmsSettings__DbName` | Database name |
| `FmsSettings__DbUser` | Least-privileged read-only account |
| `FmsSettings__DbPassword` | Database password |
| `FmsSettings__DedicatedSiteDatabase` | Set in `appsettings.json`; the database must be isolated to this site |
| `Auth__Mode` | `LocalProxy` for restricted network or `Google` for token authentication |
| `Google__ClientId` | Only required in Google mode |
| `Access__AllowedGoogleSubjects__0` | Only required in Google mode |
| `Cors__AllowedOrigins__0` | Exact trusted WebGL origin, including scheme |

Google mode requires Google-issued ID tokens in `Authorization: Bearer <token>` for `/api/v1/*` and `/hubs/telemetry`. LocalProxy mode has no application-level login and must be protected by the reverse proxy/VPN. `/` and `/health` do not disclose database host or name.

Operators on the trusted network can inspect `/api/v1/ops/health` for database and GPS feed diagnostics. `/api/v1/site/profile` supplies the site identity used by Unity's startup screen.

Configure `FmsSettings__RefEasting`, `RefNorthing`, `RefElevation`, `SiteLat`, `SiteLon`, `HexOriginX`, `HexOriginY`, `HexScaleM`, and `UtmEpsg` for each site. The defaults in `appsettings.json` are for Astha. The current ingest understands Hexagon survey coordinates and WGS84 longitude/latitude; other source formats need an adapter before onboarding. `FmsSettings__WeatherRadiusKm` limits weather requests to the site's vicinity.

`FmsSettings:UnitCategories` maps exact source equipment type labels to `Hauler`, `Excavator`, `Bulldozer`, `Grader`, `WheelLoader`, `FuelTruck`, or `Support`. Example environment value: `FmsSettings__UnitCategories__Mine_Excavator=Excavator`. Inspect `/api/v1/fleet/types` after authenticating to see actual source labels and counts. Unknown types are reported as `Support` by default.

Unity renders unmapped support types with a neutral placeholder rather than presenting them as haul trucks. Map a source type and assign a matching GLB before treating that visual as the actual machine.

The live fleet API sends `reference_length_m` as a type-based visual estimate, not the raw equipment `length` column. That column currently contains generic 20 m values even for dozers, graders, and light vehicles. For a verified machine dimension, configure `FmsSettings:UnitLengthOverridesM:<unit name>` (for example `FmsSettings__UnitLengthOverridesM__DZ3001=8.7`). Only 2-40 m values are accepted. A unit's exact make/model is not present in the current equipment feed, so type defaults are not manufacturer-certified dimensions.

## Important before production

- Rotate the database password and API key that were previously committed; removing values from the current file does not remove Git history or deployed copies.
- Restrict the public domain at the reverse proxy or VPN before using LocalProxy mode. If that is not possible, switch to Google mode and complete the Unity sign-in flow first.
- Use a valid HTTPS certificate. The Unity client no longer ignores certificate errors.
- Deploy separate databases and backend instances for separate companies. Shared-database tenancy is not implemented.
- A distributed Unity/WebGL client can be copied or modified. Protect data and license entitlements at the server, and optionally sign build artifacts; do not place enforcement secrets in the client.
