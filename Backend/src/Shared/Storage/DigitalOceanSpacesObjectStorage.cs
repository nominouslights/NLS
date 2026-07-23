using Amazon.S3;
using Amazon.S3.Model;

namespace NorthernLink.Shared.Storage;

/// <summary>
/// Object storage via DigitalOcean Spaces (S3-compatible API). Wires up the AWS SDK client
/// with the DO Spaces endpoint, credentials, and bucket name.
/// </summary>
internal sealed class DigitalOceanSpacesObjectStorage : IObjectStorage, IAsyncDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public DigitalOceanSpacesObjectStorage(string serviceUrl, string bucketName, string accessKey, string secretKey)
    {
        _bucketName = bucketName;
        var config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = false,
            SignatureVersion = "4",
        };
        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task PutAsync(string key, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
        };

        try
        {
            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload object to '{key}': {ex.Message}", ex);
        }
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_bucketName, key, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Object '{key}' not found in bucket '{_bucketName}'.");
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve object from '{key}': {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_bucketName, key, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete object at '{key}': {ex.Message}", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_s3Client is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        else
        {
            _s3Client?.Dispose();
        }
    }
}
