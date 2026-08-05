using System.Runtime.InteropServices;

namespace BotFramework.Text;

/// <summary>
/// Maps a span in canonical text to the source span from which it was produced.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct TextSpanMapping(TextSpan Canonical, TextSpan Original);
