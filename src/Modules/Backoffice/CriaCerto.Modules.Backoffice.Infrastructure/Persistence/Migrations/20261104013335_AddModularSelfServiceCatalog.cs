using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModularSelfServiceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeadTierCatalogs",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MinHeads = table.Column<int>(type: "int", nullable: false),
                    MaxHeads = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadTierCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleCatalogs",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsCore = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleTierPrices",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeadTierCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualPriceMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StripePriceIdMonthly = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    StripePriceIdAnnual = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleTierPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleTierPrices_HeadTierCatalogs_HeadTierCatalogId",
                        column: x => x.HeadTierCatalogId,
                        principalSchema: "backoffice",
                        principalTable: "HeadTierCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModuleTierPrices_ModuleCatalogs_ModuleCatalogId",
                        column: x => x.ModuleCatalogId,
                        principalSchema: "backoffice",
                        principalTable: "ModuleCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeadTierCatalogs_SortOrder",
                schema: "backoffice",
                table: "HeadTierCatalogs",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_HeadTierCatalogs_TierCode",
                schema: "backoffice",
                table: "HeadTierCatalogs",
                column: "TierCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleCatalogs_ModuleKey",
                schema: "backoffice",
                table: "ModuleCatalogs",
                column: "ModuleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleCatalogs_SortOrder",
                schema: "backoffice",
                table: "ModuleCatalogs",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleTierPrices_HeadTierCatalogId",
                schema: "backoffice",
                table: "ModuleTierPrices",
                column: "HeadTierCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleTierPrices_ModuleCatalogId_HeadTierCatalogId_Status",
                schema: "backoffice",
                table: "ModuleTierPrices",
                columns: new[] { "ModuleCatalogId", "HeadTierCatalogId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleTierPrices_ModuleCatalogId_HeadTierCatalogId_VersionNumber",
                schema: "backoffice",
                table: "ModuleTierPrices",
                columns: new[] { "ModuleCatalogId", "HeadTierCatalogId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleTierPrices",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "HeadTierCatalogs",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "ModuleCatalogs",
                schema: "backoffice");
        }
    }
}
