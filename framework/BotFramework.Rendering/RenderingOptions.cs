namespace BotFramework.Rendering;

public sealed class RenderingOptions
{
    public const string SectionName = "Rendering";

    public bool Enabled { get; set; } = true;
    public int QueueCapacity { get; set; } = 64;
    public int MaxParallelism { get; set; }
    public long MaxArtifactBytes { get; set; } = 32 * 1024 * 1024;
    public MinioRenderingOptions Minio { get; set; } = new();

    internal int EffectiveParallelism => MaxParallelism > 0
        ? MaxParallelism
        : Math.Max(1, Environment.ProcessorCount / 2);
}