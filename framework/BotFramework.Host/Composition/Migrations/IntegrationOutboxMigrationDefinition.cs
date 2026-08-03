using BotFramework.Sdk.Modules.Migrations;

namespace BotFramework.Host.Composition.Migrations;

internal static class IntegrationOutboxMigrationDefinition
{
    public static Migration Create() => new("040_integration_outbox", """
        CREATE TABLE IF NOT EXISTS integration_outbox_messages (
            outbox_id      BIGSERIAL    PRIMARY KEY,
            producer_name  TEXT         NOT NULL,
            message_id     TEXT         NOT NULL,
            kind           TEXT         NOT NULL,
            topic          TEXT         NOT NULL,
            message_key    TEXT         NOT NULL,
            message_type   TEXT         NOT NULL,
            contract_type  TEXT         NOT NULL,
            schema_version INTEGER      NOT NULL,
            payload        JSONB        NOT NULL,
            envelope_json  JSONB        NOT NULL,
            occurred_at    TIMESTAMPTZ  NOT NULL,
            correlation_id TEXT         NOT NULL,
            causation_id   TEXT         NOT NULL,
            tenant_id      TEXT,
            scope_id       TEXT,
            player_id      TEXT,
            channel        TEXT         NOT NULL,
            status         TEXT         NOT NULL DEFAULT 'pending',
            attempts       INTEGER      NOT NULL DEFAULT 0,
            next_attempt_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            locked_until   TIMESTAMPTZ,
            locked_by      TEXT,
            last_error     TEXT,
            created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
            published_at   TIMESTAMPTZ,
            UNIQUE (producer_name, message_id),
            CONSTRAINT ck_integration_outbox_kind
                CHECK (kind IN ('event', 'command')),
            CONSTRAINT ck_integration_outbox_status
                CHECK (status IN ('pending', 'sending', 'sent'))
        );

        CREATE INDEX IF NOT EXISTS ix_integration_outbox_due
            ON integration_outbox_messages (producer_name, status, next_attempt_at, outbox_id);
        CREATE INDEX IF NOT EXISTS ix_integration_outbox_topic_key
            ON integration_outbox_messages (topic, message_key, outbox_id);
        """);
}
