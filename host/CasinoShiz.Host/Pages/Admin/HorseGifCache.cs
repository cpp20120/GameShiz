namespace CasinoShiz.Host.Pages.Admin;

// Holds the GIF bytes from the most recent RunRaceAsync call so the admin UI
// can embed it after a POST→redirect. HorseResultStore persists only the last
// frame (PNG), not the full animation. Keep the cache bounded: GIFs are large
// and this is only a convenience for inspecting recent races after a redirect.
public sealed class HorseGifCache
{
    private const int Capacity = 12;
    private readonly Dictionary<string, CacheEntry> _byDate = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public void Put(string raceDate, byte[] gif)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raceDate);
        ArgumentNullException.ThrowIfNull(gif);

        lock (_gate)
        {
            _byDate[raceDate] = new CacheEntry(gif, DateTimeOffset.UtcNow);
            while (_byDate.Count > Capacity)
            {
                var oldest = _byDate.MinBy(static pair => pair.Value.CreatedAt).Key;
                _byDate.Remove(oldest);
            }
        }
    }

    public byte[]? Get(string raceDate)
    {
        lock (_gate)
            return _byDate.GetValueOrDefault(raceDate)?.Gif;
    }

    public IReadOnlyList<string> Dates
    {
        get
        {
            lock (_gate)
                return _byDate
                    .OrderByDescending(static pair => pair.Value.CreatedAt)
                    .Select(static pair => pair.Key)
                    .ToArray();
        }
    }

    public Task<byte[]?> GetAsync(string raceDate, CancellationToken ct = default) =>
        Task.FromResult(Get(raceDate));

    private sealed record CacheEntry(byte[] Gif, DateTimeOffset CreatedAt);
}
