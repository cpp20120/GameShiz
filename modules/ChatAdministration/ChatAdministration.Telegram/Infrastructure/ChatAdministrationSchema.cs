namespace ChatAdministration.Telegram.Infrastructure;

internal static class ChatAdministrationSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS chat_admin_chats (
            chat_id BIGINT PRIMARY KEY, chat_type TEXT NOT NULL, title TEXT NOT NULL,
            is_enabled BOOLEAN NOT NULL DEFAULT TRUE, settings JSONB NOT NULL DEFAULT '{}'::jsonb,
            bot_permissions JSONB,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        ALTER TABLE chat_admin_chats ADD COLUMN IF NOT EXISTS bot_permissions JSONB;
        CREATE TABLE IF NOT EXISTS chat_admin_members (
            chat_id BIGINT NOT NULL, user_id BIGINT NOT NULL, username TEXT, display_name TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'active', roles JSONB NOT NULL DEFAULT '[]'::jsonb,
            custom_roles JSONB NOT NULL DEFAULT '[]'::jsonb,
            explicit_permissions JSONB NOT NULL DEFAULT '[]'::jsonb, trust_level TEXT NOT NULL DEFAULT 'unknown',
            desired_restriction JSONB, observed_restriction JSONB,
            first_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(), last_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (chat_id, user_id)
        );
        ALTER TABLE chat_admin_members ADD COLUMN IF NOT EXISTS status TEXT NOT NULL DEFAULT 'active';
        ALTER TABLE chat_admin_members ADD COLUMN IF NOT EXISTS explicit_permissions JSONB NOT NULL DEFAULT '[]'::jsonb;
        ALTER TABLE chat_admin_members ADD COLUMN IF NOT EXISTS custom_roles JSONB NOT NULL DEFAULT '[]'::jsonb;
        ALTER TABLE chat_admin_members ADD COLUMN IF NOT EXISTS trust_level TEXT NOT NULL DEFAULT 'unknown';
        ALTER TABLE chat_admin_members ADD COLUMN IF NOT EXISTS joined_at TIMESTAMPTZ;
        ALTER TABLE chat_admin_members ADD COLUMN IF NOT EXISTS left_at TIMESTAMPTZ;
        CREATE TABLE IF NOT EXISTS chat_admin_warnings (
            warning_id UUID PRIMARY KEY, chat_id BIGINT NOT NULL, target_user_id BIGINT NOT NULL,
            actor_user_id BIGINT, reason TEXT, created_at TIMESTAMPTZ NOT NULL,
            expires_at TIMESTAMPTZ, is_active BOOLEAN NOT NULL DEFAULT TRUE,
            revocation_reason TEXT
        );
        ALTER TABLE chat_admin_warnings ADD COLUMN IF NOT EXISTS revocation_reason TEXT;
        CREATE INDEX IF NOT EXISTS ix_chat_admin_warnings_active
            ON chat_admin_warnings (chat_id, target_user_id, is_active, expires_at);
        CREATE TABLE IF NOT EXISTS chat_admin_verifications (
            session_id UUID PRIMARY KEY, chat_id BIGINT NOT NULL, user_id BIGINT NOT NULL,
            status TEXT NOT NULL, challenge_type TEXT NOT NULL, correct_answer TEXT NOT NULL,
            options JSONB NOT NULL, attempts INTEGER NOT NULL, maximum_attempts INTEGER NOT NULL,
            created_at TIMESTAMPTZ NOT NULL, expires_at TIMESTAMPTZ NOT NULL,
            challenge_message_id INTEGER, updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS ix_chat_admin_verifications_expiry
            ON chat_admin_verifications (status, expires_at);
        CREATE TABLE IF NOT EXISTS chat_admin_message_index (
            chat_id BIGINT NOT NULL, message_id INTEGER NOT NULL, author_user_id BIGINT NOT NULL,
            content_type TEXT NOT NULL, has_links BOOLEAN NOT NULL, sent_at TIMESTAMPTZ NOT NULL,
            content_hash TEXT, created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (chat_id, message_id)
        );
        CREATE INDEX IF NOT EXISTS ix_chat_admin_message_index_recent
            ON chat_admin_message_index (chat_id, sent_at DESC, message_id DESC);
        CREATE INDEX IF NOT EXISTS ix_chat_admin_message_index_author
            ON chat_admin_message_index (chat_id, author_user_id, sent_at DESC, message_id DESC);
        CREATE TABLE IF NOT EXISTS chat_admin_cases (
            case_id UUID PRIMARY KEY, chat_id BIGINT NOT NULL, target_user_id BIGINT NOT NULL,
            actor_user_id BIGINT, actor_type TEXT NOT NULL, action TEXT NOT NULL, reason TEXT,
            source_message_id INTEGER, source_rule_id TEXT,
            created_at TIMESTAMPTZ NOT NULL, expires_at TIMESTAMPTZ, status TEXT NOT NULL,
            correlation_id TEXT NOT NULL, updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS ix_chat_admin_cases_target ON chat_admin_cases (chat_id, target_user_id, created_at DESC);
        ALTER TABLE chat_admin_cases ADD COLUMN IF NOT EXISTS source_message_id INTEGER;
        ALTER TABLE chat_admin_cases ADD COLUMN IF NOT EXISTS source_rule_id TEXT;
        CREATE TABLE IF NOT EXISTS chat_admin_case_history (
            id BIGSERIAL PRIMARY KEY, case_id UUID NOT NULL, status TEXT NOT NULL, reason TEXT NOT NULL,
            created_at TIMESTAMPTZ NOT NULL
        );
        CREATE TABLE IF NOT EXISTS chat_admin_appeals (
            appeal_id UUID PRIMARY KEY, case_id UUID NOT NULL, chat_id BIGINT NOT NULL,
            author_user_id BIGINT NOT NULL, text TEXT NOT NULL, status TEXT NOT NULL,
            resolved_by BIGINT, resolution_comment TEXT, created_at TIMESTAMPTZ NOT NULL,
            resolved_at TIMESTAMPTZ
        );
        CREATE INDEX IF NOT EXISTS ix_chat_admin_appeals_chat_status
            ON chat_admin_appeals (chat_id, status, created_at DESC);
        CREATE TABLE IF NOT EXISTS chat_admin_domain_events (
            id BIGSERIAL PRIMARY KEY, aggregate_id UUID NOT NULL, event_type TEXT NOT NULL,
            payload JSONB NOT NULL, occurred_at TIMESTAMPTZ NOT NULL
        );
        CREATE TABLE IF NOT EXISTS chat_admin_commands (
            command_id TEXT PRIMARY KEY, idempotency_key TEXT NOT NULL UNIQUE, case_id UUID,
            response_text TEXT NOT NULL, created_at TIMESTAMPTZ NOT NULL
        );
        CREATE TABLE IF NOT EXISTS chat_admin_effect_outbox (
            effect_id UUID PRIMARY KEY, effect_type TEXT NOT NULL, payload JSONB NOT NULL,
            importance TEXT NOT NULL, case_id UUID, correlation_id TEXT NOT NULL, causation_id TEXT NOT NULL,
            idempotency_key TEXT NOT NULL UNIQUE, status TEXT NOT NULL, attempt INTEGER NOT NULL DEFAULT 0,
            maximum_attempts INTEGER NOT NULL DEFAULT 8, created_at TIMESTAMPTZ NOT NULL,
            not_before TIMESTAMPTZ NOT NULL, started_at TIMESTAMPTZ, completed_at TIMESTAMPTZ,
            locked_until TIMESTAMPTZ, dependencies JSONB NOT NULL DEFAULT '[]'::jsonb,
            last_error_code TEXT, last_error_message TEXT, updated_at TIMESTAMPTZ NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_chat_admin_effect_due ON chat_admin_effect_outbox (status, not_before, created_at);
        CREATE TABLE IF NOT EXISTS chat_admin_audit_events (
            id BIGSERIAL PRIMARY KEY, chat_id BIGINT NOT NULL, actor_user_id BIGINT, target_user_id BIGINT,
            action TEXT NOT NULL, correlation_id TEXT NOT NULL, case_id UUID, metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
            created_at TIMESTAMPTZ NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_chat_admin_audit_chat ON chat_admin_audit_events (chat_id, created_at DESC);
        CREATE TABLE IF NOT EXISTS chat_admin_settings_callbacks (
            token TEXT PRIMARY KEY, chat_id BIGINT NOT NULL, setting_key TEXT NOT NULL,
            setting_value TEXT NOT NULL, expires_at TIMESTAMPTZ NOT NULL, consumed_at TIMESTAMPTZ
        );
        CREATE INDEX IF NOT EXISTS ix_chat_admin_settings_callbacks_expiry
            ON chat_admin_settings_callbacks (expires_at, consumed_at);
        """;
}
