using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForensicAuditAndRetentionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AdminUserEmail",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ActorRole",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "backoffice",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NewValuesJson",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldValuesJson",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousRecordHash",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordHash",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetTenantId",
                schema: "backoffice",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetTenantName",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                schema: "backoffice",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AdminUserId",
                schema: "backoffice",
                table: "AuditLogs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Category",
                schema: "backoffice",
                table: "AuditLogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RecordHash",
                schema: "backoffice",
                table: "AuditLogs",
                column: "RecordHash");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Severity",
                schema: "backoffice",
                table: "AuditLogs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TargetTenantId",
                schema: "backoffice",
                table: "AuditLogs",
                column: "TargetTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc",
                schema: "backoffice",
                table: "AuditLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc_Severity",
                schema: "backoffice",
                table: "AuditLogs",
                columns: new[] { "TimestampUtc", "Severity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_AdminUserId",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Category",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_RecordHash",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Severity",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TargetTenantId",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TimestampUtc",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TimestampUtc_Severity",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorRole",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "NewValuesJson",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OldValuesJson",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PreviousRecordHash",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RecordHash",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Severity",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TargetTenantId",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TargetTenantName",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                schema: "backoffice",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "AdminUserEmail",
                schema: "backoffice",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }
    }
}
