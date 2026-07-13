using System.Security.Cryptography;
using System.Text;

namespace Games.Horse.Infrastructure.Rendering.Generators;

public static class SpeedGenerator
{
    private const double BaseVelocity = 0.3;
    private const double MaxDeviation = 0.3 * BaseVelocity;
    private const double Distance = 100;

    public static int GenPlaces(int horseCount)
    {
        var bytes = new byte[1];
        RandomNumberGenerator.Fill(bytes);
        return bytes[0] % horseCount;
    }

    public static double[][] CreateSpeeds(int horsesCount, int winnerPosition)
    {
        var seed = RandomNumberGenerator.GetBytes(32);
        return CreateSpeeds(horsesCount, winnerPosition, seed);
    }

    public static double[][] CreateSpeeds(int horsesCount, int winnerPosition, string deterministicSeed) =>
        CreateSpeeds(horsesCount, winnerPosition, SHA256.HashData(Encoding.UTF8.GetBytes(deterministicSeed)));

    private static double[][] CreateSpeeds(int horsesCount, int winnerPosition, byte[] seed)
    {
        var random = new DeterministicRandom(seed);
        var serieses = new double[horsesCount][];
        for (var i = 0; i < horsesCount; i++)
            serieses[i] = CreateDataSeries(Distance, BaseVelocity, MaxDeviation, random);

        var actualWinnerId = 0;
        var minLen = serieses[0].Length;
        for (var i = 1; i < horsesCount; i++)
        {
            if (serieses[i].Length < minLen)
            {
                minLen = serieses[i].Length;
                actualWinnerId = i;
            }
        }

        (serieses[actualWinnerId], serieses[winnerPosition]) = (serieses[winnerPosition], serieses[actualWinnerId]);
        return serieses;
    }

    private static double[] CreateDataSeries(
        double distance,
        double velocity,
        double deviation,
        DeterministicRandom random)
    {
        var prevSpeed = velocity;
        var sum = 0.0;
        var series = new List<double> { velocity };

        while (sum < distance)
        {
            var raw = prevSpeed + ((random.NextDouble() - 0.5) * 2 * deviation);
            var clamped = Math.Min(velocity + deviation, Math.Max(velocity - deviation, raw));
            var newSpeed = Math.Round(clamped, 3, MidpointRounding.ToEven);
            sum += newSpeed;
            series.Add(newSpeed);
            prevSpeed = newSpeed;
        }

        return [.. series];
    }

    private sealed class DeterministicRandom(byte[] seed)
    {
        private long counter;

        public double NextDouble()
        {
            Span<byte> input = stackalloc byte[seed.Length + sizeof(long)];
            seed.CopyTo(input);
            BitConverter.TryWriteBytes(input[seed.Length..], counter++);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(input, hash);
            return BitConverter.ToUInt64(hash) / (double)ulong.MaxValue;
        }
    }
}
