using CriaCerto.Modules.Sanitary.Application.Contracts;
using CriaCerto.Modules.Sanitary.Application.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Sanitary.UnitTests;

public class TestSanitaryDbContext : DbContext, ISanitaryDbContext
{
    public DbSet<VaccinationCampaign> VaccinationCampaigns => Set<VaccinationCampaign>();
    public DbSet<TreatmentRecord> TreatmentRecords => Set<TreatmentRecord>();
    public DbSet<VaccineReference> VaccineReferences => Set<VaccineReference>();

    public TestSanitaryDbContext(DbContextOptions<TestSanitaryDbContext> options) : base(options) { }
}

public class SanitaryQueryTests
{
    private static TestSanitaryDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<TestSanitaryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TestSanitaryDbContext(options);
    }

    [Fact]
    public async Task GetTreatmentsQuery_WhenNoTreatments_ShouldReturnEmptyList()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new GetTreatmentsQueryHandler(db);

        var result = await handler.Handle(new GetTreatmentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreatmentsQuery_ShouldReturnTreatmentsWithCorrectWithdrawalActiveStatus()
    {
        using var db = CreateInMemoryDbContext();
        var animalId = Guid.NewGuid();

        // Active withdrawal treatment: applied yesterday with 30 days withdrawal
        var activeTreatment = TreatmentRecord.Create(
            animalId,
            "Dectomax 1%",
            TreatmentType.Deworming,
            "LOT-01",
            "10ml",
            30,
            DateTime.UtcNow.AddDays(-1),
            "Dr. Vet").Value;

        // Expired withdrawal treatment: applied 40 days ago with 10 days withdrawal
        var expiredTreatment = TreatmentRecord.Create(
            animalId,
            "Oxitetraciclina LA",
            TreatmentType.Medication,
            "LOT-02",
            "15ml",
            10,
            DateTime.UtcNow.AddDays(-40),
            "Dr. Vet").Value;

        db.TreatmentRecords.AddRange(activeTreatment, expiredTreatment);
        await db.SaveChangesAsync();

        var handler = new GetTreatmentsQueryHandler(db);
        var result = await handler.Handle(new GetTreatmentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        // First item should be the most recently applied (activeTreatment)
        result.Value[0].ProductCommercialName.Should().Be("Dectomax 1%");
        result.Value[0].IsWithdrawalActive.Should().BeTrue();

        // Second item should be the expired one
        result.Value[1].ProductCommercialName.Should().Be("Oxitetraciclina LA");
        result.Value[1].IsWithdrawalActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveCampaignsQuery_ShouldReturnOnlyActiveCampaigns()
    {
        using var db = CreateInMemoryDbContext();

        var activeCampaign = VaccinationCampaign.Create(
            "Campanha Aftosa 2026",
            CampaignType.Aftosa,
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow.AddDays(25)).Value;

        db.VaccinationCampaigns.Add(activeCampaign);
        await db.SaveChangesAsync();

        var handler = new GetActiveCampaignsQueryHandler(db);
        var result = await handler.Handle(new GetActiveCampaignsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(c => c.Name == "Campanha Aftosa 2026");
    }
}
