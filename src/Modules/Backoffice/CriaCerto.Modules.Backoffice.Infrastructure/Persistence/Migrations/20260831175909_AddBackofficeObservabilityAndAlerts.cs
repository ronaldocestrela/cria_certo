using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackofficeObservabilityAndAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerts",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    FirstTriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastTriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetTenantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RelatedAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedAdminEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedByEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Fingerprint",
                schema: "backoffice",
                table: "Alerts",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_LastTriggeredAtUtc",
                schema: "backoffice",
                table: "Alerts",
                column: "LastTriggeredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_RuleCode",
                schema: "backoffice",
                table: "Alerts",
                column: "RuleCode");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Status_Severity",
                schema: "backoffice",
                table: "Alerts",
                columns: new[] { "Status", "Severity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts",
                schema: "backoffice");
        }
    }
}
