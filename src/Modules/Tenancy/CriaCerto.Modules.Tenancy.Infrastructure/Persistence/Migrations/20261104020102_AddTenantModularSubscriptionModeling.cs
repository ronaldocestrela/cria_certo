using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantModularSubscriptionModeling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeadTierCode",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "TIER_500");

            migrationBuilder.CreateTable(
                name: "TenantSubscribedModules",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StripeSubscriptionItemId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MonthlyPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscribedModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSubscribedModules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_HeadTierCode",
                schema: "tenancy",
                table: "Tenants",
                column: "HeadTierCode");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscribedModules_TenantId_ModuleKey",
                schema: "tenancy",
                table: "TenantSubscribedModules",
                columns: new[] { "TenantId", "ModuleKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSubscribedModules",
                schema: "tenancy");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_HeadTierCode",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "HeadTierCode",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
