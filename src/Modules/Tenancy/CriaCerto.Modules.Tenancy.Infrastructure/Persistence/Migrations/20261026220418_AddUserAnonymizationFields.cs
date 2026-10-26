using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAnonymizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnonymizationReason",
                schema: "tenancy",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymizedAtUtc",
                schema: "tenancy",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymized",
                schema: "tenancy",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsAnonymized",
                schema: "tenancy",
                table: "Users",
                column: "IsAnonymized");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsAnonymized",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AnonymizationReason",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AnonymizedAtUtc",
                schema: "tenancy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAnonymized",
                schema: "tenancy",
                table: "Users");
        }
    }
}
