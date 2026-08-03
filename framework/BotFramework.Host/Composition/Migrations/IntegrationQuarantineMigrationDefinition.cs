using BotFramework.Sdk.Modules.Migrations;

namespace BotFramework.Host.Composition.Migrations;

internal static class IntegrationQuarantineMigrationDefinition
{
    public static Migration Create() => new("041_integration_quarantine", """
        CREATE TABLE IF NOT EXISTS integration_message_quarantine (
            quarantine_id  BIGSERIAL    PRIMARY KEY,
            consumer_name  TEXT         NOT NULL,
            tenant_id      TEXT         NOT NULL DEFAULT '',
            scope_id       TEXT         NOT NULL DEFAULT '',
            message_id     TEXT         NOT NULL,
            topic          TEXT,
            message_type   TEXT,
            contract_type  TEXT,
            schema_version INTEGER,
            payload        JSONB,
            error_code     TEXT         NOT NULL,
            error_message  TEXT         NOT NULL,
            status         TEXT         NOT NULL DEFAULT 'open',
            first_seen_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
            last_seen_at   TIMESTAMPTZ  NOT NULL DEFAULT now(),
            replayed_at    TIMESTAMPTZ,
            UNIQUE (consumer_name, tenant_id, scope_id, message_id),
            CONSTRAINT ck_integration_quarantine_status
                CHECK (status IN ('open', 'replayed', 'ignored'))
        );

        CREATE INDEX IF NOT EXISTS ix_integration_quarantine_open
            ON integration_message_quarantine (consumer_name, status, last_seen_at DESC);
        """);
}
