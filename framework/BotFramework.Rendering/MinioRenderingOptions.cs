namespace BotFramework.Rendering;

public sealed class MinioRenderingOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Bucket { get; set; } = "casinoshiz-media";
    public bool Secure { get; set; }
}