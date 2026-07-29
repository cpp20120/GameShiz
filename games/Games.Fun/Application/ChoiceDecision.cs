using Games.Fun.Domain;

namespace Games.Fun.Application;

public sealed record ChoiceDecision(
    IReadOnlyList<string> Options,
    int SelectedIndex,
    ChoiceError? Error)
{
    public string? Selected => Error is null ? Options[SelectedIndex] : null;
}
