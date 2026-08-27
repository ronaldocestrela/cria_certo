using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImpersonationSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImpersonationSessions",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetTenantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetUserEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupportTicket = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpersonationSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationSessions_AdminUserId",
                schema: "backoffice",
                table: "ImpersonationSessions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationSessions_ExpiresAtUtc",
                schema: "backoffice",
                table: "ImpersonationSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationSessions_Status",
                schema: "backoffice",
                table: "ImpersonationSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationSessions_TargetTenantId",
                schema: "backoffice",
                table: "ImpersonationSessions",
                column: "TargetTenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImpersonationSessions",
                schema: "backoffice");
        }
    }
}
