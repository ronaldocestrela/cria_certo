using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Analytics.Dtos;
using CriaCerto.Modules.Referral.Application.Features.Analytics.Queries;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public class GetReferralProgramKpisQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _db;
    private readonly FakeTenantInfoLookupService _tenantLookup;
    private readonly FakePaidMediaLookupService _paidMediaLookup;

    public GetReferralProgramKpisQueryTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ReferralDbContext>().UseSqlite(_connection).Options;
        _db = new ReferralDbContext(options);
        _db.Database.EnsureCreated();

        _tenantLookup = new FakeTenantInfoLookupService();
        _paidMediaLookup = new FakePaidMediaLookupService();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyKpis_WhenNoDataExists()
    {
        // Arrange
        var handler = new GetReferralProgramKpisQueryHandler(_db, _tenantLookup, _paidMediaLookup);
        var query = new GetReferralProgramKpisQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Funnel.TotalClicks.Should().Be(0);
        result.Value.Funnel.TotalAttributions.Should().Be(0);
        result.Value.Funnel.ConvertedAttributions.Should().Be(0);
        result.Value.Funnel.GlobalConversionRate.Should().Be(0m);
        result.Value.Cac.CustomerCac.Should().Be(0m);
        result.Value.Cac.PartnerCac.Should().Be(0m);
        result.Value.Cac.PaidMediaCac.Should().Be(0m);
        result.Value.Cac.BlendedCac.Should().Be(0m);
        result.Value.Virality.KFactor.Should().Be(0);
        result.Value.Virality.ViralStatus.Should().Be("Subcrítico");
    }

    [Fact]
    public async Task Handle_ShouldCalculateKpisAccurately_WhenDataExists()
    {
        // Arrange
        _tenantLookup.SetActiveTenantsCount(100);
        _tenantLookup.SetRetentionMetrics(new TenantsRetentionMetricsDto(
            ReferredAverageLifespanDays: 150,
            GlobalAverageLifespanDays: 100,
            ReferredRetentionRatePercentage: 90.0m,
            GlobalRetentionRatePercentage: 75.0m));

        _paidMediaLookup.SetSummary(totalSpent: 5000m, totalAcquisitions: 10);

        // Participante Cliente
        var customerParticipant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid()).Value;
        var partnerParticipant = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        _db.Participants.AddRange(customerParticipant, partnerParticipant);

        // Códigos
        var customerCode = ReferralCode.Create(customerParticipant.Id, "CLIENTE10").Value;
        customerCode.RecordClick();
        customerCode.RecordClick();
        customerCode.RecordClick();
        customerCode.RecordClick(); // 4 cliques

        var partnerCode = ReferralCode.Create(partnerParticipant.Id, "PARCEIRO20").Value;
        partnerCode.RecordClick();
        partnerCode.RecordClick(); // 2 cliques
        _db.ReferralCodes.AddRange(customerCode, partnerCode);

        // Atribuições Cliente (2 convertidas)
        var attr1 = ReferralAttribution.Create(customerCode.Id, customerParticipant.Id, Guid.NewGuid()).Value;
        attr1.MarkConverted(DateTime.UtcNow);
        var attr2 = ReferralAttribution.Create(customerCode.Id, customerParticipant.Id, Guid.NewGuid()).Value;
        attr2.MarkConverted(DateTime.UtcNow);

        // Atribuição Parceiro (1 convertida)
        var attr3 = ReferralAttribution.Create(partnerCode.Id, partnerParticipant.Id, Guid.NewGuid()).Value;
        attr3.MarkConverted(DateTime.UtcNow);

        _db.Attributions.AddRange(attr1, attr2, attr3);

        // Recompensas: Desconto R$ 100 por cada conversão de cliente (total R$ 200)
        var reward1 = ReferralRewardLedger.CreateInvoiceDiscountCredit(attr1.Id, customerParticipant.Id, 100m).Value;
        var reward2 = ReferralRewardLedger.CreateInvoiceDiscountCredit(attr2.Id, customerParticipant.Id, 100m).Value;
        // Comissão Parceiro: R$ 300
        var reward3 = ReferralRewardLedger.CreateCashCommission(attr3.Id, partnerParticipant.Id, 300m, 300m, "BRL", DateTime.UtcNow.AddDays(30)).Value;
        _db.RewardLedger.AddRange(reward1, reward2, reward3);

        await _db.SaveChangesAsync();

        var handler = new GetReferralProgramKpisQueryHandler(_db, _tenantLookup, _paidMediaLookup);
        var query = new GetReferralProgramKpisQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var kpis = result.Value;

        // Funil
        kpis.Funnel.TotalClicks.Should().Be(6); // 4 + 2
        kpis.Funnel.CustomerClicks.Should().Be(4);
        kpis.Funnel.PartnerClicks.Should().Be(2);
        kpis.Funnel.TotalAttributions.Should().Be(3);
        kpis.Funnel.ConvertedAttributions.Should().Be(3);
        kpis.Funnel.GlobalConversionRate.Should().Be(50.0m); // 3 / 6 * 100

        // CAC
        kpis.Cac.CustomerTotalRewardSpent.Should().Be(200m);
        kpis.Cac.CustomerCac.Should().Be(100m); // 200 / 2 clientes convertidos

        kpis.Cac.PartnerTotalCommissionSpent.Should().Be(300m);
        kpis.Cac.PartnerCac.Should().Be(300m); // 300 / 1 parceiro convertido

        kpis.Cac.PaidMediaTotalSpent.Should().Be(5000m);
        kpis.Cac.PaidMediaCac.Should().Be(500m); // 5000 / 10

        // Blended CAC = (200 + 300 + 5000) / (2 + 1 + 10) = 5500 / 13 = ~423.08
        kpis.Cac.BlendedCac.Should().Be(Math.Round(5500m / 13m, 2));

        // Viralidade (K-Factor)
        // K = 2 novos clientes indicados por clientes / 100 clientes ativos = 0.02
        kpis.Virality.KFactor.Should().Be(0.02);
        kpis.Virality.ActivePlatformTenantsCount.Should().Be(100);
        kpis.Virality.NewCustomersFromCustomerReferrals.Should().Be(2);
        kpis.Virality.ViralStatus.Should().Be("Subcrítico");

        // Retenção
        kpis.Retention.ReferredAverageLifespanDays.Should().Be(150);
        kpis.Retention.GlobalAverageLifespanDays.Should().Be(100);
        kpis.Retention.LifespanAdvantagePercentage.Should().Be(50.0m); // (150 - 100)/100 * 100
    }

    private sealed class FakeTenantInfoLookupService : ITenantInfoLookupService
    {
        private int _activeCount = 10;
        private TenantsRetentionMetricsDto _retention = new(100, 80, 90.0m, 80.0m);

        public void SetActiveTenantsCount(int count) => _activeCount = count;
        public void SetRetentionMetrics(TenantsRetentionMetricsDto retention) => _retention = retention;

        public Task<TenantSummaryDto?> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<TenantSummaryDto?>(null);
        public Task<IReadOnlyDictionary<Guid, TenantSummaryDto>> GetTenantSummariesAsync(IEnumerable<Guid> tenantIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, TenantSummaryDto>>(new Dictionary<Guid, TenantSummaryDto>());

        public Task<int> GetActiveTenantsCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_activeCount);
        public Task<TenantsRetentionMetricsDto> GetTenantsRetentionMetricsAsync(IEnumerable<Guid> referredTenantIds, CancellationToken cancellationToken = default) => Task.FromResult(_retention);
    }

    private sealed class FakePaidMediaLookupService : IPaidMediaSpendLookupService
    {
        private PaidMediaSummaryDto _summary = new(0m, 0);

        public void SetSummary(decimal totalSpent, int totalAcquisitions)
        {
            _summary = new PaidMediaSummaryDto(totalSpent, totalAcquisitions);
        }

        public Task<PaidMediaSummaryDto> GetPaidMediaSummaryAsync(DateTime? startDateUtc, DateTime? endDateUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_summary);
        }
    }
}
