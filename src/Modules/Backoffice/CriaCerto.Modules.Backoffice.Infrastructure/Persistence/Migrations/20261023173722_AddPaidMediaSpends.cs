using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidMediaSpends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaidMediaSpends",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PeriodStartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AcquisitionsCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaidMediaSpends", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaidMediaSpends_Channel",
                schema: "backoffice",
                table: "PaidMediaSpends",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_PaidMediaSpends_PeriodStartDateUtc_PeriodEndDateUtc",
                schema: "backoffice",
                table: "PaidMediaSpends",
                columns: new[] { "PeriodStartDateUtc", "PeriodEndDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaidMediaSpends",
                schema: "backoffice");
        }
    }
}
