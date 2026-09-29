-- Application-owned map drafts only. No Hexagon object is modified.
INSERT INTO tbl_m_company_astha (code, display_name)
VALUES ('astha', 'Astha Virexa Technology')
ON CONFLICT (code) DO NOTHING;

INSERT INTO tbl_m_site_astha (company_id, code, display_name, epsg)
SELECT id, 'astha', 'Astha Mine', 32650
FROM tbl_m_company_astha WHERE code = 'astha'
ON CONFLICT (company_id, code) DO NOTHING;

CREATE TABLE tbl_m_map_draft_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    code text NOT NULL,
    name text NOT NULL,
    feature_type text NOT NULL,
    shape_kind text NOT NULL,
    points jsonb NOT NULL,
    shape geometry(Geometry, 32650) NOT NULL,
    width_m numeric(8,2),
    radius_m numeric(9,2),
    color_hex char(7) NOT NULL DEFAULT '#4FD0B4',
    status text NOT NULL DEFAULT 'draft',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (site_id, code),
    CONSTRAINT ck_map_draft_type_astha CHECK (feature_type IN
        ('road', 'loading', 'front', 'disposal', 'stockpile', 'note')),
    CONSTRAINT ck_map_draft_shape_astha CHECK (shape_kind IN
        ('line', 'rectangle', 'circle', 'polygon', 'text')),
    CONSTRAINT ck_map_draft_status_astha CHECK (status IN ('draft', 'retired')),
    CONSTRAINT ck_map_draft_width_astha CHECK (width_m IS NULL OR width_m > 0),
    CONSTRAINT ck_map_draft_radius_astha CHECK (radius_m IS NULL OR radius_m > 0),
    CONSTRAINT ck_map_draft_color_astha CHECK (color_hex ~ '^#[0-9A-Fa-f]{6}$'),
    CONSTRAINT ck_map_draft_points_astha CHECK (jsonb_typeof(points) = 'array')
);
CREATE INDEX ix_map_draft_site_astha ON tbl_m_map_draft_astha(site_id, feature_type, status);
CREATE INDEX ix_map_draft_shape_astha ON tbl_m_map_draft_astha USING gist(shape);
