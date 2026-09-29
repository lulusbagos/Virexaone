-- Application-owned cabin communications. Hexagon tables remain read-only.
CREATE TABLE tbl_m_cabin_access_astha (
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    unit_name varchar(40) NOT NULL,
    token_hash bytea NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (site_id, unit_name),
    CONSTRAINT ck_cabin_access_hash_astha CHECK (octet_length(token_hash) = 32)
);

CREATE TABLE tbl_t_cabin_message_astha (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id uuid NOT NULL REFERENCES tbl_m_site_astha(id),
    unit_name varchar(40) NOT NULL,
    sender_role varchar(16) NOT NULL,
    kind varchar(8) NOT NULL,
    body varchar(500) NOT NULL DEFAULT '',
    priority varchar(8) NOT NULL DEFAULT 'normal',
    audio_mime varchar(24),
    audio_bytes bytea,
    sent_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_cabin_message_sender_astha CHECK (sender_role IN ('dispatcher', 'cabin')),
    CONSTRAINT ck_cabin_message_kind_astha CHECK (kind IN ('text', 'voice')),
    CONSTRAINT ck_cabin_message_priority_astha CHECK (priority IN ('normal', 'urgent')),
    CONSTRAINT ck_cabin_message_audio_astha CHECK (
        (kind = 'text' AND audio_bytes IS NULL AND audio_mime IS NULL AND length(trim(body)) > 0)
        OR
        (kind = 'voice' AND sender_role = 'dispatcher' AND audio_bytes IS NOT NULL
         AND octet_length(audio_bytes) BETWEEN 100 AND 2000000
         AND audio_mime IN ('audio/wav', 'audio/webm'))
    )
);
CREATE INDEX ix_cabin_message_site_unit_astha
    ON tbl_t_cabin_message_astha(site_id, unit_name, sent_at DESC);
