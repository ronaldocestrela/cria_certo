using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.UnitTests.Persistence;

public sealed class ReferralDbContextModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ReferralDbContext> _options;

    public ReferralDbContextModelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void ModelBuilder_ShouldConfigureDefaultSchemaAsReferral()
    {
        using var context = new ReferralDbContext(_options);
        var schema = context.Model.GetDefaultSchema();

        schema.Should().Be("referral");
    }

    [Fact]
    public void ModelBuilder_ShouldContainAllDomainEntitiesMappedToCorrectTables()
    {
        using var context = new ReferralDbContext(_options);
        var model = context.Model;

        var participantEntity = model.FindEntityType(typeof(ReferralProgramParticipant));
        participantEntity.Should().NotBeNull();
        participantEntity!.GetTableName().Should().Be("Participants");

        var codeEntity = model.FindEntityType(typeof(ReferralCode));
        codeEntity.Should().NotBeNull();
        codeEntity!.GetTableName().Should().Be("ReferralCodes");

        var contractEntity = model.FindEntityType(typeof(PartnerContract));
        contractEntity.Should().NotBeNull();
        contractEntity!.GetTableName().Should().Be("PartnerContracts");

        var policyEntity = model.FindEntityType(typeof(PlanReferralDiscountPolicy));
        policyEntity.Should().NotBeNull();
        policyEntity!.GetTableName().Should().Be("PlanDiscountPolicies");

        var attributionEntity = model.FindEntityType(typeof(ReferralAttribution));
        attributionEntity.Should().NotBeNull();
        attributionEntity!.GetTableName().Should().Be("Attributions");

        var ledgerEntity = model.FindEntityType(typeof(ReferralRewardLedger));
        ledgerEntity.Should().NotBeNull();
        ledgerEntity!.GetTableName().Should().Be("RewardLedger");
    }

    [Fact]
    public void ModelBuilder_ShouldConfigureUniqueIndexOnReferralCode()
    {
        using var context = new ReferralDbContext(_options);
        var codeEntity = context.Model.FindEntityType(typeof(ReferralCode))!;

        var codeIndex = codeEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(ReferralCode.Code)));

        codeIndex.Should().NotBeNull();
        codeIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void ModelBuilder_ShouldConfigureRequiredIndexesOnAttribution()
    {
        using var context = new ReferralDbContext(_options);
        var attributionEntity = context.Model.FindEntityType(typeof(ReferralAttribution))!;

        var indexes = attributionEntity.GetIndexes().ToList();

        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(ReferralAttribution.ReferredTenantId)));
        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(ReferralAttribution.ReferralCodeId)));
        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(ReferralAttribution.ReferrerParticipantId)));
        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(ReferralAttribution.Status)));
    }

    [Fact]
    public void ModelBuilder_ShouldConfigureRequiredIndexesOnRewardLedger()
    {
        using var context = new ReferralDbContext(_options);
        var ledgerEntity = context.Model.FindEntityType(typeof(ReferralRewardLedger))!;

        var indexes = ledgerEntity.GetIndexes().ToList();

        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(ReferralRewardLedger.ParticipantId)));
        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(ReferralRewardLedger.ReferralAttributionId)));
        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(ReferralRewardLedger.Status)));
    }

    [Fact]
    public async Task CanPersistAndQuery_CompleteReferralLifecycle_InDatabase()
    {
        using (var context = new ReferralDbContext(_options))
        {
            await context.Database.EnsureCreatedAsync();

            var tenantId = Guid.NewGuid();
            var participant = ReferralProgramParticipant.CreateCustomer(tenantId).Value;
            var code = ReferralCode.Create(participant.Id, "FAZENDA-MODELO").Value;
            var policy = PlanReferralDiscountPolicy.Create(Guid.NewGuid(), "PRO", DiscountType.Percentage, 15m).Value;

            context.Participants.Add(participant);
            context.ReferralCodes.Add(code);
            context.PlanDiscountPolicies.Add(policy);

            await context.SaveChangesAsync();

            var referredTenantId = Guid.NewGuid();
            var attribution = ReferralAttribution.Create(code.Id, participant.Id, referredTenantId).Value;
            context.Attributions.Add(attribution);
            await context.SaveChangesAsync();

            var ledger = ReferralRewardLedger.CreateInvoiceDiscountCredit(attribution.Id, participant.Id, 150m).Value;
            context.RewardLedger.Add(ledger);
            await context.SaveChangesAsync();
        }

        using (var context = new ReferralDbContext(_options))
        {
            var savedParticipant = await context.Participants.SingleAsync();
            savedParticipant.ParticipantType.Should().Be(ParticipantType.Customer);
            savedParticipant.Status.Should().Be(ParticipantStatus.Active);

            var savedCode = await context.ReferralCodes.SingleAsync();
            savedCode.Code.Should().Be("FAZENDA-MODELO");
            savedCode.IsActive.Should().BeTrue();

            var savedAttribution = await context.Attributions.SingleAsync();
            savedAttribution.Status.Should().Be(AttributionStatus.Pending);

            var savedLedger = await context.RewardLedger.SingleAsync();
            savedLedger.RewardType.Should().Be(RewardType.InvoiceDiscountCredit);
            savedLedger.GrossAmount.Should().Be(150m);
        }
    }
}
