# FMS readiness - Virexa One

Status: preparation only. No new dispatch action, automatic assignment, route decision, or simulated production metric is enabled by this document.

Repository note (2026-09-27): some backend/Unity communication, road-audit, and migration 004 changes described below are still local worktree changes, not part of the pushed source mirror or a verified server deployment. Treat those sections as implementation notes pending a separate backend release.

Database preparation applied to `DB_FMS`: migrations 001-003 now create 25 isolated `_astha` tables and a hidden, disabled catalog of 34 FMS menu entries. Migration 003 seeds the dedicated Astha company/site and adds map drafts. Existing Hexagon tables remain unchanged. `../../Backend/Migrations/README.md` lists the table groups and application procedure. A release-notes PDF is not a complete ManagerPro menu or map-editing guide; obtain the actual specification before claiming parity.

Backend UI contract: `GET /api/v1/fms/catalog` reads the menu catalog without enabling planned functions. The Windows publish package passed `/health`, fleet, and catalog smoke tests. On 2026-09-26, the public domain returned 200 for `/health`, fleet, MTC, and the catalog; `/api/v1/ops/health` reported a connected database and fresh GPS. Recheck these endpoints after each deployment.

Unity now has a separate `FMSModulePanel` opened from the top navigation on desktop or the Panel menu on narrower screens. It reads the catalog, reports a missing or unavailable endpoint, and keeps planned entries non-actionable. Its MTC action opens the existing read-only Unity MTC panel. No existing map, fleet, or production UI was replaced. The scene's configured API URL matches the verified public domain.

The FMS panel also opens a map-draft editor in Unity. It draws road lines, area rectangles/circles/polygons, and text labels on the existing map, with create/update/archive limited to the new `tbl_m_map_draft_astha` table. Writes are disabled until a server-side editor key is configured. Drafts do not affect the live road layer or routing. Backend create/update/archive and disabled-write checks passed locally; the updated build has not yet been deployed to the public domain.

## Reference and boundary

The FMS panel also opens a read-only road audit. `GET /api/v1/fms/roads/audit` checks current `tbl_m_roads_hexagon` records against endpoint locations, reports missing or invalid attributes, and supports search and paging. This is a data-quality view, not route validation or misroute detection. It changes no Hexagon or Astha tables. The endpoint passed local live-database checks; deploy the updated backend before expecting it on the public domain.

- Reference reviewed: `D:\MineOperate_OP_Pro_4_0_Internal_Release_Notes_A4_en_v1.1.pdf`, internal release notes for OP Pro 4.0, document version 1.1 (2023). It is confidential and must not be copied into this repository or deployment packages.
- Release notes describe selected features and fixes, not a complete API, database schema, or permission model. Do not infer that the current site has every mentioned table, sensor, configuration, or workflow.
- Unity 6000.4.10f1 remains the read-only visualization client. The canonical API and database adapter live in `../../Backend`; the mobile app remains separate.
- The backend currently serves one dedicated site database in `LocalProxy` mode behind Cloudflare Tunnel and Access/VPN. Multi-company use requires tenant isolation and authorization before data or controls are shared.

## Existing surfaces

| Area | Current implementation | Preparation decision |
| --- | --- | --- |
| Fleet map and GPS | `FMSFleetManager` reads `/api/v1/fleet/live`; the backend reads equipment, traveling, hauling, and GPS history. | Preserve source timestamp, GPS validity, and stale status. Never animate missing data as if live. |
| Fleet assignment | Backend reads `tbl_m_assignments_hexagon`; `/api/v1/mtc/live` suppresses the fallback dispatch list. | Treat assignment as observed database state, not authority to reassign. Resolve duplicate assignments by equipment ID and effective time after schema audit. |
| MTC | Unity panel and separate `/mtc/` view read the same `/api/v1/mtc/live` snapshot. The Unity panel orders GPS-fresh units by straight-line proximity to the excavator, shows adjacent-unit and excavator distances, and reveals all pairwise distances for a selected unit. | The order is schematic, not a verified road route. Stale GPS yields unavailable distances. Both views are read-only until a safe command flow exists. |
| Status and activity | Unit state is derived for presentation in `FMSUnitController` and `FMSSmartDispatchManager`. | Preserve raw status/activity codes and timestamps separately from derived labels. Delay, Ready, Loading, and Queueing must not be conflated. |
| Dispatch suggestion | `FMSSmartDispatchManager` displays queue indications; reassignment action is intentionally disabled. | Keep decisions and writes on a validated server-side workflow, never in a Unity button alone. |
| Road and locations | Backend exposes `/roads/network`, `/locations/all`, and read-only `/fms/roads/audit` for missing/invalid source attributes. | Audit connectivity, categories, direction, restrictions, and coordinate system before routing or misroute detection. |
| Production and material movement | Backend exposes `/production/summary`; other UI counters may represent different sources. | Maintain a metric-source label, shift boundary, record identity, and reconciliation rule for every number. |

## Contracts required before feature work

1. **Identity and tenancy:** stable site ID, equipment ID, equipment type, operator ID where available, and source-system ID. UI names are labels, not keys. No cross-company query or cache without tenant-scoped tests.
2. **Telemetry observation:** source timestamp, ingestion timestamp, coordinate reference system, position, speed, heading, validity, and freshness threshold. A stale observation remains historical, not live.
3. **Assignment snapshot:** truck ID, shovel ID, destination ID, source timestamp, effective interval, lock/restriction state if the source supports it, and revision. Missing fields must be reported as unavailable, not defaulted to false.
4. **Status and activity event:** raw code, equipment ID, event time, source, and transition context. Render a derived state only when mapping is verified with site operators.
5. **Road graph:** node/segment IDs, real geometry, direction, category, distance, availability, speed/restrictions, and update time. Euclidean GPS spacing is not travel distance.
6. **Haul and shift event:** immutable event identity plus equipment, load/dump times, shovel, destination, payload, shift, and any source-specific disambiguator. Do not join events on timestamp alone.
7. **Command audit:** before any reassignment or status change, require authenticated actor, current revision, precondition validation, confirmation in the active window, idempotency key, result event, and audit trail.

## Release-note signals to validate with site data

| Reference | Future capability | Gate before implementation |
| --- | --- | --- |
| 4.1.1, 4.1.2, 4.2.2 | Preserve loading activity across shovel delay; honor locked-truck and delayed-destination assignment rules. | Confirm current event and configuration sources; test each transition with real site examples. |
| 4.2.6, 5.3.2 | MTC desired trucks/coverage and detached reassignment confirmation. | Find actual region target data; no invented target values. Keep detached MTC read-only until server command and confirmation exist. |
| 4.2.7, 4.2.8, 5.3.14 | Road-segment misroutes and low-priority paths. | Validate road graph topology, categories, and segment-distance semantics against GIS source. |
| 4.3.5 | Loading detection within shovel radius. | Verify GPS accuracy, payload/dipper source, configured radius, and event ordering. |
| 5.3.1, 5.3.3, 5.3.15 | Shift history, material movement joins, and reconciliation. | Use bounded shift queries; inspect source keys and any extra-load discriminator; never treat latest/future shift records as settled. |
| 4.3.1, 4.4, 5.5 | Refuel, engine hours, high-precision shovel/sensor data. | Enable only where the site has verified sensors, units, permissions, and calibration. |

## Mobile in-cabin menu mapping (2026-09-27)

The new mobile Data sumber tab reads `GET /api/v1/cabin/hexagon/{unitName}` from a read-only backend projection of the six actual Hexagon tables. A local DT5107 query returned GPS satellites, raw quality/road IDs, named current/next locations, prestart, and haul counters; null tonnage is displayed as unavailable. The public domain had 205 fleet units and returned `enabled:false` from `/api/v1/comms/status` on 2026-09-27. This is not an end-to-end communication success: deploy the new backend endpoint, activate comms only with a private dispatcher key behind Access/VPN, then pair a unit and test text and live two-way radio on real devices.

### Cabin messages and live radio

The application-owned `004_cabin_comms_astha.sql` migration adds paired cabin access and site-scoped messages; it does not write Hexagon tables. Unity dispatch can send text and create a one-unit pairing code. The current clients use short-lived tickets and a WebSocket for live bidirectional, half-duplex PCM radio. Incoming text appears as a mobile popup and is read aloud in Indonesian; message polling covers a missed socket push. Live audio is not stored. The old Unity-only simulated messages and fake talkback delivery have been removed. The recorded-audio API routes are removed; existing historical rows are unchanged.

The backend feature is disabled by default and needs `CabinComms__Enabled=true` plus a private, random `CabinComms__DispatcherKey` on the deployed server. The current public domain returns 200 for `/api/v1/comms/status`, but `enabled:false`; source and database migration alone do not activate communication. Pairing codes are rotated per unit and kept only in mobile process memory, then cleared on logout or API change. This shared-key stage is suitable only behind the existing Access/VPN and dedicated-site database; per-user identity, audit, active-socket revocation, delivery acknowledgements, background audio, and on-device two-way validation remain future work.

The cabin client at `../../virexa_mobile` now has a read-only FMS menu. The source guide is `D:\1. DOKUMEN\99.Astha\FMS\MineOperate_OP_Pro_Manager_3_0_URM_A4_en_v7_2.pdf`, especially sections 2.6 (truck/shovel cycle), 4.3 (MTC), 5.9-5.11 (assignments, messages, emergencies), 5.19 (prestart), 21 (trucks), and Appendix C sections 30.4-30.10 (equipment panel, status, navigation, GPS, diagnostics). The DozerHP guide sections 4-11 are a separate machine-specific workflow, not a hauler tab. Asset Health and release notes are not treated as proof that this site's sensors or commands exist.

| Cabin menu | Current source | Availability |
| --- | --- | --- |
| Operasi | `/api/v1/fleet/live`: unit type, raw status ID, activity, GPS age, speed, payload/load only when source flags say available | Read-only, actual Hexagon-backed API |
| Penugasan | `/api/v1/mtc/live`: latest matching truck assignment; `/api/v1/locations/actual` and fleet GPS for straight-line navigation | Read-only, stale assignment explicitly identified |
| Armada | `/api/v1/fleet/live`, filtered to fresh GPS | Read-only; not a collision-avoidance system |
| Lokasi | `/api/v1/locations/actual` | Read-only; no road route inferred |
| Diagnostik | API connection, unit GPS age, source heading and UTM coordinates | Read-only; no fabricated sensor health |
| Data sumber | `/api/v1/cabin/hexagon/{unitName}`: equipment, traveling, hauling, assignment and named locations | Read-only; raw source codes and timestamps, no inferred status |
| Status/reasons, emergencies, prestart, defects, road hazards, KPI and maintenance | Documented workflows without verified site-specific command/read contracts | Planned only; no live action |
| Cabin messages and live radio | Application-owned gated endpoint and client UI | Implemented in source; deployment and device validation must be verified separately |

The mobile menu does not query or write Hexagon tables directly. Existing Hexagon UI and tables remain untouched. Operational values come from backend reads of real source records; missing values must stay unavailable. Before enabling cabin dispatch/status/production commands, build authenticated operator identity, tenant/site scope, idempotent server commands, validation, audit, offline/retry behavior, and a controlled non-production trial. A unit selector alone is **not** operator authentication. Application-owned messages are a separate, feature-gated write path.

Panah and Jejak show a straight-line target bearing and observed GPS breadcrumb, not the road navigation described in Appendix C 30.8.5. Road guidance requires a validated road/location layer and usability checks. A 2026-09-27 public-domain check briefly returned HTTP 502 before `/health` recovered; the observed fleet snapshot had 205 units, including 163 haulers, while `heading_available` was absent and `/locations/actual` returned 404. These are historical observations, not permanent current counts or endpoint guarantees. The mobile client can derive heading availability from a timestamp-matching real trajectory sample when the flag is absent and falls back to `/locations/all` only on a 404, filtering to the active fleet area. Deploy and verify the newer backend to remove these compatibility paths. A device test entered cabin DT5107 and displayed source GPS/assignment data with the FMS menu open.

The cabin HUD uses a layered guidance chevron and smooths only its visual rotation over 700 ms; bearing and distance still come from the latest real unit GPS. It withholds guidance once the selected unit GPS is older than 45 seconds. Nearby unit radar markers also require GPS age at most 45 seconds, with names/distances in a side list so closely spaced units do not cover the arrow. The Fleet menu separately offers an inventory filter for GPS samples up to 2 minutes old and labels that threshold explicitly; those older samples are not treated as live cabin guidance. Fleet and Locations now have search, and the cabin dock opens Messages directly. These remain straight-line GPS cues, not a road-following route or collision-avoidance instruction.

## Sequence for the next development phase

Navigation update (2026-09-27): the cabin now has both Panah and Jejak. Both use the same selected-unit GPS, target and `CabinBearing.headingUpOffset` basis; Jejak's center is a vehicle silhouette, while its destination has a separate marker. Panah's elevated projection compensates lateral foreshortening. The Jejak breadcrumb contains distinct source points seen while the app runs, not a verified road route or full historical playback. A stopped unit's source heading can remain its last valid orientation; a matching UI angle is not independent proof of a correct physical compass direction. Compare raw heading, GPS point times, and target coordinates for any specific suspect unit.

1. Inventory read-only database schema and sample time ranges for assignments, equipment status/activity, GPS, roads, locations, hauls, shift loads/dumps, payload, and sensors. Record table/column types, nullability, keys, update cadence, and data owner.
2. Build a source-to-contract mapping and fixtures from redacted real records. Add tests for stale GPS, duplicate assignments, missing shovel, delayed destination, conflicting state, and route gaps.
3. Split future FMS presentation from `FMSDashboardUI` into focused views only when a verified API contract exists. Retain the current Unity and web MTC views as read-only during this step.
4. Add operational read models in the backend, with source age and data quality visible in the UI. Do not use fallback simulation in production read models.
5. Design command authorization, site isolation, and audit with mine controllers before enabling any write workflow. Validate on a non-production site first.

## Acceptance gates

- Every visible operational value is traceable to a source record and time, or is explicitly labeled as a derived estimate.
- A missing or stale source cannot silently become Ready, Loading, assigned, or available for dispatch.
- The same site, equipment, assignment, and shift boundaries are used by Unity, backend, MTC, and mobile clients.
- A detached window does not bypass authorization or lose confirmation/audit context.
- Database read models and any future writes are isolated by company/site and tested across at least two distinct tenants before multi-company rollout.
