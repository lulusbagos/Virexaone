# FMS roadmap and database boundaries

This is a planning index, not a measured completion percentage or a live database inventory. Use [the system blueprint](Design/SYSTEM_BLUEPRINT.md) for architecture and [FMS readiness](Design/FMS_READINESS.md) for capability gates.

## Data ownership

| Source | Role | Write policy |
| --- | --- | --- |
| Hexagon tables in `DB_FMS` | Existing equipment, GPS, assignments, roads, locations, and hauls | Read-only from Virexa One |
| `_astha` tables | Application-owned catalog, map drafts, pairing, and messages | Validated backend workflows only |
| Unity and Flutter | Presentation and operator interaction | No direct database access |

The backend currently serves one configured site database. Company/site rows do not alone provide multi-tenant isolation. Table and fleet counts must be queried at the time of use; historical screenshots are not acceptance evidence.

The current source/deployment matrix is in [the blueprint](Design/SYSTEM_BLUEPRINT.md). The migration ledger, rather than a document count, determines what schema has actually been applied to a given database. Migration 004 and its backend/Unity integration remain local pending work until committed and deployed.

## Delivery stages

1. Audit real source keys, timestamps, GPS quality, assignments, location geometry, activities, and haul semantics. Capture redacted fixtures.
2. Expose freshness and provenance consistently across backend, Unity, MTC, and cabin. Keep straight-line distance distinct from road distance.
3. Review and validate Astha map drafts, then add explicit publish/versioning before they can influence routing.
4. Validate text and live radio on deployed server and physical devices. Add individual actor identity, revocation, acknowledgements, audit, and site isolation before operational control.
5. Use a connected road graph and reconciled haul events for any future route/production decisions. Never infer trips or payload from GPS motion alone.

## Acceptance gates

- Each displayed value has a source, timestamp, unit, and missing/stale state.
- No synthetic GPS point or road route is presented as observed data.
- A stopped unit's GPS heading is not assumed to be a current physical compass orientation.
- Hexagon schema and data remain untouched by application migrations.
- HTTP 200 is not proof of end-to-end device communication or an enabled workflow.

Historical versions with fixed counts and progress percentages remain in Git history; those were not verified live metrics.
