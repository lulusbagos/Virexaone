# Virexa One developer guide

Start with [the blueprint](Design/SYSTEM_BLUEPRINT.md), [FMS readiness](Design/FMS_READINESS.md), [mobile README](virexa_in_cabin_flutter/README.md), and [backend deployment](Backend/DEPLOYMENT.md). This guide is not a claim of MineOperate feature parity.

## Source layout

| Path | Responsibility |
| --- | --- |
| `Assets/Scripts/` | Unity digital twin, MTC, map, and dispatcher presentation |
| `virexa_in_cabin_flutter/` | Git mirror of the Flutter cabin app |
| `Backend/`, `Backend.Tests/` | Git mirrors of canonical API source/tests in `../Backend/`, `../Backend.Tests/` |
| `Backend/Migrations/` | Application-owned `_astha` schema; no Hexagon DDL |
| `Design/` | Architecture and readiness decisions |

Edit the canonical backend locally, then run `sync_backend_source.ps1 -Apply` and its check mode before staging the mirror. In a clone without sibling folders, build the backend in this repository. Never commit passwords, editor/dispatcher keys, generated output, or publish packages.

The active cabin source is `../virexa_mobile`; synchronize it to `virexa_in_cabin_flutter/` before a mobile commit. Documentation must make the same distinction as code reviews: local source, committed source, deployed server, and verified physical-device behavior are separate states. Do not publish a runbook that references a migration file or endpoint absent from the same release without marking it pending.

## GPS and navigation

`/api/v1/fleet/live` supplies selected-unit GPS and source heading. The cabin never substitutes phone GPS. UTM easting/northing give a straight-line target vector; Panah and Jejak use the same heading-up basis in `CabinBearing.headingUpOffset`. Panah projects it into an elevated view; Jejak plots it top-down. Jejak's center is a vehicle silhouette, not a second target arrow. Its breadcrumb comprises received GPS points, not a road route. Smoothing affects presentation only.

Require fresh source GPS and a valid target. Show held/unavailable states distinctly. Reported heading can disagree with point-to-point course, especially when stopped or turning; do not silently replace it with a calculated course. A validated road network and map matching are required before calling guidance a route.

## Backend and communication

REST supplies fleet, MTC, location, production, weather, and cabin source projections. Cabin messaging is feature-gated. Live talkback uses short-lived tickets and a WebSocket PCM half-duplex stream, not SignalR or stored voice clips. `/api/v1/comms/status` does not prove pairing, delivery, or device audio. Test all stages behind Cloudflare Access/VPN. A shared dispatcher key is not individual operator authentication.

## Release checks

1. Build/test backend source and verify migration ledger on the intended database without touching Hexagon tables.
2. From an authorized client, check `/health`, `/api/v1/fleet/live`, `/api/v1/mtc/live`, and required feature-gated endpoints.
3. Run Flutter analyzer, navigation assertions, and APK build. Compare Panah/Jejak for the same unit/target on a physical device.
4. Verify Unity UI in the target build; an API response alone does not validate rendering.
5. Deploy the tested build and repeat network/device checks. Keep local pending work distinct from what is live on the domain.
