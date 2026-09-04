using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimHistoryRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateOfLoss = table.Column<DateOnly>(type: "date", nullable: false),
                    LossType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedDamage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PoliceReportNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsGlassOnly = table.Column<bool>(type: "bit", nullable: false),
                    FlaggedAnomaly = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimHistoryRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NamedInsured = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VehicleYear = table.Column<int>(type: "int", nullable: false),
                    VehicleMake = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VehicleModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Vin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FormVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LiabilityBiPerPerson = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityBiPerAccident = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityPd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MedPay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UmUimPerPerson = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UmUimPerAccident = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HasCollision = table.Column<bool>(type: "bit", nullable: false),
                    CollisionDeductible = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    HasComprehensive = table.Column<bool>(type: "bit", nullable: false),
                    ComprehensiveDeductible = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RentalReimbursementDaily = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Endorsements = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyDeclarations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimHistoryRecords_ClaimNumber",
                table: "ClaimHistoryRecords",
                column: "ClaimNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimHistoryRecords_PolicyNumber",
                table: "ClaimHistoryRecords",
                column: "PolicyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyDeclarations_PolicyNumber",
                table: "PolicyDeclarations",
                column: "PolicyNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimHistoryRecords");

            migrationBuilder.DropTable(
                name: "PolicyDeclarations");
        }
    }
}
