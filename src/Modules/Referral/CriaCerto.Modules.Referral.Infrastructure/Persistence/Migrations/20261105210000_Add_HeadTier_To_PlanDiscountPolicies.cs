using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Referral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_HeadTier_To_PlanDiscountPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HeadTierCatalogId",
                schema: "referral",
                table: "PlanDiscountPolicies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadTierCode",
                schema: "referral",
                table: "PlanDiscountPolicies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountScope",
                schema: "referral",
                table: "PlanDiscountPolicies",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyPlan");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                schema: "referral",
                table: "PlanDiscountPolicies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanDiscountPolicies_HeadTierCode",
                schema: "referral",
                table: "PlanDiscountPolicies",
                column: "HeadTierCode");

            migrationBuilder.CreateIndex(
                name: "IX_PlanDiscountPolicies_DiscountScope",
                schema: "referral",
                table: "PlanDiscountPolicies",
                column: "DiscountScope");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanDiscountPolicies_HeadTierCode",
                schema: "referral",
                table: "PlanDiscountPolicies");

            migrationBuilder.DropIndex(
                name: "IX_PlanDiscountPolicies_DiscountScope",
                schema: "referral",
                table: "PlanDiscountPolicies");

            migrationBuilder.DropColumn(
                name: "HeadTierCatalogId",
                schema: "referral",
                table: "PlanDiscountPolicies");

            migrationBuilder.DropColumn(
                name: "HeadTierCode",
                schema: "referral",
                table: "PlanDiscountPolicies");

            migrationBuilder.DropColumn(
                name: "DiscountScope",
                schema: "referral",
                table: "PlanDiscountPolicies");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "referral",
                table: "PlanDiscountPolicies");
        }
    }
}

