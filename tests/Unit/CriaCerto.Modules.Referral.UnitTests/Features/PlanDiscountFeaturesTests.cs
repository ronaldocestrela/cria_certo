using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Commands;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Queries;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class PlanDiscountFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;
    private readonly TestPlanCatalogLookupService _planCatalogLookup;

    public PlanDiscountFeaturesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ReferralDbContext(options);
        _dbContext.Database.EnsureCreated();

        _planCatalogLookup = new TestPlanCatalogLookupService();
    }

    private sealed class TestPlanCatalogLookupService : IPlanCatalogLookupService
    {
        public List<PlanLookupDto> Plans { get; set; } = new();

        public Task<IReadOnlyList<PlanLookupDto>> GetAvailablePlansAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlanLookupDto>>(Plans);
        }
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ConfigurePlanDiscountPolicy_WhenPolicyDoesNotExist_ShouldCreateNewPolicy()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new PlanLookupDto(planId, "PRO", "Plano Profissional", false);
        _planCatalogLookup.Plans = new List<PlanLookupDto> { plan };

        var handler = new ConfigurePlanDiscountPolicyCommandHandler(_dbContext, _planCatalogLookup);
        var command = new ConfigurePlanDiscountPolicyCommand(
            planId,
            DiscountType.Percentage,
            15m,
            100m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlanCatalogId.Should().Be(planId);
        result.Value.PlanCode.Should().Be("PRO");
        result.Value.DiscountType.Should().Be(DiscountType.Percentage);
        result.Value.DiscountValue.Should().Be(15m);
        result.Value.MaxDiscountCapPercentage.Should().Be(100m);
        result.Value.IsActive.Should().BeTrue();
        result.Value.HasConfiguredPolicy.Should().BeTrue();

        var saved = await _dbContext.PlanDiscountPolicies.FirstOrDefaultAsync(p => p.PlanCatalogId == planId);
        saved.Should().NotBeNull();
        saved!.DiscountValue.Should().Be(15m);
    }

    [Fact]
    public async Task ConfigurePlanDiscountPolicy_WhenPolicyAlreadyExists_ShouldUpdatePolicy()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPolicy = PlanReferralDiscountPolicy.Create(
            planId,
            "ENTERPRISE",
            DiscountType.FixedValue,
            100m,
            80m).Value;

        _dbContext.PlanDiscountPolicies.Add(existingPolicy);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanLookupDto(planId, "ENTERPRISE", "Plano Enterprise", false);
        _planCatalogLookup.Plans = new List<PlanLookupDto> { plan };

        var handler = new ConfigurePlanDiscountPolicyCommandHandler(_dbContext, _planCatalogLookup);
        var command = new ConfigurePlanDiscountPolicyCommand(
            planId,
            DiscountType.FixedValue,
            150m,
            90m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountValue.Should().Be(150m);
        result.Value.MaxDiscountCapPercentage.Should().Be(90m);

        var updated = await _dbContext.PlanDiscountPolicies.FirstOrDefaultAsync(p => p.PlanCatalogId == planId);
        updated!.DiscountValue.Should().Be(150m);
        updated.MaxDiscountCapPercentage.Should().Be(90m);
    }

    [Fact]
    public async Task ConfigurePlanDiscountPolicy_WhenPlanNotFoundInCatalog_ShouldReturnFailure()
    {
        // Arrange
        var planId = Guid.NewGuid();
        _planCatalogLookup.Plans = new List<PlanLookupDto>();

        var handler = new ConfigurePlanDiscountPolicyCommandHandler(_dbContext, _planCatalogLookup);
        var command = new ConfigurePlanDiscountPolicyCommand(
            planId,
            DiscountType.FixedValue,
            50m,
            100m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Policy.PlanCatalogRequired.Code);
    }

    [Fact]
    public async Task TogglePlanDiscountPolicyStatus_WhenPolicyExists_ShouldToggleActiveState()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var policy = PlanReferralDiscountPolicy.Create(
            planId,
            "PRO",
            DiscountType.Percentage,
            10m).Value;

        _dbContext.PlanDiscountPolicies.Add(policy);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanLookupDto(planId, "PRO", "Plano Profissional", false);
        _planCatalogLookup.Plans = new List<PlanLookupDto> { plan };

        var handler = new TogglePlanDiscountPolicyStatusCommandHandler(_dbContext, _planCatalogLookup);

        // Act 1: Deactivate
        var result1 = await handler.Handle(new TogglePlanDiscountPolicyStatusCommand(policy.Id), CancellationToken.None);

        // Assert 1
        result1.IsSuccess.Should().BeTrue();
        result1.Value.IsActive.Should().BeFalse();

        // Act 2: Reactivate
        var result2 = await handler.Handle(new TogglePlanDiscountPolicyStatusCommand(policy.Id), CancellationToken.None);

        // Assert 2
        result2.IsSuccess.Should().BeTrue();
        result2.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task TogglePlanDiscountPolicyStatus_WhenNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new TogglePlanDiscountPolicyStatusCommandHandler(_dbContext, _planCatalogLookup);

        // Act
        var result = await handler.Handle(new TogglePlanDiscountPolicyStatusCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Policy.NotFound.Code);
    }

    [Fact]
    public async Task GetPlanDiscountPolicies_ShouldReturnBothConfiguredAndUnconfiguredPlans()
    {
        // Arrange
        var plan1 = new PlanLookupDto(Guid.NewGuid(), "STARTER", "Plano Starter", false);
        var plan2 = new PlanLookupDto(Guid.NewGuid(), "PRO", "Plano Pro", false);

        var policy1 = PlanReferralDiscountPolicy.Create(
            plan1.Id,
            plan1.Code,
            DiscountType.FixedValue,
            50m,
            100m).Value;

        _dbContext.PlanDiscountPolicies.Add(policy1);
        await _dbContext.SaveChangesAsync();

        _planCatalogLookup.Plans = new List<PlanLookupDto> { plan1, plan2 };

        var handler = new GetPlanDiscountPoliciesQueryHandler(_dbContext, _planCatalogLookup);

        // Act
        var result = await handler.Handle(new GetPlanDiscountPoliciesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var item1 = result.Value.First(p => p.PlanCatalogId == plan1.Id);
        item1.HasConfiguredPolicy.Should().BeTrue();
        item1.DiscountValue.Should().Be(50m);
        item1.IsActive.Should().BeTrue();

        var item2 = result.Value.First(p => p.PlanCatalogId == plan2.Id);
        item2.HasConfiguredPolicy.Should().BeFalse();
        item2.PlanCode.Should().Be("PRO");
        item2.PlanName.Should().Be("Plano Pro");
    }
}
