using InvoiceAI.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<SourceFile> SourceFiles => Set<SourceFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(i => i.InvoiceNumber).HasMaxLength(64);
            entity.Property(i => i.Currency).HasMaxLength(3).IsRequired();

            // Tunisian dinar has 3 decimal places (millimes)
            entity.Property(i => i.Subtotal).HasPrecision(18, 3);
            entity.Property(i => i.VatAmount).HasPrecision(18, 3);
            entity.Property(i => i.Total).HasPrecision(18, 3);

            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);

            entity.HasIndex(i => i.InvoiceNumber);
            entity.HasIndex(i => i.Status);

            entity.HasOne(i => i.Supplier)
                  .WithMany()
                  .HasForeignKey(i => i.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Customer)
                  .WithMany()
                  .HasForeignKey(i => i.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.SourceFile)
                  .WithMany()
                  .HasForeignKey(i => i.SourceFileId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(i => i.LineItems)
                  .WithOne(l => l.Invoice)
                  .HasForeignKey(l => l.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceLineItem>(entity =>
        {
            entity.Property(l => l.Description).HasMaxLength(512).IsRequired();
            entity.Property(l => l.Quantity).HasPrecision(18, 3);
            entity.Property(l => l.UnitPrice).HasPrecision(18, 3);
            entity.Property(l => l.VatRate).HasPrecision(5, 2);
            entity.Property(l => l.LineTotal).HasPrecision(18, 3);
        });

        modelBuilder.Entity<Party>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(256).IsRequired();
            entity.Property(p => p.Address).HasMaxLength(512);
            entity.Property(p => p.TaxId).HasMaxLength(64);
            entity.HasIndex(p => p.TaxId);
        });

        modelBuilder.Entity<SourceFile>(entity =>
        {
            entity.Property(f => f.StorageKey).HasMaxLength(128).IsRequired();
            entity.Property(f => f.OriginalFileName).HasMaxLength(256).IsRequired();
            entity.Property(f => f.ContentType).HasMaxLength(128).IsRequired();
            entity.HasIndex(f => f.StorageKey).IsUnique();
        });

        // ---- SEED DATA ----
        var seeded = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Party>().HasData(
            new Party { Id = 1, Name = "SOTUMETAL SARL", Address = "Zone Industrielle, Sfax", TaxId = "1234567/A/M/000", CreatedAt = seeded },
            new Party { Id = 2, Name = "Tunisie Telecom", Address = "Avenue Mohamed V, Tunis", TaxId = "0001234/B/N/000", CreatedAt = seeded },
            new Party { Id = 3, Name = "EDI Solutions", Address = "Les Berges du Lac, Tunis", TaxId = "7654321/C/M/000", CreatedAt = seeded }
        );

        modelBuilder.Entity<Invoice>().HasData(
            new Invoice
            {
                Id = 1,
                InvoiceNumber = "FA-2026-0142",
                IssueDate = new DateOnly(2026, 8, 12),
                DueDate = new DateOnly(2026, 9, 11),
                SupplierId = 1,
                CustomerId = 3,
                Subtotal = 1250.000m,
                VatAmount = 237.500m,
                Total = 1487.500m,
                Currency = "TND",
                Status = InvoiceStatus.PendingReview,
                CreatedAt = seeded
            },
            new Invoice
            {
                Id = 2,
                InvoiceNumber = "FA-2026-0143",
                IssueDate = new DateOnly(2026, 8, 18),
                SupplierId = 2,
                CustomerId = 3,
                Subtotal = 89.000m,
                VatAmount = 16.910m,
                Total = 105.910m,
                Currency = "TND",
                Status = InvoiceStatus.Reviewed,
                CreatedAt = seeded
            }
        );

        modelBuilder.Entity<InvoiceLineItem>().HasData(
            new InvoiceLineItem { Id = 1, InvoiceId = 1, Description = "Tole acier galvanise 2mm", Quantity = 50m, UnitPrice = 20.000m, VatRate = 19.00m, LineTotal = 1000.000m },
            new InvoiceLineItem { Id = 2, InvoiceId = 1, Description = "Decoupe laser sur mesure", Quantity = 5m, UnitPrice = 50.000m, VatRate = 19.00m, LineTotal = 250.000m },
            new InvoiceLineItem { Id = 3, InvoiceId = 2, Description = "Abonnement fibre Pro - Aout 2026", Quantity = 1m, UnitPrice = 89.000m, VatRate = 19.00m, LineTotal = 89.000m }
        );
    }
}