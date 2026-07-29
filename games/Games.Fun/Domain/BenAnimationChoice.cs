using System.Runtime.InteropServices;

namespace Games.Fun.Domain;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct BenAnimationChoice(BenAnimationGroup Group, int Index);
