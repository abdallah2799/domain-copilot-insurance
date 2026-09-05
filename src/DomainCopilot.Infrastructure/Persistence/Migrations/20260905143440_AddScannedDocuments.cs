using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScannedDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScannedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PageResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CombinedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallConfidencePercent = table.Column<double>(type: "float", nullable: true),
                    LowestPageConfidencePercent = table.Column<double>(type: "float", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannedDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScannedDocuments_ClaimNumber",
                table: "ScannedDocuments",
                column: "ClaimNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ScannedDocuments_ClaimNumber_ContentHash",
                table: "ScannedDocuments",
                columns: new[] { "ClaimNumber", "ContentHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScannedDocuments");
        }
    }
}
