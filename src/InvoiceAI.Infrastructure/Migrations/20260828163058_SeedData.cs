using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InvoiceAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Parties",
                columns: new[] { "Id", "Address", "CreatedAt", "Name", "TaxId" },
                values: new object[,]
                {
                    { 1, "Zone Industrielle, Sfax", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SOTUMETAL SARL", "1234567/A/M/000" },
                    { 2, "Avenue Mohamed V, Tunis", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tunisie Telecom", "0001234/B/N/000" },
                    { 3, "Les Berges du Lac, Tunis", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "EDI Solutions", "7654321/C/M/000" }
                });

            migrationBuilder.InsertData(
                table: "Invoices",
                columns: new[] { "Id", "CreatedAt", "Currency", "CustomerId", "DueDate", "InvoiceNumber", "IssueDate", "Status", "Subtotal", "SupplierId", "Total", "UpdatedAt", "VatAmount" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "TND", 3, new DateOnly(2026, 9, 11), "FA-2026-0142", new DateOnly(2026, 8, 12), "PendingReview", 1250.000m, 1, 1487.500m, null, 237.500m },
                    { 2, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "TND", 3, null, "FA-2026-0143", new DateOnly(2026, 8, 18), "Reviewed", 89.000m, 2, 105.910m, null, 16.910m }
                });

            migrationBuilder.InsertData(
                table: "InvoiceLineItems",
                columns: new[] { "Id", "Description", "InvoiceId", "LineTotal", "Quantity", "UnitPrice", "VatRate" },
                values: new object[,]
                {
                    { 1, "Tôle acier galvanisé 2mm", 1, 1000.000m, 50m, 20.000m, 19.00m },
                    { 2, "Découpe laser sur mesure", 1, 250.000m, 5m, 50.000m, 19.00m },
                    { 3, "Abonnement fibre Pro — Août 2026", 2, 89.000m, 1m, 89.000m, 19.00m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Invoices",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Invoices",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Parties",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Parties",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Parties",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
