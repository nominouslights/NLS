using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Infrastructure.Spaces;

internal sealed class SpacesFileStorageService : IFileStorageService, IAsyncDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;

    public SpacesFileStorageService(IOptions<SpacesSettings> options)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "DigitalOcean Spaces is not fully configured. Set these environment variables: " +
                "Spaces__AccessKey, Spaces__SecretKey, Spaces__BucketName, Spaces__Endpoint " +
                "(and optionally Spaces__Region, e.g. nyc3).");
        }

        var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint.TrimEnd('/'),
            ForcePathStyle = false
        };

        if (!string.IsNullOrWhiteSpace(settings.Region))
            config.AuthenticationRegion = settings.Region;

        _client = new AmazonS3Client(credentials, config);
        _bucketName = settings.BucketName;
    }

    public async Task<string> StoreAsync(
        string fileName,
        string contentType,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        var objectKey = $"{Guid.NewGuid()}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = new MemoryStream(data),
            ContentType = contentType
        };

        await _client.PutObjectAsync(request, cancellationToken);
        return objectKey;
    }

    public async Task<FileStorageResult> RetrieveAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        };

        using var response = await _client.GetObjectAsync(request, cancellationToken);
        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, cancellationToken);

        var fileName = Path.GetFileName(storageKey);
        return new FileStorageResult(fileName, response.Headers.ContentType, ms.ToArray());
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        };

        await _client.DeleteObjectAsync(request, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
