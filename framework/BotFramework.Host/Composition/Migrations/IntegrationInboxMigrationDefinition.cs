using BotFramework.Sdk.Modules.Migrations;

namespace BotFramework.Host.Composition.Migrations;

internal static class IntegrationInboxMigrationDefinition
{
    public static Migration Create() => new("039_integration_inbox", """
        CREATE TABLE IF NOT EXISTS integration_inbox_messages (
            consumer_name  TEXT        NOT NULL,
            tenant_id      TEXT        NOT NULL DEFAULT '',
            scope_id       TEXT        NOT NULL DEFAULT '',
            message_id     TEXT        NOT NULL,
            message_type   TEXT        NOT NULL,
            contract_type  TEXT        NOT NULL,
            schema_version INTEGER     NOT NULL,
            payload        JSONB       NOT NULL,
            occurred_at    TIMESTAMPTZ NOT NULL,
            correlation_id TEXT        NOT NULL,
            causation_id   TEXT        NOT NULL,
            player_id      TEXT,
            channel        TEXT        NOT NULL,
            status         TEXT        NOT NULL,
            result_type    TEXT,
            result_json    JSONB,
            received_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
            completed_at   TIMESTAMPTZ,
            PRIMARY KEY (consumer_name, tenant_id, scope_id, message_id),
            CONSTRAINT ck_integration_inbox_status
                CHECK (status IN ('processing', 'completed'))
        );

        CREATE INDEX IF NOT EXISTS ix_integration_inbox_due
            ON integration_inbox_messages (consumer_name, status, received_at DESC);
        """);
}
