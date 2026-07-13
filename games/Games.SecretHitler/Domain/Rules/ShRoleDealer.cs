using System.Security.Cryptography;

namespace Games.SecretHitler.Domain.Rules;

public static class ShRoleDealer
{
    public const int MinPlayers = 5;
    public const int MaxPlayers = 10;

    public static ShRole[] BuildRoleDeck(int playerCount)
    {
        var (liberals, fascists) = playerCount switch
        {
            5 => (3, 1),
            6 => (4, 1),
            7 => (4, 2),
            8 => (5, 2),
            9 => (5, 3),
            10 => (6, 3),
            _ => throw new ArgumentOutOfRangeException(nameof(playerCount), $"Secret Hitler supports {MinPlayers}-{MaxPlayers} players"),
        };

        var roles = new List<ShRole>(playerCount);
        for (int i = 0; i < liberals; i++) roles.Add(ShRole.Liberal);
        for (int i = 0; i < fascists; i++) roles.Add(ShRole.Fascist);
        roles.Add(ShRole.Hitler);

        Shuffle(roles);
        return [.. roles];
    }

    public static void DealRoles(List<SecretHitlerPlayer> players)
    {
        var ordered = players.OrderBy(p => p.Position).ToList();
        var deck = BuildRoleDeck(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].Role = deck[i];
    }

    public static void DealRoles(List<SecretHitlerPlayer> players, IReadOnlyList<double> entropy)
    {
        var ordered = players.OrderBy(p => p.Position).ToList();
        var deck = BuildRoleDeckDeterministic(ordered.Count, entropy);
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].Role = deck[i];
    }

    private static ShRole[] BuildRoleDeckDeterministic(int playerCount, IReadOnlyList<double> entropy)
    {
        var deck = BuildUnshuffledRoleDeck(playerCount);
        Shuffle(deck, entropy);
        return [.. deck];
    }

    private static List<ShRole> BuildUnshuffledRoleDeck(int playerCount)
    {
        var (liberals, fascists) = playerCount switch
        {
            5 => (3, 1), 6 => (4, 1), 7 => (4, 2), 8 => (5, 2),
            9 => (5, 3), 10 => (6, 3),
            _ => throw new ArgumentOutOfRangeException(nameof(playerCount)),
        };
        var roles = new List<ShRole>(playerCount);
        for (var i = 0; i < liberals; i++) roles.Add(ShRole.Liberal);
        for (var i = 0; i < fascists; i++) roles.Add(ShRole.Fascist);
        roles.Add(ShRole.Hitler);
        return roles;
    }

    private static void Shuffle<T>(IList<T> list, IReadOnlyList<double> entropy)
    {
        if (entropy.Count < list.Count - 1)
            throw new ArgumentException("Not enough entropy values.", nameof(entropy));
        var entropyIndex = 0;
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Math.Min(i, (int)(entropy[entropyIndex++] * (i + 1)));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
