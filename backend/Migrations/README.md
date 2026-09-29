# FMS preparation schema

`001_fms_preparation_astha.sql` creates 24 new `public` tables, including the migration ledger. `002_fms_audit_site_scope_astha.sql` makes audit-to-command references site-scoped. `003_fms_map_draft_astha.sql` adds one isolated table for application-owned map drafts and seeds the dedicated Astha company/site records. These committed migrations prepare 25 `_astha` tables. Migration `004_cabin_comms_astha.sql`, when included in a release and applied, adds two site-scoped cabin pairing/message tables for a total of 27. It is still pending in the local backend worktree at this documentation revision; do not assume it exists in a GitHub clone or on the server. The 34 catalog entries remain hidden and disabled. No migration alters, deletes, or copies data from a Hexagon table.

Run on a machine with PowerShell 7, the backend Release DLLs, and database access:

```powershell
pwsh -NoProfile -File .\Migrations\Apply-FmsPreparation.ps1
pwsh -NoProfile -File .\Migrations\Apply-FmsPreparation.ps1 -Apply
```

The first command executes pending migrations in a transaction and rolls them back. The second commits them and records SHA-256 checksums. Re-running applied migrations is a no-op; editing an applied migration is rejected. Database settings come from the backend configuration plus `FmsSettings__DbPassword` or the ignored `appsettings.Local.json`. Do not commit database passwords.

Operational data remains draft-only. The new map editor uses the Astha company/site seed; confirm that mapping before using it for another company. No table here is a dispatch authority. Assignment plans, map drafts, and commands are not sent to Hexagon.

## Cabin communication

This section describes the pending migration 004 and matching backend/Unity source. Check the migration ledger, deployed EXE, `/api/v1/comms/status`, and real devices independently before enabling it.

The new communication tables are `tbl_m_cabin_access_astha` and `tbl_t_cabin_message_astha`. They hold unit pairing-token hashes and text messages for the configured site. Hexagon is read only and used solely to verify that a paired unit exists. Live voice uses a WebSocket PCM stream and is not stored in these tables. Cabin-to-control and control-to-cabin audio are implemented in source, but require deployment and real-device validation before operational use.

The service remains disabled until the server has `CabinComms__Enabled=true` and a unique random `CabinComms__DispatcherKey` of at least 32 characters. Supply both only as server environment variables or ignored local settings. Do not put the key in Unity assets or a mobile build. The dispatcher enters the key in the Unity session, selects a unit, and obtains a pairing code. The cabin enters that code for the matching selected unit. Pairing again rotates and revokes the previous code. Unit codes remain only in mobile process memory, so switching menus does not require re-entry but restarting the app does. Keep port 8000 restricted behind the existing Cloudflare Access/VPN. A shared dispatcher key is not per-user identity and is unsuitable for an untrusted multi-company public deployment.

`GET /api/v1/comms/status` reports the feature flag. `POST /api/v1/comms/pair` and `GET/POST /api/v1/comms/messages` use `X-FMS-Dispatcher-Key` or `X-FMS-Unit-Key` as appropriate. `POST /api/v1/comms/live-ticket` issues a short-lived ticket for `GET /api/v1/comms/live` WebSocket audio. There is no `/comms/voice/...` recorded-audio route. No simulated message is shown as delivered. Deploy this backend version and run migration 004 on the server database before enabling the feature; status alone does not verify pairing or two-way device audio.

## Map draft editor

The Unity FMS panel opens a map editor for roads, loading/front/disposal/stockpile areas, and text labels. Road lines, rectangle/circle/polygon areas, and point labels are saved only in `tbl_m_map_draft_astha`. They are not merged into the live road graph and cannot affect routing or dispatch. All coordinates are UTM EPSG:32650. The source release notes mention road categories, map boundary editing, and selecting locations/call points when creating roads; they do **not** specify this editor's exact controls or shape palette. Obtain the actual ManagerPro map-editing guide before claiming UI parity.

`GET /api/v1/fms/map-drafts` reads drafts for the configured site. POST/PUT/DELETE require both `FmsMapEditor:Enabled=true` and a 32-character-or-longer `FmsMapEditor:Key`, supplied per request as `X-FMS-Editor-Key`. The key is entered in the Unity editor for the current session and is not saved in the project. Configure the key through a server environment variable or ignored `appsettings.Local.json`, not `appsettings.json`. Keep the endpoint behind Cloudflare Access/VPN and restrict direct port 8000 access; an editor key is an additional control, not user identity or a multi-tenant authorization system. The default is read-only.

`GET /api/v1/fms/map-drafts/{id}` reads one current draft (404 when absent or outside the configured site). POST creates, PUT replaces a draft only when `updated_at` matches its current revision, and DELETE archives only when the `revision` query parameter matches. A duplicate code or stale revision returns 409; invalid shape, width, coordinates, or metadata returns 400; unavailable editor access returns 403. A successful DELETE returns 204. The server rejects zero-length roads, collapsed rectangles, and degenerate polygons before writing. Codes remain reserved after archival because the table has a site-scoped unique constraint.

The OP Pro 4.0 release notes section 5.3.5 mentions editing boundary points and selecting locations/call points while creating roads. This draft API can persist edited point arrays, but the current Unity editor uses Undo/re-pick rather than drag-to-edit and does not auto-fill road endpoints from live Hexagon features. Sections 4.2.7-4.2.8 describe misroutes by road segment and low-priority road categories; Astha drafts are deliberately excluded from routing and misroute calculations until a separate review/publish workflow is designed. Do not describe these draft endpoints as ManagerPro feature parity.

## Table groups

| Group | New tables | Purpose |
| --- | --- | --- |
| Tenant and catalog | `tbl_m_company_astha`, `tbl_m_site_astha`, `tbl_m_fms_menu_astha`, `tbl_m_fms_setting_astha` | Company/site scope and future hidden menu registry. |
| Geometry and roads | `tbl_m_road_category_astha`, `tbl_m_road_node_astha`, `tbl_m_road_segment_astha`, `tbl_m_road_restriction_astha`, `tbl_m_boundary_astha`, `tbl_m_operational_location_astha`, `tbl_m_map_draft_astha` | Application-owned GIS, isolated from Hexagon roads/locations. Draft shapes have site scope, UTM geometry, editable points, and revision timestamps. Road width is separate from length. |
| Fleet and shifts | `tbl_m_equipment_config_astha`, `tbl_m_shift_astha`, `tbl_m_mtc_target_astha`, `tbl_t_assignment_plan_astha`, `tbl_t_equipment_activity_astha` | Future fleet configuration and read/event models; assignment plans are drafts. |
| Production | `tbl_t_haul_event_astha`, `tbl_t_material_movement_astha` | Source record IDs, payload, material, and `extra_load` preserved for reconciliation. |
| Equipment/events | `tbl_t_refuel_astha`, `tbl_t_sensor_observation_astha`, `tbl_t_misroute_astha`, `tbl_t_alert_astha` | Only populate from verified source records. |
| Governance | `tbl_t_fms_command_astha`, `tbl_t_fms_audit_astha`, `tbl_m_fms_schema_migration_astha` | Future command and audit design; no command endpoint enabled. |

The local reference is `D:\MineOperate_OP_Pro_4_0_Internal_Release_Notes_A4_en_v1.1.pdf`. It is release notes, not a complete ManagerPro menu specification. The catalog covers functions explicitly mentioned there; unlisted proprietary menus cannot be inferred from it. See `../../Virexa v.2/Design/FMS_READINESS.md` for capability gates.
