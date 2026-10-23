using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Commands;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Dtos;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Queries;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class CustomerReferralTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;
    private readonly FakeTenantInfoLookupService _tenantLookupService;

    public CustomerReferralTests()
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
    public async Task GetDashboard_WhenParticipantDoesNotExist_ShouldCreateOnDemand_WithFarmNameSlug()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantLookupService.AddTenant(new TenantSummaryDto(tenantId, "Fazenda Bela Vista", "Bela Vista", "Active", "Starter", "Starter Plan"));

        var handler = new GetCustomerReferralDashboardQueryHandler(_dbContext, _tenantLookupService);
        var query = new GetCustomerReferralDashboardQuery(tenantId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReferralCode.Should().Be("FAZENDA-BELA-VISTA");
        result.Value.ReferralUrl.Should().Contain("/r/FAZENDA-BELA-VISTA");
        result.Value.ActiveReferralsCount.Should().Be(0);
        result.Value.PendingTrialReferralsCount.Should().Be(0);
        result.Value.AccumulatedDiscountNextInvoice.Should().Be(0);

        // Verify persisted in database
        var participant = await _dbContext.Participants.FirstOrDefaultAsync(p => p.TenantId == tenantId);
        participant.Should().NotBeNull();
        participant!.ParticipantType.Should().Be(ParticipantType.Customer);

        var code = await _dbContext.ReferralCodes.FirstOrDefaultAsync(c => c.ParticipantId == participant.Id);
        code.Should().NotBeNull();
        code!.Code.Should().Be("FAZENDA-BELA-VISTA");
    }

    [Fact]
    public async Task GetDashboard_WhenCodeCollides_ShouldAppendNumericSuffix()
    {
        // Arrange: Existing participant with code FAZENDA-MODELO
        var existingParticipant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid()).Value;
        var existingCode = ReferralCode.Create(existingParticipant.Id, "FAZENDA-MODELO").Value;
        _dbContext.Participants.Add(existingParticipant);
        _dbContext.ReferralCodes.Add(existingCode);
        await _dbContext.SaveChangesAsync();

        var newTenantId = Guid.NewGuid();
        _tenantLookupService.AddTenant(new TenantSummaryDto(newTenantId, "Fazenda Modelo", null, "Active", "Starter", "Starter Plan"));

        var handler = new GetCustomerReferralDashboardQueryHandler(_dbContext, _tenantLookupService);
        var query = new GetCustomerReferralDashboardQuery(newTenantId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReferralCode.Should().Be("FAZENDA-MODELO-2");
    }

    [Fact]
    public async Task GetDashboard_WithActiveAndPendingReferrals_ShouldCalculateDiscountsAndMaskNames()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCustomer(tenantId).Value;
        var code = ReferralCode.Create(participant.Id, "FAZENDA-PRODUTORA").Value;
        _dbContext.Participants.Add(participant);
        _dbContext.ReferralCodes.Add(code);

        // Add discount policy: 50.00 BRL fixed per active referral
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PRO",
            DiscountType.FixedValue,
            50.00m,
            100.00m).Value;
        _dbContext.PlanDiscountPolicies.Add(policy);

        // Referral 1: Active (Converted)
        var farm1TenantId = Guid.NewGuid();
        var attr1 = ReferralAttribution.Create(code.Id, participant.Id, farm1TenantId).Value;
        attr1.MarkConverted(DateTime.UtcNow.AddDays(-10));
        _dbContext.Attributions.Add(attr1);

        // Referral 2: Active (Converted)
        var farm2TenantId = Guid.NewGuid();
        var attr2 = ReferralAttribution.Create(code.Id, participant.Id, farm2TenantId).Value;
        attr2.MarkConverted(DateTime.UtcNow.AddDays(-5));
        _dbContext.Attributions.Add(attr2);

        // Referral 3: In Trial (Pending)
        var farm3TenantId = Guid.NewGuid();
        var attr3 = ReferralAttribution.Create(code.Id, participant.Id, farm3TenantId).Value;
        _dbContext.Attributions.Add(attr3);

        await _dbContext.SaveChangesAsync();

        _tenantLookupService.AddTenant(new TenantSummaryDto(tenantId, "Fazenda Produtora", null, "Active", "PRO", "Plano Profissional"));
        _tenantLookupService.AddTenant(new TenantSummaryDto(farm1TenantId, "Fazenda Santa Maria", null, "Active", "PRO", "Plano Profissional"));
        _tenantLookupService.AddTenant(new TenantSummaryDto(farm2TenantId, "Agropecuária Boi Gordo", null, "Active", "PRO", "Plano Profissional"));
        _tenantLookupService.AddTenant(new TenantSummaryDto(farm3TenantId, "Estância do Sol", null, "Trial", "Starter", "Starter Plan"));

        var handler = new GetCustomerReferralDashboardQueryHandler(_dbContext, _tenantLookupService);
        var query = new GetCustomerReferralDashboardQuery(tenantId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveReferralsCount.Should().Be(2);
        result.Value.PendingTrialReferralsCount.Should().Be(1);
        result.Value.AccumulatedDiscountNextInvoice.Should().Be(100.00m); // 2 x 50.00
        result.Value.FormattedAccumulatedDiscount.Should().Be("R$ 100,00");
        result.Value.WhatsAppShareText.Should().Contain("Cria Certo");
        result.Value.WhatsAppShareText.Should().Contain("/r/FAZENDA-PRODUTORA");

        result.Value.ReferredFarms.Should().HaveCount(3);
        result.Value.ReferredFarms.Should().Contain(f => f.MaskedFarmName == "F*****a S***a M***a" && f.Status == "ActiveGeneratingDiscount");
        result.Value.ReferredFarms.Should().Contain(f => f.MaskedFarmName == "A**********a B*i G***o" && f.Status == "ActiveGeneratingDiscount");
        result.Value.ReferredFarms.Should().Contain(f => f.MaskedFarmName == "E******a do S*l" && f.Status == "InTrialPendingPayment");
    }

    [Fact]
    public async Task CustomizeCode_WithValidAvailableCode_ShouldUpdateSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCustomer(tenantId).Value;
        var code = ReferralCode.Create(participant.Id, "FAZENDA-OLD").Value;
        _dbContext.Participants.Add(participant);
        _dbContext.ReferralCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var handler = new CustomizeReferralCodeCommandHandler(_dbContext);
        var command = new CustomizeReferralCodeCommand(tenantId, "FAZENDA-NOVA-2026");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("FAZENDA-NOVA-2026");
        result.Value.ReferralUrl.Should().Contain("/r/FAZENDA-NOVA-2026");

        var updatedCode = await _dbContext.ReferralCodes.FirstOrDefaultAsync(c => c.ParticipantId == participant.Id);
        updatedCode!.Code.Should().Be("FAZENDA-NOVA-2026");
    }

    [Fact]
    public async Task CustomizeCode_WhenCodeAlreadyExists_ShouldFailWithConflict()
    {
        // Arrange: Another participant already has "BOIVIGOR"
        var otherParticipant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid()).Value;
        var otherCode = ReferralCode.Create(otherParticipant.Id, "BOIVIGOR").Value;
        _dbContext.Participants.Add(otherParticipant);
        _dbContext.ReferralCodes.Add(otherCode);

        var myTenantId = Guid.NewGuid();
        var myParticipant = ReferralProgramParticipant.CreateCustomer(myTenantId).Value;
        var myCode = ReferralCode.Create(myParticipant.Id, "MYFARM").Value;
        _dbContext.Participants.Add(myParticipant);
        _dbContext.ReferralCodes.Add(myCode);
        await _dbContext.SaveChangesAsync();

        var handler = new CustomizeReferralCodeCommandHandler(_dbContext);
        var command = new CustomizeReferralCodeCommand(myTenantId, "BOIVIGOR");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralCode.AlreadyExists");
    }
}

public sealed class FakeTenantInfoLookupService : ITenantInfoLookupService
{
    private readonly Dictionary<Guid, TenantSummaryDto> _tenants = new();

    public void AddTenant(TenantSummaryDto tenant)
    {
        _tenants[tenant.TenantId] = tenant;
    }

    public Task<TenantSummaryDto?> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _tenants.TryGetValue(tenantId, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<IReadOnlyDictionary<Guid, TenantSummaryDto>> GetTenantSummariesAsync(
        IEnumerable<Guid> tenantIds,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, TenantSummaryDto> dict = _tenants
            .Where(kvp => tenantIds.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return Task.FromResult(dict);
    }

    public Task<int> GetActiveTenantsCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tenants.Count);
    }

    public Task<TenantsRetentionMetricsDto> GetTenantsRetentionMetricsAsync(
        IEnumerable<Guid> referredTenantIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TenantsRetentionMetricsDto(120, 90, 85.5m, 80.0m));
    }
}
