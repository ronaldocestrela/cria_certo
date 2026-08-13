using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Growth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "growth");

            migrationBuilder.CreateTable(
                name: "LotMovements",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePaddockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationPaddockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeadCountMoved = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotMovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CurrentPaddockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HeadCount = table.Column<int>(type: "int", nullable: false),
                    AverageWeightKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PasturePaddocks",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AreaHectares = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxCapacityUA = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasturePaddocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "weighings",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalTagId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WeighingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CarcassYieldPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CalculatedArrobasTotal = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CalculatedAdgKgPerDay = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    CalculatedMonthlyArrobaGain = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    IsWeightLossWarning = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotMovements_TenantId_LotId",
                schema: "growth",
                table: "LotMovements",
                columns: new[] { "TenantId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_TenantId_Code_Status",
                schema: "growth",
                table: "Lots",
                columns: new[] { "TenantId", "Code", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PasturePaddocks_TenantId_Code",
                schema: "growth",
                table: "PasturePaddocks",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighings_TenantId_AnimalTagId_WeighingDate",
                schema: "growth",
                table: "weighings",
                columns: new[] { "TenantId", "AnimalTagId", "WeighingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_weighings_TenantId_LotId",
                schema: "growth",
                table: "weighings",
                columns: new[] { "TenantId", "LotId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotMovements",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "Lots",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PasturePaddocks",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "weighings",
                schema: "growth");
        }
    }
}
