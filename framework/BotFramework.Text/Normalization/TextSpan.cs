using System.Runtime.InteropServices;

namespace BotFramework.Text;

/// <summary>
/// A half-open UTF-16 span in a text value.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct TextSpan
{
    public TextSpan(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (start > int.MaxValue - length)
            throw new ArgumentOutOfRangeException(nameof(length), "The span end exceeds Int32.MaxValue.");

        Start = start;
        Length = length;
    }

    public int Start { get; }
    public int Length { get; }
    public int End => Start + Length;
    public bool IsEmpty => Length == 0;

    public string Slice(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (End > value.Length)
            throw new ArgumentOutOfRangeException(nameof(value));

        return value.Substring(Start, Length);
    }

    public bool Contains(int position) => position >= Start && position < End;
    public bool Contains(TextSpan other) => other.Start >= Start && other.End <= End;
    public bool Intersects(TextSpan other) => Start < other.End && other.Start < End;

    public TextSpan Union(TextSpan other)
    {
        var start = Math.Min(Start, other.Start);
        var end = Math.Max(End, other.End);
        return new TextSpan(start, end - start);
    }
}
