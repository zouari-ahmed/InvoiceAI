using InvoiceAI.Application;

namespace InvoiceAI.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(string rootPath)
    {
        _root = rootPath;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct)
    {
        // Keep the extension for convenience, but never the caller's filename
        var extension = Path.GetExtension(originalFileName);
        if (extension.Length > 8) extension = string.Empty;

        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_root, storageKey);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, ct);

        return storageKey;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct)
    {
        Stream stream = File.OpenRead(ResolveSafePath(storageKey));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = ResolveSafePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    // Refuses any key that would escape the storage root — e.g. "../../appsettings.json"
    private string ResolveSafePath(string storageKey)
    {
        var candidate = Path.GetFullPath(Path.Combine(_root, storageKey));
        var root = Path.GetFullPath(_root);

        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid storage key.");

        return candidate;
    }
}