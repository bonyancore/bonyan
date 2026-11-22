using System.Collections.Concurrent;
using Construction.Platform.Core.Application.Files;

namespace Construction.Platform.Infrastructure.Files;

/// <summary>
/// In-memory object storage for tests and explicitly enabled Development runs. Not for production:
/// bytes are process-local and disappear on restart.
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    public InMemoryObjectStorage(TimeProvider? clock = null) =>
        _clock = clock ?? TimeProvider.System;

    public void Put(string storageKey, byte[] content) => _objects[storageKey] = content;

    public Task<PresignedUpload> CreateUploadAsync(
        string storageKey,
        string contentType,
        long maxBytes,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var expires = _clock.GetUtcNow() + lifetime;
        return Task.FromResult(new PresignedUpload(
            $"memory://upload/{storageKey}?expires={expires:O}",
            expires,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = contentType,
                ["Content-Length"] = maxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));
    }

    public Task<StoredObjectInfo?> HeadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.TryGetValue(storageKey, out var bytes)
            ? new StoredObjectInfo(bytes.LongLength, null, null)
            : null);

    public Task<PresignedDownload> CreateDownloadAsync(
        string storageKey,
        TimeSpan lifetime,
        string safeDownloadName,
        CancellationToken cancellationToken = default)
    {
        var expires = _clock.GetUtcNow() + lifetime;
        return Task.FromResult(new PresignedDownload(
            $"memory://download/{storageKey}?name={Uri.EscapeDataString(safeDownloadName)}&expires={expires:O}",
            expires));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        _objects.TryGetValue(storageKey, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
            : throw new FileNotFoundException("Object not found.", storageKey);
}
