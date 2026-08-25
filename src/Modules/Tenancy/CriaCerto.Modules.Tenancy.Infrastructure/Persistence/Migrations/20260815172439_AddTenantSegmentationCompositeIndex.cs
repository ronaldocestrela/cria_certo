using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSegmentationCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_CreatedAtUtc_Id",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ChurnRisk_CommercialRegion_CreatedAtUtc_Id",
                schema: "tenancy",
                table: "Tenants",
                columns: new[] { "ChurnRisk", "CommercialRegion", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_ChurnRisk_CommercialRegion_CreatedAtUtc_Id",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAtUtc_Id",
                schema: "tenancy",
                table: "Tenants",
                columns: new[] { "CreatedAtUtc", "Id" });
        }
    }
}
