# Virexa One system blueprint

Updated 2026-09-27. This maps architecture and data ownership; it does not claim that every prepared feature is deployed. See [FMS readiness](FMS_READINESS.md) and the [roadmap](../FMS_ROADMAP_AND_DATABASE_SPECS.md).

## Document ownership

| Document | Purpose | Authority |
| --- | --- | --- |
| This blueprint | Runtime boundaries, data provenance, and release gates | Architecture overview |
| [FMS readiness](FMS_READINESS.md) | Source audit, feature status, and open acceptance criteria | Detailed preparation record |
| [Roadmap](../FMS_ROADMAP_AND_DATABASE_SPECS.md) | Delivery order and database ownership | Planning, not progress percentage |
| [Developer guide](../VIREXAONE_FMS_DEVELOPMENT_GUIDE.md) | Source layout and verification workflow | Engineering instructions |
| [Backend deployment](../Backend/DEPLOYMENT.md) | Server configuration and rollout checks | Deployment runbook |
| [Migration notes](../Backend/Migrations/README.md) | Application-owned schema and migration procedure | Schema runbook; check file availability first |
| [Cabin README](../virexa_in_cabin_flutter/README.md) | Mobile contract and device verification | Cabin runbook |

The local runtime projects are `../../Backend` and `../../virexa_mobile`. Their repository mirrors are `../Backend` and `../virexa_in_cabin_flutter`. Synchronize each pair before a release. A GitHub document may describe work in a local branch/worktree; only committed files are reproducible from GitHub. Review the commit and server build separately.

```text
Hexagon PostgreSQL (existing operational tables; read-only)
          |
          v
ASP.NET Core API (:8000; one configured site database)
          | REST snapshots / gated WebSocket cabin communication
          +------------------+---------------------+
          |                  |                     |
    Unity digital twin   Flutter cabin       Detached MTC web
```

The canonical local backend is `../../Backend`; `../Backend` is its Git mirror. Cloudflare Tunnel and Access/VPN protect the published domain. Port 8000 must not be an unrestricted public API. LocalProxy trust and shared keys are not per-user identity or multi-company authorization.

## Data contracts

| Surface | Source | Meaning / limit |
| --- | --- | --- |
| Fleet | `/api/v1/fleet/live` from equipment/GPS | Observation time and freshness matter; no synthetic motion |
| MTC | `/api/v1/mtc/live` from assignments/GPS | Excavator fleet spacing is straight-line, not road distance |
| Map | Hexagon roads/locations, separate Astha drafts | Drafts do not alter live roads or dispatch |
| Cabin guidance | Selected-unit GPS/heading plus target GPS/location | UTM straight-line bearing; Panah and Jejak share a heading-up transform |
| Cabin Jejak | Source GPS points received while app runs | Observed breadcrumb, not a road route or complete historical playback |
| Production | Backend source-record read model | Preserve payload/ritase availability and reconciliation rules |
| Weather | Open-Meteo through backend | External report, not an on-site sensor |
| Messages/radio | `_astha` pairing/messages; gated WebSocket audio | Verify deployed enablement, pairing, delivery, and physical two-way audio |

The cabin does not use phone GPS. Source heading is not necessarily a current physical compass at zero speed. GPS course between samples may differ during turns, delays, or noisy fixes. Both cabin modes calculate orientation with `CabinBearing.headingUpOffset`; the elevated projection compensates lateral foreshortening. Jejak's center silhouette represents the unit and its separate marker represents the destination.

## Ownership and release state

Migrations 001-003 prepare `_astha` tables without altering Hexagon tables. Migration 004 for pairing/messages is prepared in the local backend worktree but must be committed, deployed, and verified before it can be treated as part of a release. The map-draft table remains separate from the operational road graph. See [migration notes](../Backend/Migrations/README.md). A prepared table is not an active command workflow.

- **Source implemented:** code exists locally or in Git and may be disabled.
- **Deployed:** a specific build runs on the server; verify endpoints from an authorized network.
- **Operationally validated:** correct permissions, fresh source records, and physical devices have been tested together.

Do not collapse these states. `/health` and fleet response do not prove map drafts, messages, or radio are enabled.

| Capability | Repository state at this revision | Deployment evidence |
| --- | --- | --- |
| Cabin Panah/Jejak shared bearing | Committed Flutter source and navigation assertions | Installed and visually checked on one Android device; not a survey-grade heading validation |
| MTC read-only fleet view | Committed Unity/backend source | Verify the running build and source GPS age for each session |
| Map drafts | Committed preparation schema and editor/read API | Drafts remain isolated from live roads; writes depend on private server configuration |
| Cabin messaging/live radio | Backend/Unity changes and migration 004 pending in local worktree | No claim of successful server rollout or two-way device audio |
| Road audit | Backend/Unity changes pending in local worktree | No claim that the public domain serves the new endpoint |
| Multi-company dispatch and road navigation | Not implemented as safe operational workflows | Requires separate authorization, graph, and audit work |

## Gates before broader FMS use

1. Preserve source IDs, timestamps, GPS validity, units, and missing values across API/UI.
2. Validate topology and publish/version a reviewed road graph before route guidance; the current target line is not a drivable route.
3. Require individual actor identity, tenant/site authorization, idempotency, and audit before operational writes.
4. Reconcile haul events with source-specific keys before treating production/ritase as complete.
5. Test at least two isolated tenants before claiming multi-company readiness.
