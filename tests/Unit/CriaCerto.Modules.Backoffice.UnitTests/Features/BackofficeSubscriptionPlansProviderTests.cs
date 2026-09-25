using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Services;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class BackofficeSubscriptionPlansProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _db;
    private readonly BackofficeSubscriptionPlansProvider _provider;

    public BackofficeSubscriptionPlansProviderTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _db = new BackofficeDbContext(options);
        _db.Database.EnsureCreated();
        _provider = new BackofficeSubscriptionPlansProvider(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetActivePlansAsync_WhenNoPlansInDb_ShouldReturnNull()
    {
        var result = await _provider.GetActivePlansAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActivePlansAsync_WithPublishedPlans_ShouldReturnExactBackofficeValuesAndFeatures()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var starter = PlanCatalog.Create("starter", "Plano Starter", "Desc Starter").Value;
        var starterVersion = starter.CreateVersion(
            "v1.0",
            149.90m,
            119.90m,
            500,
            features: new[]
            {
                PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                PlanFeature.Create("Modules.Calving", "Módulo de Partos & Bezerreiro", true)
            }).Value;
        starter.PublishVersion(starterVersion.Id, adminId);
        _db.PlanCatalogs.Add(starter);

        var pro = PlanCatalog.Create("pro", "Plano Profissional", "Desc Pro").Value;
        var proVersion = pro.CreateVersion(
            "v1.0",
            349.90m,
            299.90m,
            2500,
            features: new[]
            {
                PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                PlanFeature.Create("Modules.Growth", "Módulo de Manejo & Pesagem", true)
            }).Value;
        pro.PublishVersion(proVersion.Id, adminId);
        _db.PlanCatalogs.Add(pro);

        await _db.SaveChangesAsync();

        // Act
        var plans = await _provider.GetActivePlansAsync();

        // Assert
        plans.Should().NotBeNull();
        plans!.Should().HaveCount(2);

        var starterPlan = plans.Single(p => p.PlanId == "Starter");
        starterPlan.Name.Should().Be("Plano Starter");
        starterPlan.MonthlyPrice.Should().Be(149.90m);
        starterPlan.AnnualPriceMonthly.Should().Be(119.90m);
        starterPlan.HeadCapacityLimit.Should().Be(500);
        starterPlan.Features.Should().NotBeNull();
        starterPlan.Features!.Select(f => f.Name).Should().Contain(new[]
        {
            "Módulo de Reprodução & IATF",
            "Módulo de Partos & Bezerreiro"
        });

        var proPlan = plans.Single(p => p.PlanId == "Pro");
        proPlan.MonthlyPrice.Should().Be(349.90m);
        proPlan.IsPopular.Should().BeTrue();
        proPlan.Features!.Select(f => f.Name).Should().Contain("Módulo de Manejo & Pesagem");
    }

    [Fact]
    public async Task GetActivePlansAsync_WhenDraftVersionEdited_ShouldReflectDraftValuesAndFunctions()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var starter = PlanCatalog.Create("starter", "Plano Starter", "Desc Starter").Value;
        var v1 = starter.CreateVersion("v1.0", 149.90m, 119.90m, 500).Value;
        starter.PublishVersion(v1.Id, adminId);

        // Admin alters the plan in backoffice by creating and editing a draft v2
        var v2 = starter.CreateVersion(
            "v2.0 - Reajuste",
            179.90m,
            149.90m,
            800,
            features: new[]
            {
                PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                PlanFeature.Create("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                PlanFeature.Create("PwaOfflineMode", "Modo Offline PWA em Curral", true)
            }).Value;

        _db.PlanCatalogs.Add(starter);
        await _db.SaveChangesAsync();

        // Act
        var plans = await _provider.GetActivePlansAsync();

        // Assert
        plans.Should().NotBeNull();
        var plan = plans!.Single(p => p.PlanId == "Starter");
        plan.MonthlyPrice.Should().Be(179.90m);
        plan.AnnualPriceMonthly.Should().Be(149.90m);
        plan.HeadCapacityLimit.Should().Be(800);
        plan.Features!.Select(f => f.Name).Should().Contain(new[]
        {
            "Módulo de Reprodução & IATF",
            "Módulo Sanitário & Vacinação",
            "Modo Offline PWA em Curral"
        });
    }

    [Fact]
    public async Task GetSubscriptionPlansQueryHandler_WithProvider_ShouldReturnProviderPlans()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var plan = PlanCatalog.Create("custom", "Plano Personalizado", "Desc").Value;
        var version = plan.CreateVersion("v1.0", 250m, 200m, 1200, features: new[]
        {
            PlanFeature.Create("Modules.Growth", "Módulo Pesagem Balança", true)
        }).Value;
        plan.PublishVersion(version.Id, adminId);
        _db.PlanCatalogs.Add(plan);
        await _db.SaveChangesAsync();

        var handler = new GetSubscriptionPlansQueryHandler(_provider);

        // Act
        var result = await handler.Handle(new GetSubscriptionPlansQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Name.Should().Be("Plano Personalizado");
        result.Value.First().MonthlyPrice.Should().Be(250m);
        result.Value.First().Features!.First().Name.Should().Be("Módulo Pesagem Balança");
    }
}
