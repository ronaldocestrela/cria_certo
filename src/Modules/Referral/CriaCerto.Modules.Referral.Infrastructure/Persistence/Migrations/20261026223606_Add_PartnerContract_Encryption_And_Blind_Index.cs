using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Referral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_PartnerContract_Encryption_And_Blind_Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PixKey",
                schema: "referral",
                table: "PartnerContracts",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "LegalDocumentNumber",
                schema: "referral",
                table: "PartnerContracts",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "LegalDocumentNumberHash",
                schema: "referral",
                table: "PartnerContracts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerContracts_LegalDocumentNumberHash",
                schema: "referral",
                table: "PartnerContracts",
                column: "LegalDocumentNumberHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartnerContracts_LegalDocumentNumberHash",
                schema: "referral",
                table: "PartnerContracts");

            migrationBuilder.DropColumn(
                name: "LegalDocumentNumberHash",
                schema: "referral",
                table: "PartnerContracts");

            migrationBuilder.AlterColumn<string>(
                name: "PixKey",
                schema: "referral",
                table: "PartnerContracts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "LegalDocumentNumber",
                schema: "referral",
                table: "PartnerContracts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);
        }
    }
}
