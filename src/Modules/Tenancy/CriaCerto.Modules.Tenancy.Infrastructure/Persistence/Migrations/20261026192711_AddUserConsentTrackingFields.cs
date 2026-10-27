using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConsentTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsentIpAddress",
                schema: "tenancy",
                table: "Users",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacyPolicyAcceptedAtUtc",
                schema: "tenancy",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyPolicyVersion",
                schema: "tenancy",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAtUtc",
                schema: "tenancy",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                schema: "tenancy",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentIpAddress",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyAcceptedAtUtc",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyVersion",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAtUtc",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                schema: "tenancy",
                table: "Users");
        }
    }
}
