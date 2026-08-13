using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Calving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "calving");

            migrationBuilder.CreateTable(
                name: "Calves",
                schema: "calving",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MotherCowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sex = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Breed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BirthWeightKg = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Calvings",
                schema: "calving",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MotherCowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalvingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CalfId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calvings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Weanings",
                schema: "calving",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalfId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MotherCowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeaningDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WeaningWeightKg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Adjusted205DayWeightKg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    DestinationLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weanings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calves_TagId",
                schema: "calving",
                table: "Calves",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Calvings_MotherCowId",
                schema: "calving",
                table: "Calvings",
                column: "MotherCowId");

            migrationBuilder.CreateIndex(
                name: "IX_Weanings_CalfId",
                schema: "calving",
                table: "Weanings",
                column: "CalfId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Calves",
                schema: "calving");

            migrationBuilder.DropTable(
                name: "Calvings",
                schema: "calving");

            migrationBuilder.DropTable(
                name: "Weanings",
                schema: "calving");
        }
    }
}
