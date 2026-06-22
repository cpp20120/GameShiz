namespace Games.SecretHitler.Domain.Configuration;

public sealed class SecretHitlerOptions
{
    public const string SectionName = "Games:sh";

    public int BuyIn { get; init; } = 50;
}
