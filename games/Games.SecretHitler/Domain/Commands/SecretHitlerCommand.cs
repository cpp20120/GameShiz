namespace Games.SecretHitler.Domain.Commands;

public abstract record SecretHitlerCommand
{
    public sealed record Usage : SecretHitlerCommand;
    public sealed record Unknown(string Action) : SecretHitlerCommand;
    public sealed record Create : SecretHitlerCommand;
    public sealed record Join(string Code) : SecretHitlerCommand;
    public sealed record JoinMissingCode : SecretHitlerCommand;
    public sealed record Start : SecretHitlerCommand;
    public sealed record Leave : SecretHitlerCommand;
    public sealed record Status : SecretHitlerCommand;

    public sealed record Nominate(int ChancellorPosition) : SecretHitlerCommand;
    public sealed record Vote(bool Ja) : SecretHitlerCommand;
    public sealed record PresidentDiscard(int Index) : SecretHitlerCommand;
    public sealed record ChancellorEnact(int Index) : SecretHitlerCommand;
}

