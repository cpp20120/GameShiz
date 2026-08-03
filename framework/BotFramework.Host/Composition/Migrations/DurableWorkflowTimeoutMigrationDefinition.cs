using BotFramework.Sdk.Modules.Migrations;

namespace BotFramework.Host.Composition.Migrations;

internal static class DurableWorkflowTimeoutMigrationDefinition
{
    public static Migration Create() => new("042_durable_workflow_timeouts", """
        CREATE TABLE IF NOT EXISTS durable_workflow_timeouts (
            timeout_id      TEXT         PRIMARY KEY,
            workflow_id     TEXT         NOT NULL,
            command_id      TEXT         NOT NULL,
            command_type    TEXT         NOT NULL,
            operation       TEXT         NOT NULL,
            aggregate_id    TEXT,
            causation_id    TEXT,
            group_id        TEXT,
            command_json    JSONB        NOT NULL,
            due_at          TIMESTAMPTZ  NOT NULL,
            status          TEXT         NOT NULL DEFAULT 'pending',
            attempts        INTEGER      NOT NULL DEFAULT 0,
            max_attempts    INTEGER      NOT NULL DEFAULT 10,
            locked_until    TIMESTAMPTZ,
            locked_by       TEXT,
            last_error      TEXT,
            created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
            dispatched_at   TIMESTAMPTZ,
            CONSTRAINT ck_durable_workflow_timeout_status
                CHECK (status IN ('pending', 'sending', 'dispatched', 'cancelled', 'failed')),
            CONSTRAINT ck_durable_workflow_timeout_attempts
                CHECK (attempts >= 0 AND max_attempts > 0 AND attempts <= max_attempts)
        );

        CREATE INDEX IF NOT EXISTS ix_durable_workflow_timeouts_due
            ON durable_workflow_timeouts (status, due_at, timeout_id);
        CREATE INDEX IF NOT EXISTS ix_durable_workflow_timeouts_workflow
            ON durable_workflow_timeouts (workflow_id, created_at DESC);
        """);
}
