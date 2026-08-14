using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantOperationalSegmentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChurnRisk",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommercialRegion",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductiveProfile",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SizeSegment",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OperationalTags",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantOperationalTags",
                schema: "tenancy",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantOperationalTags", x => new { x.TenantId, x.TagId });
                    table.ForeignKey(
                        name: "FK_TenantOperationalTags_OperationalTags_TagId",
                        column: x => x.TagId,
                        principalSchema: "tenancy",
                        principalTable: "OperationalTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantOperationalTags_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ChurnRisk",
                schema: "tenancy",
                table: "Tenants",
                column: "ChurnRisk");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CommercialRegion",
                schema: "tenancy",
                table: "Tenants",
                column: "CommercialRegion");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAtUtc",
                schema: "tenancy",
                table: "Tenants",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAtUtc_Id",
                schema: "tenancy",
                table: "Tenants",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ProductiveProfile",
                schema: "tenancy",
                table: "Tenants",
                column: "ProductiveProfile");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SizeSegment",
                schema: "tenancy",
                table: "Tenants",
                column: "SizeSegment");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                schema: "tenancy",
                table: "Tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTags_Slug",
                schema: "tenancy",
                table: "OperationalTags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantOperationalTags_TagId",
                schema: "tenancy",
                table: "TenantOperationalTags",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantOperationalTags",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "OperationalTags",
                schema: "tenancy");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ChurnRisk",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CommercialRegion",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CreatedAtUtc",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CreatedAtUtc_Id",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ProductiveProfile",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_SizeSegment",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Status",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ChurnRisk",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CommercialRegion",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ProductiveProfile",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SizeSegment",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
