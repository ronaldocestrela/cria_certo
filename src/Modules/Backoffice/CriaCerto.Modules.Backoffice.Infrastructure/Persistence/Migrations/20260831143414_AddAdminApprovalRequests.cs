using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminApprovalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminApprovalRequests",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    SupportTicketId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TargetResourceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImpactSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiffJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByAdminEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByAdminEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutionError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminApprovalRequests_ExpiresAtUtc",
                schema: "backoffice",
                table: "AdminApprovalRequests",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminApprovalRequests_RequestedByAdminUserId",
                schema: "backoffice",
                table: "AdminApprovalRequests",
                column: "RequestedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminApprovalRequests_RequestType",
                schema: "backoffice",
                table: "AdminApprovalRequests",
                column: "RequestType");

            migrationBuilder.CreateIndex(
                name: "IX_AdminApprovalRequests_Status",
                schema: "backoffice",
                table: "AdminApprovalRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminApprovalRequests",
                schema: "backoffice");
        }
    }
}
