namespace ProposalEval.Services;

public sealed record StoredFile(string FileName, string ContentType, byte[] Content);

public static class BlobContainers
{
    public const string ProposalEvals = "proposalevals";
}

public static class BlobFolders
{
    public static string Rfq(int rfqId) => $"rfq-{rfqId}";

    public static string Vendor(string name, string? projectId)
    {
        var vendor = BlobNaming.Segment(name);
        var project = string.IsNullOrWhiteSpace(projectId) ? "_" : BlobNaming.Segment(projectId);
        return $"{vendor}/{project}";
    }
}

public static class BlobNaming
{
    public static string Segment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "_";

        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', '#', '?', '%']).ToHashSet();
        var cleaned = new string(value.Trim().Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '-' : c).ToArray())
            .Trim('-', '.');
        return string.IsNullOrWhiteSpace(cleaned) ? "_" : cleaned;
    }

    public static string OriginalName(string? fileName)
    {
        var value = (fileName ?? "").Trim().Replace('\\', '/');
        if (value.Length == 0)
            return "file";

        var slash = value.LastIndexOf('/');
        var name = slash >= 0 ? value[(slash + 1)..] : value;
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }

    public static string PublicUrl(string storageKey)
    {
        var path = string.Join('/', (storageKey ?? "")
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        return "/files/" + path;
    }

    public static string FileName(string fileName, string contentType)
    {
        var name = OriginalName(fileName);
        var stem = Segment(Path.GetFileNameWithoutExtension(name));
        var ext = Path.GetExtension(name);
        if (string.IsNullOrEmpty(ext))
            ext = ExtensionFromContentType(contentType.Split(';')[0].Trim());
        return stem + ext;
    }

    public static string BlobPath(string folder, string fileName, string contentType)
    {
        var name = FileName(fileName, contentType);
        if (string.IsNullOrWhiteSpace(folder))
            return name;

        var prefix = string.Join('/', folder.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Segment));
        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}/{name}";
    }

    private static string ExtensionFromContentType(string contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "application/msword" => ".doc",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "text/plain" => ".txt",
        "text/markdown" => ".md",
        "application/vnd.ms-excel" => ".xls",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        "text/html" => ".html",
        "application/xhtml+xml" => ".html",
        _ => ""
    };
}

public interface IFileStore
{
    Task<string> SaveAsync(string container, string folder, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);
    Task<StoredFile?> GetAsync(string container, string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string container, string storageKey, CancellationToken cancellationToken = default);
}
