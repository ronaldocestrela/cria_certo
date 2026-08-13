using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Nutrition.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "nutrition");

            migrationBuilder.CreateTable(
                name: "DailyFeedBatches",
                schema: "nutrition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedRationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedingTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OfferedAsFedKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OfferedDryMatterKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TroughScore = table.Column<int>(type: "int", nullable: false),
                    HeadCountAtFeeding = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyFeedBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedRations",
                schema: "nutrition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RationType = table.Column<int>(type: "int", nullable: false),
                    DryMatterPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CalculatedCostPerKg = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedRations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PastureSupplementations",
                schema: "nutrition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaddockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedRationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DistributionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuantityKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HeadCount = table.Column<int>(type: "int", nullable: false),
                    CalculatedIntakeGramsPerHead = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastureSupplementations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiloStocks",
                schema: "nutrition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CurrentStockKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCostPerKg = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DryMatterPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MinimumThresholdKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastRestockedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiloStocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedRationItems",
                schema: "nutrition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedRationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    UnitCostPerKg = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedRationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedRationItems_FeedRations_FeedRationId",
                        column: x => x.FeedRationId,
                        principalSchema: "nutrition",
                        principalTable: "FeedRations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedRationItems_FeedRationId",
                schema: "nutrition",
                table: "FeedRationItems",
                column: "FeedRationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyFeedBatches",
                schema: "nutrition");

            migrationBuilder.DropTable(
                name: "FeedRationItems",
                schema: "nutrition");

            migrationBuilder.DropTable(
                name: "PastureSupplementations",
                schema: "nutrition");

            migrationBuilder.DropTable(
                name: "SiloStocks",
                schema: "nutrition");

            migrationBuilder.DropTable(
                name: "FeedRations",
                schema: "nutrition");
        }
    }
}
