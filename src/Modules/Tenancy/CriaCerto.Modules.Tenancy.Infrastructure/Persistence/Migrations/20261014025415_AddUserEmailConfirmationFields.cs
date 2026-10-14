using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailConfirmationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailConfirmationToken",
                schema: "tenancy",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmationTokenExpiresAt",
                schema: "tenancy",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmedAtUtc",
                schema: "tenancy",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailConfirmed",
                schema: "tenancy",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmationToken",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmationTokenExpiresAt",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmedAtUtc",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsEmailConfirmed",
                schema: "tenancy",
                table: "Users");
        }
    }
}
