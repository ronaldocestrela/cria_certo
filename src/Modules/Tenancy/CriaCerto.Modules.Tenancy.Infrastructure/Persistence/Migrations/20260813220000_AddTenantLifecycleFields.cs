using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                schema: "tenancy",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAtUtc",
                schema: "tenancy",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE tenancy.Tenants
                SET Status = 'Suspended'
                WHERE Status = 'Maintenance';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProtected",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
