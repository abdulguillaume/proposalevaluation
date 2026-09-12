using System.Collections.Concurrent;

namespace ProposalEval.Services;

public sealed class InMemoryFileStore : IFileStore
{
    private readonly ConcurrentDictionary<string, StoredFile> _files = new();

    public async Task<string> SaveAsync(string container, string folder, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var key = BlobNaming.BlobPath(folder, fileName, contentType);
        _files[Key(container, key)] = new StoredFile(fileName, contentType, buffer.ToArray());
        return key;
    }

    public Task<StoredFile?> GetAsync(string container, string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(Key(container, storageKey), out var file) ? file : null);

    public Task DeleteAsync(string container, string storageKey, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(Key(container, storageKey), out _);
        return Task.CompletedTask;
    }

    private static string Key(string container, string storageKey) =>
        $"{(string.IsNullOrWhiteSpace(container) ? "_" : container)}:{storageKey}";
}
