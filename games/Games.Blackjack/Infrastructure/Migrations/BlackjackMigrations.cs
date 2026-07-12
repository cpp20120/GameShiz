
namespace Games.Blackjack.Infrastructure.Migrations;

public sealed class BlackjackMigrations : IModuleMigrations
{
    public string ModuleId => "blackjack";

    public IReadOnlyList<Migration> Migrations { get; } =
    [
        new Migration("001_initial", """
            CREATE TABLE blackjack_hands (
                user_id           BIGINT      PRIMARY KEY,
                chat_id           BIGINT      NOT NULL,
                bet               INTEGER     NOT NULL,
                player_cards      TEXT        NOT NULL,
                dealer_cards      TEXT        NOT NULL,
                deck_state        TEXT        NOT NULL,
                state_message_id  INTEGER     NULL,
                created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            CREATE INDEX ix_blackjack_hands_created ON blackjack_hands (created_at);
            """),

        new Migration("002_atomic_execution_state", """
            INSERT INTO game_aggregate_states (
                game_id, aggregate_id, state_type, version, state, updated_at)
            SELECT
                'blackjack',
                h.user_id::text,
                'Games.Blackjack.Application.Execution.BlackjackGameState',
                0,
                jsonb_build_object(
                    'revision', 0,
                    'status', 1,
                    'currentPlayerId', h.user_id,
                    'turnDeadline', h.created_at + interval '120 seconds',
                    'displayName', COALESCE(u.display_name, h.user_id::text),
                    'hand', jsonb_build_object(
                        'handId', 'blackjack:legacy:' || h.user_id::text || ':'
                            || CAST(EXTRACT(EPOCH FROM h.created_at) * 1000 AS bigint)::text,
                        'userId', h.user_id,
                        'chatId', h.chat_id,
                        'bet', h.bet,
                        'playerCards', to_jsonb(string_to_array(h.player_cards, ' ')),
                        'dealerCards', to_jsonb(string_to_array(h.dealer_cards, ' ')),
                        'deckState', h.deck_state,
                        'stateMessageId', h.state_message_id,
                        'createdAt', to_jsonb(h.created_at))) AS state,
                now()
            FROM blackjack_hands h
            LEFT JOIN users u
              ON u.telegram_user_id = h.user_id
             AND u.balance_scope_id = h.chat_id
            ON CONFLICT (game_id, aggregate_id) DO NOTHING;

            INSERT INTO game_schedule_outbox (
                command_id,
                effect_index,
                schedule_id,
                effect_kind,
                job_key,
                due_at,
                data)
            SELECT
                'blackjack:legacy:' || h.user_id::text || ':'
                    || CAST(EXTRACT(EPOCH FROM h.created_at) * 1000 AS bigint)::text || ':timeout',
                0,
                'blackjack:' || h.user_id::text || ':hand-timeout',
                'schedule',
                'atomic-game:Games.Blackjack:Games.Blackjack.Application.Execution.BlackjackTimeoutCommand',
                GREATEST(h.created_at + interval '120 seconds', now()),
                jsonb_build_object(
                    'atomic-command',
                    jsonb_build_object(
                        'userId', h.user_id,
                        'displayName', COALESCE(u.display_name, h.user_id::text),
                        'chatId', h.chat_id,
                        'handId', 'blackjack:legacy:' || h.user_id::text || ':'
                            || CAST(EXTRACT(EPOCH FROM h.created_at) * 1000 AS bigint)::text,
                        'commandId', 'blackjack:legacy:' || h.user_id::text || ':'
                            || CAST(EXTRACT(EPOCH FROM h.created_at) * 1000 AS bigint)::text || ':timeout')::text)
            FROM blackjack_hands h
            LEFT JOIN users u
              ON u.telegram_user_id = h.user_id
             AND u.balance_scope_id = h.chat_id
            ON CONFLICT (command_id, effect_index) DO NOTHING;
            """),
    ];
}
