using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBackofficeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CnpjNormalized",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommercialOwnerEmail",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommercialOwnerName",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                schema: "tenancy",
                table: "Tenants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "ExternalIdentifier",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalOwnerEmail",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalOwnerName",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                schema: "tenancy",
                table: "Tenants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.Sql(@"
                UPDATE tenancy.Tenants
                SET CnpjNormalized = REPLACE(REPLACE(REPLACE(REPLACE(CNPJ, '.', ''), '-', ''), '/', ''), ' ', '')
                WHERE CnpjNormalized = '' OR CnpjNormalized IS NULL;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT CnpjNormalized
                    FROM tenancy.Tenants
                    GROUP BY CnpjNormalized
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    RAISERROR('Migration failed: duplicate CNPJ values detected after normalization.', 16, 1);
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CnpjNormalized",
                schema: "tenancy",
                table: "Tenants",
                column: "CnpjNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ExternalIdentifier",
                schema: "tenancy",
                table: "Tenants",
                column: "ExternalIdentifier",
                unique: true,
                filter: "[ExternalIdentifier] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_ExternalIdentifier",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CnpjNormalized",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CnpjNormalized",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CommercialOwnerEmail",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CommercialOwnerName",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ExternalIdentifier",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TechnicalOwnerEmail",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TechnicalOwnerName",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
