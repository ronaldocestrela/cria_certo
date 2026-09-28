using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Options;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class SubscriptionLifecycleServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;
    private readonly SubscriptionLifecycleService _service;
    private readonly SubscriptionLifecycleOptions _options;

    public SubscriptionLifecycleServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var dbOptions = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TenancyDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();

        _options = new SubscriptionLifecycleOptions
        {
            PastDueGracePeriodDays = 7,
            BatchSize = 100
        };

        _service = new SubscriptionLifecycleService(
            _dbContext,
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<SubscriptionLifecycleService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task ExecutePassAsync_Should_SuspendTrialTenant_When_CurrentPeriodEndIsInPast()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Trial, currentPeriodEndUtc: DateTime.UtcNow.AddMinutes(-30));
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SuspendedTrials.Should().Be(1);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.Suspended));
        updatedTenant.StatusReason.Should().Be("Período de testes expirado.");

        var history = await _dbContext.SubscriptionHistories.FirstOrDefaultAsync(h => h.TenantId == tenant.Id);
        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.Suspended);
        history.Justification.Should().Contain("Período de testes expirado");
    }

    [Fact]
    public async Task ExecutePassAsync_Should_KeepTrialTenant_When_CurrentPeriodEndIsInFuture()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Trial, currentPeriodEndUtc: DateTime.UtcNow.AddDays(5));
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SuspendedTrials.Should().Be(0);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.Trial));
    }

    [Fact]
    public async Task ExecutePassAsync_Should_KeepTrialTenant_When_CurrentPeriodEndIsNull()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Trial, currentPeriodEndUtc: null);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SuspendedTrials.Should().Be(0);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.Trial));
    }

    [Fact]
    public async Task ExecutePassAsync_Should_SuspendPastDueTenant_When_GracePeriodExceeded()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.PastDue);
        tenant.StatusChangedAtUtc = DateTime.UtcNow.AddDays(-8);
        tenant.UpdatedAtUtc = DateTime.UtcNow.AddDays(-8);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SuspendedPastDue.Should().Be(1);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.Suspended));
        updatedTenant.StatusReason.Should().Be("Inadimplência não regularizada após prazo de tolerância.");

        var history = await _dbContext.SubscriptionHistories.FirstOrDefaultAsync(h => h.TenantId == tenant.Id);
        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.Suspended);
        history.Justification.Should().Contain("Inadimplência não regularizada");
    }

    [Fact]
    public async Task ExecutePassAsync_Should_KeepPastDueTenant_When_WithinGracePeriod()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.PastDue);
        tenant.StatusChangedAtUtc = DateTime.UtcNow.AddDays(-3);
        tenant.UpdatedAtUtc = DateTime.UtcNow.AddDays(-3);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SuspendedPastDue.Should().Be(0);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.PastDue));
    }

    [Fact]
    public async Task ExecutePassAsync_Should_NotSuspend_When_TenantIsProtected()
    {
        // Arrange
        var trialTenant = CreateTenant(TenantStatus.Trial, currentPeriodEndUtc: DateTime.UtcNow.AddMinutes(-30));
        trialTenant.IsProtected = true;

        var pastDueTenant = CreateTenant(TenantStatus.PastDue);
        pastDueTenant.StatusChangedAtUtc = DateTime.UtcNow.AddDays(-10);
        pastDueTenant.UpdatedAtUtc = DateTime.UtcNow.AddDays(-10);
        pastDueTenant.IsProtected = true;

        _dbContext.Tenants.AddRange(trialTenant, pastDueTenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProtectedSkipped.Should().Be(2);
        result.Value.SuspendedTrials.Should().Be(0);
        result.Value.SuspendedPastDue.Should().Be(0);

        var reloadedTrial = await _dbContext.Tenants.FirstAsync(t => t.Id == trialTenant.Id);
        reloadedTrial.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.Trial));

        var reloadedPastDue = await _dbContext.Tenants.FirstAsync(t => t.Id == pastDueTenant.Id);
        reloadedPastDue.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.PastDue));

        var histories = await _dbContext.SubscriptionHistories.ToListAsync();
        histories.Should().HaveCount(2);
        histories.Should().OnlyContain(h => h.Justification.Contains("IsProtected=true"));
    }

    [Fact]
    public async Task ExecutePassAsync_Should_Ignore_Active_And_AlreadySuspendedTenants()
    {
        // Arrange
        var activeTenant = CreateTenant(TenantStatus.Active);
        var suspendedTenant = CreateTenant(TenantStatus.Suspended);
        var cancelledTenant = CreateTenant(TenantStatus.Cancelled);

        _dbContext.Tenants.AddRange(activeTenant, suspendedTenant, cancelledTenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SuspendedTrials.Should().Be(0);
        result.Value.SuspendedPastDue.Should().Be(0);
    }

    [Fact]
    public async Task ExecutePassAsync_Should_BeIdempotent()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Trial, currentPeriodEndUtc: DateTime.UtcNow.AddMinutes(-10));
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act 1
        var firstRun = await _service.ExecutePassAsync();
        // Act 2
        var secondRun = await _service.ExecutePassAsync();

        // Assert
        firstRun.Value.SuspendedTrials.Should().Be(1);
        secondRun.Value.SuspendedTrials.Should().Be(0);
        (await _dbContext.SubscriptionHistories.CountAsync(h => h.TenantId == tenant.Id)).Should().Be(1);
    }

    private static int _cnpjCounter = 1000;

    private static Tenant CreateTenant(TenantStatus status, DateTime? currentPeriodEndUtc = null)
    {
        var count = Interlocked.Increment(ref _cnpjCounter);
        var cnpjNum = $"12345{count:D9}";
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Fazenda Teste Ciclo {count}",
            CNPJ = cnpjNum,
            CnpjNormalized = cnpjNum,
            Status = TenantLifecycle.ToStatusString(status),
            SubscribedPlan = "Starter",
            Capacity = 500,
            State = "MS",
            City = "Campo Grande",
            StateRegistration = "123456",
            Type = "Corte",
            CurrentPeriodEndUtc = currentPeriodEndUtc,
            StatusChangedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
