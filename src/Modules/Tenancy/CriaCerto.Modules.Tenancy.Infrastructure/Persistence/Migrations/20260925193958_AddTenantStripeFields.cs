using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantStripeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                schema: "tenancy",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEndUtc",
                schema: "tenancy",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                schema: "tenancy",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_StripeCustomerId",
                schema: "tenancy",
                table: "Tenants",
                column: "StripeCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_StripeSubscriptionId",
                schema: "tenancy",
                table: "Tenants",
                column: "StripeSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_StripeCustomerId",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_StripeSubscriptionId",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEndUtc",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripePriceId",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
