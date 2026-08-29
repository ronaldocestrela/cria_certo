using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Breeding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "breeding");

            migrationBuilder.CreateTable(
                name: "Bulls",
                schema: "breeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EarTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Breed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RegistryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bulls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cows",
                schema: "breeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EarTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SisbovId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RfidTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tattoo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Nickname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Breed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntryWeightKg = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SireInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DamInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyConditionScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ParityCount = table.Column<int>(type: "int", nullable: false),
                    LastCalvingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IatfProtocols",
                schema: "breeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InseminationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SemenBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CowIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IatfProtocols", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PregnancyDiagnoses",
                schema: "breeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagnosisDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsPregnant = table.Column<bool>(type: "bit", nullable: false),
                    GestationalAgeDays = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PregnancyDiagnoses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SemenBatches",
                schema: "breeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Breed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrawQuantity = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemenBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bulls_EarTag",
                schema: "breeding",
                table: "Bulls",
                column: "EarTag");

            migrationBuilder.CreateIndex(
                name: "IX_Cows_EarTag",
                schema: "breeding",
                table: "Cows",
                column: "EarTag");

            migrationBuilder.CreateIndex(
                name: "IX_PregnancyDiagnoses_CowId",
                schema: "breeding",
                table: "PregnancyDiagnoses",
                column: "CowId");

            migrationBuilder.CreateIndex(
                name: "IX_SemenBatches_BatchCode",
                schema: "breeding",
                table: "SemenBatches",
                column: "BatchCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bulls",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "Cows",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "IatfProtocols",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "PregnancyDiagnoses",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "SemenBatches",
                schema: "breeding");
        }
    }
}
