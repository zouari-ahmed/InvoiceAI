using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawExtractionJson",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceFileId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SourceFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceFiles", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "Tole acier galvanise 2mm");

            migrationBuilder.UpdateData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Decoupe laser sur mesure");

            migrationBuilder.UpdateData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Abonnement fibre Pro - Aout 2026");

            migrationBuilder.UpdateData(
                table: "Invoices",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "RawExtractionJson", "SourceFileId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Invoices",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "RawExtractionJson", "SourceFileId" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceFileId",
                table: "Invoices",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceFiles_StorageKey",
                table: "SourceFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_SourceFiles_SourceFileId",
                table: "Invoices",
                column: "SourceFileId",
                principalTable: "SourceFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_SourceFiles_SourceFileId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "SourceFiles");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SourceFileId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RawExtractionJson",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SourceFileId",
                table: "Invoices");

            migrationBuilder.UpdateData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "Tôle acier galvanisé 2mm");

            migrationBuilder.UpdateData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Découpe laser sur mesure");

            migrationBuilder.UpdateData(
                table: "InvoiceLineItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Abonnement fibre Pro — Août 2026");
        }
    }
}
