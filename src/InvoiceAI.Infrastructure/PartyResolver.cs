using InvoiceAI.Application;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Infrastructure.Processing;

public class PartyResolver : IPartyResolver
{
    private readonly AppDbContext _db;

    public PartyResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int?> ResolveAsync(
        string? name,
        string? address,
        string? taxId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var cleanName = name.Trim();
        var cleanTaxId = string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim();
        var cleanAddress = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

        Party? existing = null;

        if (cleanTaxId is not null)
        {
            existing = await _db.Parties.FirstOrDefaultAsync(p => p.TaxId == cleanTaxId, ct);
        }

        existing ??= await _db.Parties.FirstOrDefaultAsync(p => p.Name == cleanName, ct);

        if (existing is not null)
        {
            existing.TaxId ??= cleanTaxId;
            existing.Address ??= cleanAddress;
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var created = new Party
        {
            Name = cleanName,
            Address = cleanAddress,
            TaxId = cleanTaxId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Parties.Add(created);
        await _db.SaveChangesAsync(ct);

        return created.Id;
    }
}
