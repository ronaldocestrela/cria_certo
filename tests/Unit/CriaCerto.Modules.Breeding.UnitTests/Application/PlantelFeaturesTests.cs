using System.Reflection;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Application.Contracts;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Features.BreedingOps;
using CriaCerto.Modules.Breeding.Application.Features.Plantel;
using CriaCerto.Modules.Breeding.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Breeding.UnitTests.Application;

public class PlantelFeaturesTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private static BreedingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BreedingDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new BreedingDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void CreateCowCommandValidator_WithValidCommand_ShouldNotHaveValidationErrors()
    {
        var validator = new CreateCowCommandValidator();
        var command = new CreateCowCommand("BR-990", "Nelore", DateTime.UtcNow.AddYears(-2), _tenantId, BodyConditionScore: 3.5m);

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateCowCommandValidator_WithInvalidBcs_ShouldHaveValidationError()
    {
        var validator = new CreateCowCommandValidator();
        var command = new CreateCowCommand("BR-990", "Nelore", DateTime.UtcNow.AddYears(-2), _tenantId, BodyConditionScore: 7.0m);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "BodyConditionScore");
    }

    [Fact]
    public void UpdateCowCommandValidator_WithEmptyId_ShouldHaveValidationError()
    {
        var validator = new UpdateCowCommandValidator();
        var command = new UpdateCowCommand(Guid.Empty, "BR-990", "Nelore", DateTime.UtcNow.AddYears(-2));

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Id");
    }

    [Fact]
    public async Task CreateCowCommandHandler_WhenEarTagAlreadyExistsInSameTenant_ShouldReturnConflict()
    {
        using var dbContext = CreateInMemoryDbContext();
        var handler = new CreateCowCommandHandler(dbContext);

        var command1 = new CreateCowCommand("BR-200", "Nelore", DateTime.UtcNow.AddYears(-2), _tenantId);
        await handler.Handle(command1, CancellationToken.None);

        var command2 = new CreateCowCommand("BR-200", "Angus", DateTime.UtcNow.AddYears(-3), _tenantId);
        var result = await handler.Handle(command2, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cow.EarTagAlreadyExists");
    }

    [Fact]
    public async Task CreateCowCommandHandler_WhenValid_ShouldSaveCow()
    {
        using var dbContext = CreateInMemoryDbContext();
        var handler = new CreateCowCommandHandler(dbContext);

        var command = new CreateCowCommand("BR-201", "Nelore PO", DateTime.UtcNow.AddYears(-2), _tenantId, Nickname: "Famosa", BodyConditionScore: 4.0m);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EarTag.Should().Be("BR-201");
        result.Value.Nickname.Should().Be("Famosa");
        result.Value.BodyConditionScore.Should().Be(4.0m);
    }

    [Fact]
    public async Task CreateCowCommandHandler_WhenActiveHerdReachesTenantHeadCapacity_ShouldReturnHeadCapacityExceededError()
    {
        using var dbContext = CreateInMemoryDbContext();
        var tenantContext = new FakeTenantContext(_tenantId, headCapacity: 2);

        var handler = new CreateCowCommandHandler(dbContext, tenantContext);

        (await handler.Handle(new CreateCowCommand("BR-301", "Nelore", DateTime.UtcNow.AddYears(-2), _tenantId), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await handler.Handle(new CreateCowCommand("BR-302", "Nelore", DateTime.UtcNow.AddYears(-2), _tenantId), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var thirdResult = await handler.Handle(new CreateCowCommand("BR-303", "Nelore", DateTime.UtcNow.AddYears(-2), _tenantId), CancellationToken.None);

        thirdResult.IsFailure.Should().BeTrue();
        thirdResult.Error.Code.Should().Be("License.HeadCapacityExceeded");
    }

    private sealed class FakeTenantContext(Guid tenantId, int headCapacity) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public string? SubscribedPlan => "Starter";
        public string? HeadTier => "TIER_500";
        public int HeadCapacity { get; } = headCapacity;
        public IReadOnlySet<string> SubscribedModules { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows" };
    }

    [Fact]
    public async Task ListBullsQueryHandler_ShouldReturnOnlyActiveBullsOfTenant()
    {
        using var dbContext = CreateInMemoryDbContext();

        var bull1 = Cow.Create("TOURO-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId, nickname: "Titan", category: "Reprodutor").Value;
        var bull2 = Cow.Create("TOURO-02", "Angus", DateTime.UtcNow.AddYears(-4), _tenantId, nickname: "Brutus", category: "Touro").Value;
        var femaleCow = Cow.Create("VACA-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId, nickname: "Mimosa", category: "Matriz").Value;
        var otherTenantBull = Cow.Create("TOURO-99", "Nelore", DateTime.UtcNow.AddYears(-3), Guid.NewGuid(), category: "Reprodutor").Value;

        dbContext.Cows.AddRange(bull1, bull2, femaleCow, otherTenantBull);
        await dbContext.SaveChangesAsync();

        var handler = new ListBullsQueryHandler(dbContext);
        var result = await handler.Handle(new ListBullsQuery(_tenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(b => b.EarTag).Should().Contain(new[] { "TOURO-01", "TOURO-02" });
    }

    [Fact]
    public void PlantelBaseCommandsAndQueries_ShouldRequireCowsModule_WhileBreedingOpsShouldRequireBreedingModule()
    {
        typeof(CreateCowCommand).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Cows");
        typeof(UpdateCowCommand).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Cows");
        typeof(GetCowQuery).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Cows");
        typeof(ListCowsQuery).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Cows");
        typeof(ListBullsQuery).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Cows");

        typeof(RegisterIatfProtocolCommand).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Breeding");
        typeof(RegisterPregnancyDiagnosisCommand).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Breeding");
        typeof(GetIatfProtocolsQuery).GetCustomAttribute<RequiresModuleAttribute>()?.ModuleName.Should().Be("Breeding");
    }
}
