using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace ProposalEval.Services;

public sealed class BlobStorage : IFileStore
{
    private const string FileNameMetadata = "originalfilename";
    private readonly BlobServiceClient _blobs;

    public BlobStorage(IConfiguration configuration)
    {
        var connection = Environment.GetEnvironmentVariable("STORAGE_CONNECTIONSTRING")
            ?? configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set STORAGE_CONNECTIONSTRING or AzureStorage:ConnectionString.");

        _blobs = new BlobServiceClient(connection);
    }

    public async Task<string> SaveAsync(string container, string folder, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        var blobContainer = await GetContainerAsync(container, cancellationToken);
        var key = BlobNaming.BlobPath(folder, fileName, contentType);
        var blob = blobContainer.GetBlobClient(key);
        var type = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        var name = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(key) : BlobNaming.OriginalName(fileName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = type },
            Metadata = new Dictionary<string, string>
            {
                [FileNameMetadata] = Convert.ToBase64String(Encoding.UTF8.GetBytes(name))
            }
        }, cancellationToken);

        return key;
    }

    public async Task<StoredFile?> GetAsync(string container, string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return null;

        var blob = (await GetContainerAsync(container, cancellationToken)).GetBlobClient(storageKey);
        try
        {
            var download = await blob.DownloadContentAsync(cancellationToken);
            var details = download.Value.Details;
            var type = string.IsNullOrWhiteSpace(details.ContentType) ? "application/octet-stream" : details.ContentType;
            return new StoredFile(ReadFileName(details.Metadata, Path.GetFileName(storageKey)), type, download.Value.Content.ToArray());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string container, string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return;

        var blob = (await GetContainerAsync(container, cancellationToken)).GetBlobClient(storageKey);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task<BlobContainerClient> GetContainerAsync(string container, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(container))
            throw new ArgumentException("Container name is required.", nameof(container));

        var name = container.Trim().ToLowerInvariant();
        var client = _blobs.GetBlobContainerClient(name);
        await client.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        return client;
    }

    private static string ReadFileName(IDictionary<string, string> metadata, string fallback)
    {
        if (!metadata.TryGetValue(FileNameMetadata, out var encoded) || string.IsNullOrWhiteSpace(encoded))
            return fallback;

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return encoded;
        }
    }
}
