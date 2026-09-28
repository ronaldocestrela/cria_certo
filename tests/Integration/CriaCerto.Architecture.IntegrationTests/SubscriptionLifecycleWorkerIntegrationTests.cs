using CriaCerto.Api.BackgroundServices;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Options;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CriaCerto.Architecture.IntegrationTests;

public class SubscriptionLifecycleWorkerIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly TenancyDbContext _dbContext;

    public SubscriptionLifecycleWorkerIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddDbContext<TenancyDbContext>(options =>
            options.UseSqlite(_connection));

        services.AddScoped<ITenancyDbContext>(sp => sp.GetRequiredService<TenancyDbContext>());
        services.AddScoped<ISubscriptionLifecycleService, SubscriptionLifecycleService>();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IOptions<SubscriptionLifecycleOptions>>(
            Microsoft.Extensions.Options.Options.Create(new SubscriptionLifecycleOptions
            {
                IntervalHours = 6,
                PastDueGracePeriodDays = 7,
                BatchSize = 50
            }));

        var optionsMonitorMock = Substitute.For<IOptionsMonitor<SubscriptionLifecycleOptions>>();
        optionsMonitorMock.CurrentValue.Returns(new SubscriptionLifecycleOptions
        {
            IntervalHours = 6,
            PastDueGracePeriodDays = 7,
            BatchSize = 50
        });
        services.AddSingleton(optionsMonitorMock);

        services.AddSingleton<SubscriptionLifecycleWorker>();

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<TenancyDbContext>();
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessLifecyclePassSafelyAsync_Should_Execute_And_Suspend_Expired_Trial()
    {
        // Arrange
        var worker = _serviceProvider.GetRequiredService<SubscriptionLifecycleWorker>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Worker Integration",
            CNPJ = "99887766000100",
            CnpjNormalized = "99887766000100",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Trial),
            CurrentPeriodEndUtc = DateTime.UtcNow.AddHours(-2),
            SubscribedPlan = "Starter",
            Capacity = 300,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await worker.ProcessLifecyclePassSafelyAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.SuspendedTrials.Should().Be(1);

        var updated = await _dbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id);
        updated.Status.Should().Be(TenantLifecycle.ToStatusString(TenantStatus.Suspended));
        updated.StatusReason.Should().Be("Período de testes expirado.");
    }

    [Fact]
    public async Task ProcessLifecyclePassSafelyAsync_Should_Handle_Cancellation_Gracefully()
    {
        // Arrange
        var worker = _serviceProvider.GetRequiredService<SubscriptionLifecycleWorker>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await worker.ProcessLifecyclePassSafelyAsync(cts.Token);

        // Assert
        result.Should().BeNull();
    }
}
