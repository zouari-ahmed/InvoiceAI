namespace InvoiceAI.Application;

public interface IPartyResolver
{
    Task<int?> ResolveAsync(string? name, string? address, string? taxId, CancellationToken ct);
}