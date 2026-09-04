using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjudicationCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdjudicationCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateOfLoss = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CoverageMatchResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnomalyFindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExclusionAnalysisResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecommendationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AdjusterComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjudicationCases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjudicationCases_ClaimNumber",
                table: "AdjudicationCases",
                column: "ClaimNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AdjudicationCases_PolicyNumber",
                table: "AdjudicationCases",
                column: "PolicyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AdjudicationCases_Status",
                table: "AdjudicationCases",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjudicationCases");
        }
    }
}
