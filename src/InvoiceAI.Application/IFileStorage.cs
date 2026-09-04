namespace InvoiceAI.Application;

public interface IFileStorage
{
    /// <summary>Saves the stream and returns the generated storage key.</summary>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct);

    Task<Stream> OpenAsync(string storageKey, CancellationToken ct);

    Task DeleteAsync(string storageKey, CancellationToken ct);
}