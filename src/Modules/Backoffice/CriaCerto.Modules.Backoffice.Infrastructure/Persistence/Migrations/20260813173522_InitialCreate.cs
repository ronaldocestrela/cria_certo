using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "backoffice");

            migrationBuilder.CreateTable(
                name: "AdminRoles",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminSessions",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefreshTokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    ReplacedByToken = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminUsers",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MfaSecretKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MustChangePasswordOnNextLogin = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminRoleAdminUser",
                schema: "backoffice",
                columns: table => new
                {
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RolesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoleAdminUser", x => new { x.AdminUserId, x.RolesId });
                    table.ForeignKey(
                        name: "FK_AdminRoleAdminUser_AdminRoles_RolesId",
                        column: x => x.RolesId,
                        principalSchema: "backoffice",
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminRoleAdminUser_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalSchema: "backoffice",
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminRolePermission",
                schema: "backoffice",
                columns: table => new
                {
                    AdminRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRolePermission", x => new { x.AdminRoleId, x.PermissionsId });
                    table.ForeignKey(
                        name: "FK_AdminRolePermission_AdminRoles_AdminRoleId",
                        column: x => x.AdminRoleId,
                        principalSchema: "backoffice",
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminRolePermission_Permissions_PermissionsId",
                        column: x => x.PermissionsId,
                        principalSchema: "backoffice",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminRoleAdminUser_RolesId",
                schema: "backoffice",
                table: "AdminRoleAdminUser",
                column: "RolesId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminRolePermission_PermissionsId",
                schema: "backoffice",
                table: "AdminRolePermission",
                column: "PermissionsId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminUserId",
                schema: "backoffice",
                table: "AdminSessions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_RefreshToken",
                schema: "backoffice",
                table: "AdminSessions",
                column: "RefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_SessionToken",
                schema: "backoffice",
                table: "AdminSessions",
                column: "SessionToken");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Email",
                schema: "backoffice",
                table: "AdminUsers",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminRoleAdminUser",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "AdminRolePermission",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "AdminSessions",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "AdminUsers",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "AdminRoles",
                schema: "backoffice");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "backoffice");
        }
    }
}
