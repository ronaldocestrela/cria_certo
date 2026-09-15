using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlagsAndRolloutGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RolloutPercentage = table.Column<int>(type: "int", nullable: false),
                    MaxAllowedRing = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AllowedRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhitelistedAdminEmailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlacklistedAdminEmailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KillSwitchActive = table.Column<bool>(type: "bit", nullable: false),
                    KillSwitchReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    KillSwitchActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KillSwitchActivatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastToggledReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Category",
                schema: "backoffice",
                table: "FeatureFlags",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_IsEnabled",
                schema: "backoffice",
                table: "FeatureFlags",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Key",
                schema: "backoffice",
                table: "FeatureFlags",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_KillSwitchActive",
                schema: "backoffice",
                table: "FeatureFlags",
                column: "KillSwitchActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureFlags",
                schema: "backoffice");
        }
    }
}
