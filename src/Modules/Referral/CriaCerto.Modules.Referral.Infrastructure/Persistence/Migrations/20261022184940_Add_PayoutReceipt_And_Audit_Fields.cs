using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Referral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_PayoutReceipt_And_Audit_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutReceiptFilePath",
                schema: "referral",
                table: "RewardLedger",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutRegisteredAtUtc",
                schema: "referral",
                table: "RewardLedger",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutRegisteredByAdminId",
                schema: "referral",
                table: "RewardLedger",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutReceiptFilePath",
                schema: "referral",
                table: "RewardLedger");

            migrationBuilder.DropColumn(
                name: "PayoutRegisteredAtUtc",
                schema: "referral",
                table: "RewardLedger");

            migrationBuilder.DropColumn(
                name: "PayoutRegisteredByAdminId",
                schema: "referral",
                table: "RewardLedger");
        }
    }
}
