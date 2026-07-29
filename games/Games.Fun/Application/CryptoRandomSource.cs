using System.Security.Cryptography;

namespace Games.Fun.Application;

public sealed class CryptoRandomSource : IRandomSource
{
    public int NextInt(int minInclusive, int maxExclusive) =>
        RandomNumberGenerator.GetInt32(minInclusive, maxExclusive);
}
