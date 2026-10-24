using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Commands;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Dtos;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Queries;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class PartnerPortalTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;
    private readonly FakeTenantInfoLookupService _tenantLookupService;

    public PartnerPortalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ReferralDbContext(options);
        _dbContext.Database.EnsureCreated();

        _tenantLookupService = new FakeTenantInfoLookupService();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetDashboard_WhenPartnerExists_ShouldReturnAggregatedMetricsAndCampaigns()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var participant = ReferralProgramParticipant.CreateCommercialPartner(adminId, userId).Value;
        _dbContext.Participants.Add(participant);

        var profile = CommercialPartnerProfile.Create(
            participant.Id,
            "Dr. Roberto Veterinário",
            "Clínica Boi Gordo",
            "roberto@boigordo.com.br",
            "11987654321",
            PartnerCategory.Veterinarian,
            "Ribeirão Preto",
            "SP").Value;
        _dbContext.PartnerProfiles.Add(profile);

        var contract = PartnerContract.Create(
            participant.Id,
            CommissionType.PercentageRecurring,
            20m,
            12,
            30,
            "roberto@boigordo.com.br",
            PixKeyType.Email,
            "123.456.789-00",
            DateTime.UtcNow).Value;
        _dbContext.PartnerContracts.Add(contract);

        var code1 = ReferralCode.Create(participant.Id, "BOI-GORDO", "Principal").Value;
        code1.RecordClick();
        code1.RecordClick();
        code1.RecordClick();
        var code2 = ReferralCode.Create(participant.Id, "BOI-GORDO-INSTA", "Instagram").Value;
        code2.RecordClick();
        _dbContext.ReferralCodes.AddRange(code1, code2);

        // Atribuições: 1 Trial (Pending), 1 Paga (Converted)
        var referredTenant1 = Guid.NewGuid();
        var referredTenant2 = Guid.NewGuid();

        var attr1 = ReferralAttribution.Create(code1.Id, participant.Id, referredTenant1).Value;
        var attr2 = ReferralAttribution.Create(code2.Id, participant.Id, referredTenant2).Value;
        attr2.MarkConverted(DateTime.UtcNow);
        _dbContext.Attributions.AddRange(attr1, attr2);

        // Ledgers: 1 PendingMaturity (R$ 100), 1 AvailableForPayout (R$ 200), 1 PaidOut (R$ 300)
        var ledger1 = ReferralRewardLedger.CreateCashCommission(
            attr2.Id, participant.Id, grossAmount: 500m, netAmount: 100m, "BRL", DateTime.UtcNow.AddDays(20)).Value;

        var ledger2 = ReferralRewardLedger.CreateCashCommission(
            attr2.Id, participant.Id, grossAmount: 1000m, netAmount: 200m, "BRL", DateTime.UtcNow.AddDays(-5)).Value;
        ledger2.ReleaseForPayout();

        var ledger3 = ReferralRewardLedger.CreateCashCommission(
            attr2.Id, participant.Id, grossAmount: 1500m, netAmount: 300m, "BRL", DateTime.UtcNow.AddDays(-40)).Value;
        ledger3.ReleaseForPayout();
        ledger3.MarkPaidOut("PIX-E2E-12345");

        _dbContext.RewardLedger.AddRange(ledger1, ledger2, ledger3);
        await _dbContext.SaveChangesAsync();

        var handler = new GetPartnerPortalDashboardQueryHandler(_dbContext);
        var query = new GetPartnerPortalDashboardQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dashboard = result.Value;
        dashboard.FullName.Should().Be("Dr. Roberto Veterinário");
        dashboard.TotalClicks.Should().Be(4);
        dashboard.TrialSignupsCount.Should().Be(1);
        dashboard.PaidSubscriptionsCount.Should().Be(1);
        dashboard.ConversionRatePercentage.Should().Be(50m);
        dashboard.PendingMaturityAmount.Should().Be(100m);
        dashboard.AvailablePayoutAmount.Should().Be(200m);
        dashboard.TotalPaidAmount.Should().Be(300m);
        dashboard.Campaigns.Should().HaveCount(2);
        dashboard.PixKey.Should().Be("roberto@boigordo.com.br");
    }

    [Fact]
    public async Task UpdatePixKey_WhenValid_ShouldUpdateActiveContractPixKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid(), userId).Value;
        _dbContext.Participants.Add(participant);

        var contract = PartnerContract.Create(
            participant.Id,
            CommissionType.PercentageRecurring,
            15m,
            null,
            30,
            "antiga@chave.com",
            PixKeyType.Email,
            "123.456.789-00",
            DateTime.UtcNow).Value;
        _dbContext.PartnerContracts.Add(contract);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdatePartnerPixKeyCommandHandler(_dbContext);
        var command = new UpdatePartnerPixKeyCommand(userId, PixKeyType.Phone, "+5511999998888");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.PartnerContracts.FirstAsync(c => c.Id == contract.Id);
        updated.PixKey.Should().Be("+5511999998888");
        updated.PixKeyType.Should().Be(PixKeyType.Phone);
    }

    [Fact]
    public async Task CreateCampaignCode_WhenUnique_ShouldCreateNewReferralCode()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid(), userId).Value;
        _dbContext.Participants.Add(participant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreatePartnerCampaignCodeCommandHandler(_dbContext);
        var command = new CreatePartnerCampaignCodeCommand(userId, "Palestra Goiás 2026", "GOIAS-2026");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("GOIAS-2026");
        result.Value.CampaignTitle.Should().Be("Palestra Goiás 2026");

        var persisted = await _dbContext.ReferralCodes.FirstOrDefaultAsync(c => c.Code == "GOIAS-2026");
        persisted.Should().NotBeNull();
        persisted!.ParticipantId.Should().Be(participant.Id);
    }

    [Fact]
    public async Task GetStatement_ShouldFilterAndReturnFarmName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid(), userId).Value;
        _dbContext.Participants.Add(participant);

        var contract = PartnerContract.Create(
            participant.Id,
            CommissionType.PercentageRecurring,
            20m,
            null,
            30,
            "chave@pix.com",
            PixKeyType.Email,
            "11.222.333/0001-44",
            DateTime.UtcNow).Value;
        _dbContext.PartnerContracts.Add(contract);

        var code = ReferralCode.Create(participant.Id, "TESTE-PARTNER").Value;
        _dbContext.ReferralCodes.Add(code);

        var referredTenant = Guid.NewGuid();
        _tenantLookupService.AddTenant(new TenantSummaryDto(
            referredTenant, "Fazenda Santa Maria", "Santa Maria Agro", "Active", "Pro", "Plano Pro"));

        var attr = ReferralAttribution.Create(code.Id, participant.Id, referredTenant).Value;
        attr.MarkConverted(DateTime.UtcNow);
        _dbContext.Attributions.Add(attr);

        var ledger = ReferralRewardLedger.CreateCashCommission(
            attr.Id, participant.Id, 1000m, 200m, "BRL", DateTime.UtcNow.AddDays(15)).Value;
        _dbContext.RewardLedger.Add(ledger);
        await _dbContext.SaveChangesAsync();

        var handler = new GetPartnerCommissionStatementQueryHandler(_dbContext, _tenantLookupService);
        var query = new GetPartnerCommissionStatementQuery(userId, Page: 1, PageSize: 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        var item = result.Value.Items[0];
        item.ReferredTenantName.Should().Be("Santa Maria Agro");
        item.InvoiceGrossAmount.Should().Be(1000m);
        item.NetCommissionAmount.Should().Be(200m);
        item.CommissionRateDescription.Should().Be("20% Recorrente");
        item.Status.Should().Be(LedgerRewardStatus.PendingMaturity);
    }

    private sealed class FakeTenantInfoLookupService : ITenantInfoLookupService
    {
        private readonly Dictionary<Guid, TenantSummaryDto> _tenants = new();

        public void AddTenant(TenantSummaryDto dto) => _tenants[dto.TenantId] = dto;

        public Task<TenantSummaryDto?> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            _tenants.TryGetValue(tenantId, out var summary);
            return Task.FromResult(summary);
        }

        public Task<IReadOnlyDictionary<Guid, TenantSummaryDto>> GetTenantSummariesAsync(
            IEnumerable<Guid> tenantIds,
            CancellationToken cancellationToken = default)
        {
            var dict = _tenants
                .Where(kvp => tenantIds.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return Task.FromResult<IReadOnlyDictionary<Guid, TenantSummaryDto>>(dict);
        }

        public Task<int> GetActiveTenantsCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tenants.Count);
        }

        public Task<TenantsRetentionMetricsDto> GetTenantsRetentionMetricsAsync(
            IEnumerable<Guid> referredTenantIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TenantsRetentionMetricsDto(100, 80, 90.0m, 82.0m));
        }
    }
}
