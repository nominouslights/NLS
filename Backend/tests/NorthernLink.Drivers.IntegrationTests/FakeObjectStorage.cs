using NorthernLink.Shared.Storage;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// In-memory <see cref="IObjectStorage"/> for tests — the store→SetImage→project→read
/// flow needs somewhere for bytes to live without a real S3-compatible backend.
/// </summary>
public sealed class FakeObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, (byte[] Bytes, string ContentType)> _objects = [];

    public IReadOnlyDictionary<string, (byte[] Bytes, string ContentType)> Objects => _objects;

    public async Task PutAsync(string key, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        _objects[key] = (buffer.ToArray(), contentType);
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default) =>
        _objects.TryGetValue(key, out var stored)
            ? Task.FromResult<Stream>(new MemoryStream(stored.Bytes, writable: false))
            : Task.FromException<Stream>(new KeyNotFoundException($"No object stored under key '{key}'."));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.Remove(key);
        return Task.CompletedTask;
    }
}
