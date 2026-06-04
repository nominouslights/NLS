namespace ShuttleApi.Infrastructure.Spaces;

public sealed class SpacesSettings
{
    public const string SectionName = "Spaces";

    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
}
