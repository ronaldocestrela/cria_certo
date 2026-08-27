using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSubscriptionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantSubscriptionHistories",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotHeadCount = table.Column<int>(type: "int", nullable: false),
                    SnapshotUserCount = table.Column<int>(type: "int", nullable: false),
                    SnapshotUnitCount = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptionHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptionHistories_TenantId",
                schema: "tenancy",
                table: "TenantSubscriptionHistories",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSubscriptionHistories",
                schema: "tenancy");
        }
    }
}
