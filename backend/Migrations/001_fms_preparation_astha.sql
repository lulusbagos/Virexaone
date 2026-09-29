-- Additive preparation only. No Hexagon table, existing Astha table, or live API is changed.
-- All operational tables start empty. Catalog entries are hidden and disabled.

CREATE TABLE tbl_m_fms_schema_migration_astha (
    version text PRIMARY KEY,
    checksum_sha256 text NOT NULL,
    applied_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE tbl_m_company_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    code text NOT NULL UNIQUE,
    display_name text NOT NULL,
    is_active boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_company_code_astha CHECK (code ~ '^[A-Za-z0-9_-]+$')
);

CREATE TABLE tbl_m_site_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id uuid NOT NULL REFERENCES tbl_m_company_astha(id),
    code text NOT NULL,
    display_name text NOT NULL,
    epsg integer NOT NULL DEFAULT 32650,
    timezone_name text NOT NULL DEFAULT 'Asia/Jakarta',
    is_active boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (company_id, code),
    CONSTRAINT ck_site_epsg_astha CHECK (epsg > 0)
);

CREATE TABLE tbl_m_fms_menu_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    parent_id uuid REFERENCES tbl_m_fms_menu_astha(id),
    code text NOT NULL UNIQUE,
    title text NOT NULL,
    module_code text NOT NULL,
    doc_section text,
    route_key text,
    display_order integer NOT NULL DEFAULT 0,
    readiness text NOT NULL DEFAULT 'planned',
    is_visible boolean NOT NULL DEFAULT false,
    is_enabled boolean NOT NULL DEFAULT false,
    notes text,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_menu_readiness_astha CHECK (readiness IN ('planned', 'existing_readonly'))
);
CREATE INDEX ix_fms_menu_parent_astha ON tbl_m_fms_menu_astha(parent_id, display_order);

CREATE TABLE tbl_m_fms_setting_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    setting_code text NOT NULL,
    setting_value jsonb NOT NULL DEFAULT '{}'::jsonb,
    doc_section text,
    is_enabled boolean NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, setting_code)
);

CREATE TABLE tbl_m_equipment_config_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_equipment_id bigint,
    source_system text NOT NULL DEFAULT 'hexagon',
    equipment_code text NOT NULL,
    category_code text,
    loading_radius_m numeric(9,2),
    model_scale numeric(9,4),
    configuration jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_enabled boolean NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, equipment_code),
    CONSTRAINT ck_equipment_radius_astha CHECK (loading_radius_m IS NULL OR loading_radius_m > 0),
    CONSTRAINT ck_equipment_scale_astha CHECK (model_scale IS NULL OR model_scale > 0)
);
CREATE UNIQUE INDEX ux_equipment_external_astha ON tbl_m_equipment_config_astha(site_id, source_system, external_equipment_id)
    WHERE external_equipment_id IS NOT NULL;

CREATE TABLE tbl_m_road_category_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    code text NOT NULL,
    name text NOT NULL,
    priority integer NOT NULL DEFAULT 0,
    default_speed_kmh numeric(7,2),
    is_active boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, code),
    UNIQUE (id, site_id),
    CONSTRAINT ck_road_category_speed_astha CHECK (default_speed_kmh IS NULL OR default_speed_kmh > 0)
);

CREATE TABLE tbl_m_road_node_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    code text NOT NULL,
    name text NOT NULL,
    position geometry(PointZ),
    epsg integer NOT NULL DEFAULT 32650,
    external_location_id bigint,
    status text NOT NULL DEFAULT 'draft',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, code),
    UNIQUE (id, site_id),
    CONSTRAINT ck_road_node_srid_astha CHECK (epsg > 0 AND (position IS NULL OR ST_SRID(position) = epsg)),
    CONSTRAINT ck_road_node_status_astha CHECK (status IN ('draft', 'approved', 'retired'))
);
CREATE INDEX ix_road_node_geom_astha ON tbl_m_road_node_astha USING gist(position);

CREATE TABLE tbl_m_road_segment_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    code text NOT NULL,
    start_node_id uuid NOT NULL,
    end_node_id uuid NOT NULL,
    category_id uuid,
    centerline geometry(LineStringZ),
    epsg integer NOT NULL DEFAULT 32650,
    length_m numeric(11,2),
    width_m numeric(8,2),
    speed_limit_kmh numeric(7,2),
    direction_code text NOT NULL DEFAULT 'both',
    priority integer NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'draft',
    external_road_id bigint,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, code),
    UNIQUE (id, site_id),
    FOREIGN KEY (start_node_id, site_id) REFERENCES tbl_m_road_node_astha(id, site_id),
    FOREIGN KEY (end_node_id, site_id) REFERENCES tbl_m_road_node_astha(id, site_id),
    FOREIGN KEY (category_id, site_id) REFERENCES tbl_m_road_category_astha(id, site_id),
    CONSTRAINT ck_road_segment_nodes_astha CHECK (start_node_id <> end_node_id),
    CONSTRAINT ck_road_segment_srid_astha CHECK (epsg > 0 AND (centerline IS NULL OR ST_SRID(centerline) = epsg)),
    CONSTRAINT ck_road_segment_length_astha CHECK (length_m IS NULL OR length_m > 0),
    CONSTRAINT ck_road_segment_width_astha CHECK (width_m IS NULL OR width_m > 0),
    CONSTRAINT ck_road_segment_speed_astha CHECK (speed_limit_kmh IS NULL OR speed_limit_kmh > 0),
    CONSTRAINT ck_road_segment_direction_astha CHECK (direction_code IN ('both', 'start_to_end', 'end_to_start')),
    CONSTRAINT ck_road_segment_status_astha CHECK (status IN ('draft', 'approved', 'closed', 'retired'))
);
CREATE INDEX ix_road_segment_geom_astha ON tbl_m_road_segment_astha USING gist(centerline);
CREATE INDEX ix_road_segment_nodes_astha ON tbl_m_road_segment_astha(site_id, start_node_id, end_node_id);

CREATE TABLE tbl_m_road_restriction_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    road_segment_id uuid NOT NULL,
    equipment_category text,
    direction_code text NOT NULL DEFAULT 'both',
    valid_from timestamptz,
    valid_to timestamptz,
    reason text NOT NULL,
    is_active boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (road_segment_id, site_id) REFERENCES tbl_m_road_segment_astha(id, site_id),
    CONSTRAINT ck_road_restriction_time_astha CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to > valid_from),
    CONSTRAINT ck_road_restriction_direction_astha CHECK (direction_code IN ('both', 'start_to_end', 'end_to_start'))
);
CREATE INDEX ix_road_restriction_segment_astha ON tbl_m_road_restriction_astha(site_id, road_segment_id, valid_from);

CREATE TABLE tbl_m_boundary_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    code text NOT NULL,
    name text NOT NULL,
    boundary_type text NOT NULL,
    shape geometry(MultiPolygon),
    epsg integer NOT NULL DEFAULT 32650,
    status text NOT NULL DEFAULT 'draft',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, code),
    CONSTRAINT ck_boundary_srid_astha CHECK (epsg > 0 AND (shape IS NULL OR ST_SRID(shape) = epsg)),
    CONSTRAINT ck_boundary_status_astha CHECK (status IN ('draft', 'approved', 'retired'))
);
CREATE INDEX ix_boundary_geom_astha ON tbl_m_boundary_astha USING gist(shape);

CREATE TABLE tbl_m_operational_location_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    code text NOT NULL,
    name text NOT NULL,
    location_type text NOT NULL,
    position geometry(PointZ),
    epsg integer NOT NULL DEFAULT 32650,
    external_location_id bigint,
    capacity_ton numeric(14,2),
    truck_capacity integer,
    status text NOT NULL DEFAULT 'draft',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, code),
    UNIQUE (id, site_id),
    CONSTRAINT ck_location_srid_astha CHECK (epsg > 0 AND (position IS NULL OR ST_SRID(position) = epsg)),
    CONSTRAINT ck_location_capacity_astha CHECK ((capacity_ton IS NULL OR capacity_ton >= 0) AND (truck_capacity IS NULL OR truck_capacity >= 0)),
    CONSTRAINT ck_location_status_astha CHECK (status IN ('draft', 'approved', 'closed', 'retired'))
);
CREATE INDEX ix_operational_location_geom_astha ON tbl_m_operational_location_astha USING gist(position);

CREATE TABLE tbl_m_shift_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    code text NOT NULL,
    shift_date date NOT NULL,
    starts_at timestamptz NOT NULL,
    ends_at timestamptz NOT NULL,
    status text NOT NULL DEFAULT 'planned',
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, code, shift_date),
    UNIQUE (id, site_id),
    CONSTRAINT ck_shift_time_astha CHECK (ends_at > starts_at),
    CONSTRAINT ck_shift_status_astha CHECK (status IN ('planned', 'open', 'closed'))
);

CREATE TABLE tbl_m_mtc_target_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_shovel_id bigint NOT NULL,
    desired_trucks integer,
    over_haulage_limit integer,
    is_enabled boolean NOT NULL DEFAULT false,
    doc_section text NOT NULL DEFAULT '4.2.6',
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, external_shovel_id),
    CONSTRAINT ck_mtc_target_astha CHECK ((desired_trucks IS NULL OR desired_trucks >= 0) AND (over_haulage_limit IS NULL OR over_haulage_limit >= 0))
);

CREATE TABLE tbl_t_assignment_plan_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_equipment_id bigint NOT NULL,
    external_shovel_id bigint,
    destination_location_id uuid,
    requested_at timestamptz NOT NULL DEFAULT now(),
    effective_at timestamptz,
    expires_at timestamptz,
    lock_to_shovel boolean,
    status text NOT NULL DEFAULT 'draft',
    reason text,
    source_record_id text,
    created_by text NOT NULL,
    FOREIGN KEY (destination_location_id, site_id) REFERENCES tbl_m_operational_location_astha(id, site_id),
    CONSTRAINT ck_assignment_plan_status_astha CHECK (status IN ('draft', 'proposed', 'rejected', 'archived')),
    CONSTRAINT ck_assignment_plan_time_astha CHECK (expires_at IS NULL OR effective_at IS NULL OR expires_at > effective_at)
);
CREATE INDEX ix_assignment_plan_unit_astha ON tbl_t_assignment_plan_astha(site_id, external_equipment_id, requested_at DESC);

CREATE TABLE tbl_t_equipment_activity_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_equipment_id bigint NOT NULL,
    raw_status_code text,
    raw_activity_code text,
    reason_code text,
    started_at timestamptz NOT NULL,
    ended_at timestamptz,
    source_record_id text,
    source_system text NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_activity_time_astha CHECK (ended_at IS NULL OR ended_at >= started_at)
);
CREATE INDEX ix_activity_equipment_time_astha ON tbl_t_equipment_activity_astha(site_id, external_equipment_id, started_at DESC);
CREATE UNIQUE INDEX ux_activity_source_astha ON tbl_t_equipment_activity_astha(site_id, source_system, source_record_id)
    WHERE source_record_id IS NOT NULL;

CREATE TABLE tbl_t_haul_event_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    shift_id uuid,
    external_truck_id bigint NOT NULL,
    external_shovel_id bigint,
    dump_location_id uuid,
    material_code text,
    loaded_at timestamptz,
    dumped_at timestamptz,
    payload_ton numeric(14,3),
    extra_load integer,
    source_system text NOT NULL,
    source_record_id text,
    received_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id, site_id),
    FOREIGN KEY (shift_id, site_id) REFERENCES tbl_m_shift_astha(id, site_id),
    FOREIGN KEY (dump_location_id, site_id) REFERENCES tbl_m_operational_location_astha(id, site_id),
    CONSTRAINT ck_haul_time_astha CHECK (dumped_at IS NULL OR loaded_at IS NULL OR dumped_at >= loaded_at),
    CONSTRAINT ck_haul_payload_astha CHECK (payload_ton IS NULL OR payload_ton >= 0),
    CONSTRAINT ck_haul_extra_load_astha CHECK (extra_load IS NULL OR extra_load >= 0)
);
CREATE INDEX ix_haul_shift_astha ON tbl_t_haul_event_astha(site_id, shift_id, loaded_at);
CREATE UNIQUE INDEX ux_haul_source_astha ON tbl_t_haul_event_astha(site_id, source_system, source_record_id)
    WHERE source_record_id IS NOT NULL;

CREATE TABLE tbl_t_material_movement_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    haul_event_id uuid,
    movement_type text NOT NULL,
    material_code text,
    tonnage_ton numeric(14,3),
    event_at timestamptz NOT NULL,
    extra_load integer,
    source_system text NOT NULL,
    source_record_id text,
    received_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (haul_event_id, site_id) REFERENCES tbl_t_haul_event_astha(id, site_id),
    CONSTRAINT ck_movement_type_astha CHECK (movement_type IN ('load', 'dump', 'adjustment')),
    CONSTRAINT ck_movement_tonnage_astha CHECK (tonnage_ton IS NULL OR tonnage_ton >= 0)
);
CREATE INDEX ix_movement_site_time_astha ON tbl_t_material_movement_astha(site_id, event_at DESC);
CREATE UNIQUE INDEX ux_movement_source_astha ON tbl_t_material_movement_astha(site_id, source_system, source_record_id)
    WHERE source_record_id IS NOT NULL;

CREATE TABLE tbl_t_refuel_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_equipment_id bigint NOT NULL,
    fuel_liters numeric(12,2) NOT NULL,
    occurred_at timestamptz NOT NULL,
    operator_ref text,
    source_system text NOT NULL,
    source_record_id text,
    notes text,
    recorded_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_refuel_liters_astha CHECK (fuel_liters > 0)
);
CREATE INDEX ix_refuel_equipment_time_astha ON tbl_t_refuel_astha(site_id, external_equipment_id, occurred_at DESC);

CREATE TABLE tbl_t_sensor_observation_astha (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_equipment_id bigint NOT NULL,
    sensor_code text NOT NULL,
    reading_value double precision NOT NULL,
    reading_unit text,
    quality_code text,
    observed_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    source_system text NOT NULL
);
CREATE INDEX ix_sensor_equipment_time_astha ON tbl_t_sensor_observation_astha(site_id, external_equipment_id, observed_at DESC);

CREATE TABLE tbl_t_misroute_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_equipment_id bigint NOT NULL,
    road_segment_id uuid,
    observed_at timestamptz NOT NULL,
    measured_distance_m numeric(12,2),
    event_status text NOT NULL DEFAULT 'unverified',
    source_system text NOT NULL,
    notes text,
    FOREIGN KEY (road_segment_id, site_id) REFERENCES tbl_m_road_segment_astha(id, site_id),
    CONSTRAINT ck_misroute_distance_astha CHECK (measured_distance_m IS NULL OR measured_distance_m >= 0),
    CONSTRAINT ck_misroute_status_astha CHECK (event_status IN ('unverified', 'confirmed', 'dismissed'))
);
CREATE INDEX ix_misroute_equipment_time_astha ON tbl_t_misroute_astha(site_id, external_equipment_id, observed_at DESC);

CREATE TABLE tbl_t_alert_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    external_equipment_id bigint,
    alert_code text NOT NULL,
    severity text NOT NULL,
    description text,
    occurred_at timestamptz NOT NULL,
    acknowledged_at timestamptz,
    acknowledged_by text,
    source_system text NOT NULL,
    CONSTRAINT ck_alert_severity_astha CHECK (severity IN ('info', 'warning', 'critical'))
);
CREATE INDEX ix_alert_site_time_astha ON tbl_t_alert_astha(site_id, occurred_at DESC);

CREATE TABLE tbl_t_fms_command_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    command_type text NOT NULL,
    target_equipment_id bigint,
    actor_ref text NOT NULL,
    idempotency_key text NOT NULL,
    expected_revision bigint,
    payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    status text NOT NULL DEFAULT 'draft',
    requested_at timestamptz NOT NULL DEFAULT now(),
    approved_at timestamptz,
    completed_at timestamptz,
    UNIQUE (site_id, idempotency_key),
    CONSTRAINT ck_command_status_astha CHECK (status IN ('draft', 'pending_approval', 'rejected', 'cancelled', 'completed'))
);

CREATE TABLE tbl_t_fms_audit_astha (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    command_id uuid REFERENCES tbl_t_fms_command_astha(id),
    actor_ref text NOT NULL,
    action_code text NOT NULL,
    entity_type text NOT NULL,
    entity_ref text NOT NULL,
    old_value jsonb,
    new_value jsonb,
    occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_fms_audit_site_time_astha ON tbl_t_fms_audit_astha(site_id, occurred_at DESC);

INSERT INTO tbl_m_fms_menu_astha (code, title, module_code, doc_section, display_order, notes) VALUES
    ('FLEET', 'Fleet & dispatch', 'FLEET', '4.1, 4.2, 5.3', 10, 'Catalog group; not wired to the existing UI.'),
    ('MINE_MAP', 'Mine map & roads', 'MINE_MAP', '4.2.7, 4.2.8, 5.3.5', 20, 'Catalog group; new geometry is separate from Hexagon.'),
    ('PRODUCTION', 'Production & shifts', 'PRODUCTION', '5.3.1, 5.3.3, 5.3.15', 30, 'Catalog group; no simulated totals.'),
    ('EQUIPMENT', 'Equipment & onboard', 'EQUIPMENT', '4.3, 4.4, 5.5', 40, 'Catalog group; LP/HP support requires verified sensors.'),
    ('ADMIN', 'Configuration & audit', 'ADMIN', '4.2.3, 4.3.2, 5.3.13', 50, 'Catalog group; write workflows remain disabled.')
ON CONFLICT (code) DO NOTHING;

WITH menu(code, parent_code, title, module_code, doc_section, display_order, readiness, notes) AS (
    VALUES
    ('FLEET_EQUIPMENT', 'FLEET', 'Equipment overview', 'FLEET', '4.2.4, 5.3.4', 10, 'planned', 'Search and status require validated source mapping.'),
    ('FLEET_MTC', 'FLEET', 'MTC fleet view', 'FLEET', '4.2.6, 5.3.2', 20, 'existing_readonly', 'Current Unity and web MTC remain read-only.'),
    ('FLEET_ASSIGNMENTS', 'FLEET', 'Assignment planning', 'FLEET', '4.1.2, 4.2.1, 4.2.2', 30, 'planned', 'No command writes to Hexagon.'),
    ('FLEET_STATUS_GANTT', 'FLEET', 'Status & activity Gantt', 'FLEET', '5.3.1, 5.3.7', 40, 'planned', 'Event source and shift boundaries must be validated.'),
    ('FLEET_SHOVEL_LOADS', 'FLEET', 'Shovel load count', 'FLEET', '5.3.11', 50, 'planned', 'Do not infer loads from GPS alone.'),
    ('FLEET_SHOVEL_LIMIT', 'FLEET', 'Shovel haulage limit', 'FLEET', '4.2.3', 60, 'planned', 'Site-specific target configuration.'),
    ('MAP_ROAD_NODES', 'MINE_MAP', 'Road points', 'MINE_MAP', '4.2.7, 5.3.5', 10, 'planned', 'App-owned points in tbl_m_road_node_astha.'),
    ('MAP_ROAD_SEGMENTS', 'MINE_MAP', 'Road segments', 'MINE_MAP', '4.2.7, 5.3.14', 20, 'planned', 'App-owned lines in tbl_m_road_segment_astha.'),
    ('MAP_ROAD_CATEGORIES', 'MINE_MAP', 'Road categories', 'MINE_MAP', '4.2.8', 30, 'planned', 'Separate from Hexagon category IDs.'),
    ('MAP_ROAD_RESTRICTIONS', 'MINE_MAP', 'Road restrictions', 'MINE_MAP', '5.3.13, 5.3.14', 40, 'planned', 'Routing engine not enabled.'),
    ('MAP_BOUNDARIES', 'MINE_MAP', 'Area boundaries', 'MINE_MAP', '5.3.5', 50, 'planned', 'Editing UI not enabled.'),
    ('MAP_LOCATIONS', 'MINE_MAP', 'Operational locations', 'MINE_MAP', '4.2.1, 5.3.9', 60, 'planned', 'Dumps, fronts, and call points remain distinct from road nodes.'),
    ('MAP_MISROUTES', 'MINE_MAP', 'Misroute review', 'MINE_MAP', '4.2.7', 70, 'planned', 'Requires validated road graph and trajectory.'),
    ('PROD_MATERIAL_MOVEMENTS', 'PRODUCTION', 'Material movements', 'PRODUCTION', '5.3.3', 10, 'planned', 'Preserve immutable source record identity and extra_load.'),
    ('PROD_SHIFT_LOADS', 'PRODUCTION', 'Shift loads', 'PRODUCTION', '5.3.3, 5.3.15', 20, 'planned', 'No timestamp-only join.'),
    ('PROD_SHIFT_DUMPS', 'PRODUCTION', 'Shift dumps', 'PRODUCTION', '5.3.3, 5.3.16', 30, 'planned', 'Shift reconciler not enabled.'),
    ('PROD_SHIFT_STATUS', 'PRODUCTION', 'Shift status', 'PRODUCTION', '5.3.7', 40, 'planned', 'Site-defined shift boundary required.'),
    ('PROD_RECONCILIATION', 'PRODUCTION', 'Reconciliation', 'PRODUCTION', '5.3.15', 50, 'planned', 'Exclude incomplete and future shifts only after source validation.'),
    ('PROD_SUMMARY', 'PRODUCTION', 'Production summary', 'PRODUCTION', '5.3.6', 60, 'planned', 'No simulated metric enabled by catalog.'),
    ('EQUIP_REFUEL', 'EQUIPMENT', 'Refuel', 'EQUIPMENT', '4.3.1.1', 10, 'planned', 'LP refuel records require source or authenticated entry.'),
    ('EQUIP_SENSORS', 'EQUIPMENT', 'Sensor observations', 'EQUIPMENT', '4.4.1.1, 5.5', 20, 'planned', 'HP sensor availability and calibration required.'),
    ('EQUIP_ASSET_HEALTH', 'EQUIPMENT', 'Asset health', 'EQUIPMENT', '2.4.1', 30, 'planned', 'Separate integration and licensing validation required.'),
    ('EQUIP_ALERTS', 'EQUIPMENT', 'Operational alerts', 'EQUIPMENT', '4.3.6, 5.6', 40, 'planned', 'No alert inference from absent sensors.'),
    ('EQUIP_GRADE_BOUNDARY', 'EQUIPMENT', 'Shovel grade boundary', 'EQUIPMENT', '5.5.2.1, 5.5.2.2', 50, 'planned', 'Requires verified grade model.'),
    ('ADMIN_ACCESSIBILITY', 'ADMIN', 'Accessibility', 'ADMIN', '4.3.2', 10, 'planned', 'Presentation-only settings; no operational data assumed.'),
    ('ADMIN_WIDGETS', 'ADMIN', 'Onboard widgets', 'ADMIN', '4.3.4', 20, 'planned', 'Presentation-only settings.'),
    ('ADMIN_FLEET_TARGETS', 'ADMIN', 'MTC desired fleet values', 'ADMIN', '4.2.6', 30, 'planned', 'Stored separately and disabled by default.'),
    ('ADMIN_EQUIPMENT_CONFIG', 'ADMIN', 'Equipment configuration', 'ADMIN', '4.3.5, 4.4', 40, 'planned', 'Loading radius and HP/LP settings require validation.'),
    ('ADMIN_COMMAND_AUDIT', 'ADMIN', 'Command audit', 'ADMIN', '5.3.2', 50, 'planned', 'No write endpoint until identity, approval, and idempotency are implemented.')
)
INSERT INTO tbl_m_fms_menu_astha
    (code, parent_id, title, module_code, doc_section, display_order, readiness, notes)
SELECT menu.code, parent.id, menu.title, menu.module_code, menu.doc_section,
       menu.display_order, menu.readiness, menu.notes
FROM menu
JOIN tbl_m_fms_menu_astha parent ON parent.code = menu.parent_code
ON CONFLICT (code) DO NOTHING;
