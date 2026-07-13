using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace BotFramework.Rendering;

internal sealed partial class MinioRenderArtifactStore : IRenderArtifactStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMinioClient client;
    private readonly MinioRenderingOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<MinioRenderArtifactStore> logger;
    private readonly SemaphoreSlim bucketGate = new(1, 1);
    private bool bucketReady;

    public MinioRenderArtifactStore(
        IOptions<RenderingOptions> options,
        TimeProvider timeProvider,
        ILogger<MinioRenderArtifactStore> logger)
    {
        this.options = options.Value.Minio;
        this.timeProvider = timeProvider;
        this.logger = logger;
        client = new MinioClient()
            .WithEndpoint(this.options.Endpoint)
            .WithCredentials(this.options.AccessKey, this.options.SecretKey)
            .WithSSL(this.options.Secure)
            .Build();
    }

    public async ValueTask<RenderedArtifact?> FindAsync(RenderKey key, CancellationToken ct)
    {
        await EnsureBucketAsync(ct).ConfigureAwait(false);
        try
        {
            var bytes = await DownloadAsync(key.ObjectName, ct).ConfigureAwait(false);
            return new RenderedArtifact(
                key,
                bytes,
                $"render.{key.Extension.TrimStart('.')}",
                timeProvider.GetUtcNow(),
                key.ObjectName,
                true);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public async ValueTask<RenderedArtifact> PutAsync(RenderKey key, RenderOutput output, CancellationToken ct)
    {
        await EnsureBucketAsync(ct).ConfigureAwait(false);
        await UploadAsync(key.ObjectName, key.ContentType, output.Content, ct).ConfigureAwait(false);
        return new RenderedArtifact(
            key,
            output.Content,
            output.FileName ?? $"render.{key.Extension.TrimStart('.')}",
            timeProvider.GetUtcNow(),
            key.ObjectName,
            false);
    }

    public async ValueTask RecordHistoryAsync(RenderHistoryEntry entry, CancellationToken ct)
    {
        await EnsureBucketAsync(ct).ConfigureAwait(false);
        var json = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
        await UploadAsync(HistoryObjectName(entry), "application/json", json, ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<RenderHistoryEntry> ListHistoryAsync(
        string gameId,
        string aggregateId,
        int take,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureBucketAsync(ct).ConfigureAwait(false);
        var prefix = $"history/{Segment(gameId)}/{Segment(aggregateId)}/";
        var names = new List<string>();
        var args = new ListObjectsArgs()
            .WithBucket(options.Bucket)
            .WithPrefix(prefix)
            .WithRecursive(true);
        await foreach (var item in client.ListObjectsEnumAsync(args, ct).ConfigureAwait(false))
            names.Add(item.Key);

        foreach (var name in names.OrderDescending().Take(Math.Max(0, take)))
        {
            ct.ThrowIfCancellationRequested();
            var json = await DownloadAsync(name, ct).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<RenderHistoryEntry>(json, JsonOptions);
            if (entry is not null) yield return entry;
        }
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (bucketReady) return;
        await bucketGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (bucketReady) return;
            var exists = await client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(options.Bucket), ct).ConfigureAwait(false);
            if (!exists)
            {
                await client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(options.Bucket), ct).ConfigureAwait(false);
                LogBucketCreated(options.Bucket);
            }
            bucketReady = true;
        }
        finally
        {
            bucketGate.Release();
        }
    }

    private async Task<byte[]> DownloadAsync(string objectName, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyToAsync(buffer, ct));
        await client.GetObjectAsync(args, ct).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private async Task UploadAsync(
        string objectName,
        string contentType,
        byte[] bytes,
        CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var args = new PutObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);
        await client.PutObjectAsync(args, ct).ConfigureAwait(false);
    }

    private static string HistoryObjectName(RenderHistoryEntry entry) =>
        $"history/{Segment(entry.GameId)}/{Segment(entry.AggregateId)}/{entry.CreatedAt:yyyyMMddTHHmmssfffffffZ}-{Segment(entry.MatchId)}.json";

    private static string Segment(string value) => string.Concat(value.Select(static ch =>
        char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-'));

    public void Dispose()
    {
        bucketGate.Dispose();
        client.Dispose();
    }

    [LoggerMessage(EventId = 6100, Level = LogLevel.Information, Message = "render.minio bucket created bucket={Bucket}")]
    private partial void LogBucketCreated(string bucket);
}
