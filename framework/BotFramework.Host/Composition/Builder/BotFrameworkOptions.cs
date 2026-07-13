// ─────────────────────────────────────────────────────────────────────────────
// BotFrameworkOptions — framework-level options every distribution binds.
// Per-module options are bound separately by each IModule via BindOptions.
// ─────────────────────────────────────────────────────────────────────────────

namespace BotFramework.Host.Composition.Builder;

public sealed class BotFrameworkOptions
{
    public const string SectionName = "Bot";

    /// <summary>
    /// Enables Telegram ingress and delivery hosted services. Backend-only
    /// processes disable this and expose application requests through another
    /// transport such as gRPC.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Telegram bot token. Required.
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// True when running behind a Telegram webhook (updates arrive via HTTP POST
    /// at /{Token}). False for dev polling.
    /// </summary>
    public bool IsProduction { get; set; }

    /// <summary>
    /// Kestrel port for webhook mode. Ignored in polling mode.
    /// </summary>
    public int WebhookPort { get; set; } = 3000;

    /// <summary>
    /// Public HTTPS base URL used to register Telegram webhook in production
    /// mode (without trailing slash), e.g. https://bot.example.com.
    /// </summary>
    public string WebhookBaseUrl { get; set; } = "";

    /// <summary>
    /// Secret for gating /admin/* pages. Leave empty to disable the admin UI
    /// entirely (framework returns 503 when admin routes are hit).
    /// </summary>
    public string? AdminWebToken { get; set; }

    /// <summary>
    /// Default culture for ILocalizer when an update has no culture hint.
    /// </summary>
    public string DefaultCulture { get; set; } = "ru";

    /// <summary>
    /// Coins newly seeded users start with. Applied by EconomicsService when
    /// EnsureUserAsync inserts a user row for the first time.
    /// </summary>
    public int StartingCoins { get; set; } = 100;

    /// <summary>
    /// Channel @username (with or without leading "@") used as the public
    /// broadcast target — e.g. the admin horse panel posts the race GIF here.
    /// Empty string disables broadcasting.
    /// </summary>
    public string TrustedChannel { get; set; } = "";

    /// <summary>
    /// Telegram user IDs that have full admin access (can mutate state).
    /// </summary>
    public IReadOnlyList<long> Admins { get; set; } = [];

    /// <summary>
    /// Telegram user IDs that have read-only admin access.
    /// </summary>
    public IReadOnlyList<long> ReadOnlyAdmins { get; set; } = [];

    /// <summary>
    /// Bot @username (with or without leading "@") — used by Telegram Login Widget.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Backward-compatible alias for Username to support existing configs.
    /// </summary>
    public string BotUsername
    {
        get => Username;
        set => Username = value;
    }
}
