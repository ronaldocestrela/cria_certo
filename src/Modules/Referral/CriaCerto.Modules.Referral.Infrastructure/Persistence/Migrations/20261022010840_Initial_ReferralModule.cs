using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Referral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial_ReferralModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "referral");

            migrationBuilder.CreateTable(
                name: "Participants",
                schema: "referral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParticipantType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SuspensionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanDiscountPolicies",
                schema: "referral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDiscountCapPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 100.00m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanDiscountPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerContracts",
                schema: "referral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommissionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CommissionValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MaxRecurringMonths = table.Column<int>(type: "int", nullable: true),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    PixKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PixKeyType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LegalDocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerContracts_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "referral",
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferralCodes",
                schema: "referral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CampaignTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ClicksCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralCodes_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "referral",
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attributions",
                schema: "referral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferralCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferrerParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferredTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributionDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConvertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeviceFingerprintHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FraudReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attributions_Participants_ReferrerParticipantId",
                        column: x => x.ReferrerParticipantId,
                        principalSchema: "referral",
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attributions_ReferralCodes_ReferralCodeId",
                        column: x => x.ReferralCodeId,
                        principalSchema: "referral",
                        principalTable: "ReferralCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RewardLedger",
                schema: "referral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferralAttributionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewardType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaturityDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StripeInvoiceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StripeCreditBalanceTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayoutTransactionReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardLedger_Attributions_ReferralAttributionId",
                        column: x => x.ReferralAttributionId,
                        principalSchema: "referral",
                        principalTable: "Attributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RewardLedger_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "referral",
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attributions_AttributionDateUtc",
                schema: "referral",
                table: "Attributions",
                column: "AttributionDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Attributions_ReferralCodeId",
                schema: "referral",
                table: "Attributions",
                column: "ReferralCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Attributions_ReferredTenantId",
                schema: "referral",
                table: "Attributions",
                column: "ReferredTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Attributions_ReferredTenantId_Status",
                schema: "referral",
                table: "Attributions",
                columns: new[] { "ReferredTenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Attributions_ReferrerParticipantId",
                schema: "referral",
                table: "Attributions",
                column: "ReferrerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Attributions_Status",
                schema: "referral",
                table: "Attributions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_CreatedAtUtc",
                schema: "referral",
                table: "Participants",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_ParticipantType",
                schema: "referral",
                table: "Participants",
                column: "ParticipantType");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Status",
                schema: "referral",
                table: "Participants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TenantId",
                schema: "referral",
                table: "Participants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_UserId",
                schema: "referral",
                table: "Participants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerContracts_IsActive",
                schema: "referral",
                table: "PartnerContracts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerContracts_ParticipantId",
                schema: "referral",
                table: "PartnerContracts",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerContracts_ValidFromUtc_ValidToUtc",
                schema: "referral",
                table: "PartnerContracts",
                columns: new[] { "ValidFromUtc", "ValidToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanDiscountPolicies_IsActive",
                schema: "referral",
                table: "PlanDiscountPolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PlanDiscountPolicies_PlanCatalogId",
                schema: "referral",
                table: "PlanDiscountPolicies",
                column: "PlanCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanDiscountPolicies_PlanCode",
                schema: "referral",
                table: "PlanDiscountPolicies",
                column: "PlanCode");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_Code",
                schema: "referral",
                table: "ReferralCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_IsActive",
                schema: "referral",
                table: "ReferralCodes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_ParticipantId",
                schema: "referral",
                table: "ReferralCodes",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardLedger_MaturityDateUtc",
                schema: "referral",
                table: "RewardLedger",
                column: "MaturityDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RewardLedger_ParticipantId",
                schema: "referral",
                table: "RewardLedger",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardLedger_ReferralAttributionId",
                schema: "referral",
                table: "RewardLedger",
                column: "ReferralAttributionId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardLedger_Status",
                schema: "referral",
                table: "RewardLedger",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RewardLedger_Status_MaturityDateUtc",
                schema: "referral",
                table: "RewardLedger",
                columns: new[] { "Status", "MaturityDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardLedger_StripeInvoiceId",
                schema: "referral",
                table: "RewardLedger",
                column: "StripeInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerContracts",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "PlanDiscountPolicies",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "RewardLedger",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "Attributions",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "ReferralCodes",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "Participants",
                schema: "referral");
        }
    }
}
