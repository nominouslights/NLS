namespace ShuttleApi.Infrastructure.Spaces;

public sealed class SpacesSettings
{
    public const string SectionName = "Spaces";

    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;

    /// <summary>
    /// DigitalOcean Spaces endpoint, e.g. https://nyc3.digitaloceanspaces.com
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// Spaces region slug, e.g. nyc3. Used as the S3 authentication region.
    /// </summary>
    public string Region { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(BucketName)
        && !string.IsNullOrWhiteSpace(Endpoint);
}
