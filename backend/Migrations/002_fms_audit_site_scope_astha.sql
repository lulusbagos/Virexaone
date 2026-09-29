-- Keep audit references within the same site. No existing Hexagon table is changed.
ALTER TABLE tbl_t_fms_command_astha
    ADD CONSTRAINT uq_fms_command_id_site_astha UNIQUE (id, site_id);

ALTER TABLE tbl_t_fms_audit_astha
    DROP CONSTRAINT tbl_t_fms_audit_astha_command_id_fkey;

ALTER TABLE tbl_t_fms_audit_astha
    ADD CONSTRAINT fk_fms_audit_command_site_astha
    FOREIGN KEY (command_id, site_id)
    REFERENCES tbl_t_fms_command_astha(id, site_id);
