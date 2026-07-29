namespace Games.Fun.Telegram;

public sealed class FunOptions
{
    public const string SectionName = "Games:fun";

    /// <summary>
    /// Two common Talking Ben animations. Values may be Telegram file ids,
    /// http(s) URLs, or paths relative to the application directory.
    /// </summary>
    public string[] BenPrimary { get; set; } = [];

    /// <summary>Three rare Talking Ben animations with 2% weight each.</summary>
    public string[] BenRare { get; set; } = [];
}
