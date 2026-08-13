using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Sanitary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sanitary");

            migrationBuilder.CreateTable(
                name: "treatment_records",
                schema: "sanitary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductCommercialName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Dosage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WithdrawalDays = table.Column<int>(type: "int", nullable: false),
                    ApplicationDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WithdrawalEndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedByVeterinarian = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treatment_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vaccination_campaigns",
                schema: "sanitary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vaccination_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vaccine_references",
                schema: "sanitary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DiseaseName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CommercialCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsMandatoryMAPA = table.Column<bool>(type: "bit", nullable: false),
                    TargetAudience = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RecommendedAgeMonths = table.Column<int>(type: "int", nullable: true),
                    BoosterIntervalDays = table.Column<int>(type: "int", nullable: true),
                    DefaultWithdrawalDays = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vaccine_references", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_references_Code",
                schema: "sanitary",
                table: "vaccine_references",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "treatment_records",
                schema: "sanitary");

            migrationBuilder.DropTable(
                name: "vaccination_campaigns",
                schema: "sanitary");

            migrationBuilder.DropTable(
                name: "vaccine_references",
                schema: "sanitary");
        }
    }
}
