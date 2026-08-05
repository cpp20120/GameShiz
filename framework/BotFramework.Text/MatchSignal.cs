using System.Runtime.InteropServices;

namespace BotFramework.Text;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct MatchSignal
{
    public MatchSignal(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A match signal name is required.", nameof(name));

        Name = name.Trim();
    }

    public string Name { get; }
    public override string ToString() => Name;
}
