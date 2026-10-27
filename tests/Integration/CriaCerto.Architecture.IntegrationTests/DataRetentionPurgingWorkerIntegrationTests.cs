using CriaCerto.Api.BackgroundServices;
using CriaCerto.Api.Configuration;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.DataRetention.Commands;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.DataRetention;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class DataRetentionPurgingWorkerIntegrationTests : IDisposable
{
    private readonly SqliteConnection _tenancyConnection;
    private readonly SqliteConnection _backofficeConnection;
    private readonly ServiceProvider _serviceProvider;
    private readonly TenancyDbContext _tenancyDbContext;
    private readonly BackofficeDbContext _backofficeDbContext;

    public DataRetentionPurgingWorkerIntegrationTests()
    {
        _tenancyConnection = new SqliteConnection("Filename=:memory:");
        _tenancyConnection.Open();

        _backofficeConnection = new SqliteConnection("Filename=:memory:");
        _backofficeConnection.Open();

        var services = new ServiceCollection();

        // 1. Database Contexts
        services.AddDbContext<TenancyDbContext>(options =>
            options.UseSqlite(_tenancyConnection));
        services.AddScoped<ITenancyDbContext>(sp => sp.GetRequiredService<TenancyDbContext>());

        services.AddDbContext<BackofficeDbContext>(options =>
            options.UseSqlite(_backofficeConnection));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<BackofficeDbContext>());

        // 2. MediatR handlers
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(PurgeExpiredTenancyTokensAndInvitesCommand).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(PurgeExpiredAdminSessionsCommand).Assembly);
        });

        // 3. Logger & Options
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var defaultOptions = new DataRetentionPurgingOptions
        {
            IntervalHours = 24,
            InitialDelaySeconds = 60,
            TokenGraceDays = 7,
            InviteGraceDays = 30,
            AdminSessionGraceDays = 7,
            DryRun = false
        };

        var optionsMonitorMock = Substitute.For<IOptionsMonitor<DataRetentionPurgingOptions>>();
        optionsMonitorMock.CurrentValue.Returns(defaultOptions);
        services.AddSingleton(optionsMonitorMock);

        services.AddSingleton<DataRetentionPurgingBackgroundService>();

        _serviceProvider = services.BuildServiceProvider();

        _tenancyDbContext = _serviceProvider.GetRequiredService<TenancyDbContext>();
        _tenancyDbContext.Database.EnsureCreated();

        _backofficeDbContext = _serviceProvider.GetRequiredService<BackofficeDbContext>();
        _backofficeDbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _tenancyDbContext.Dispose();
        _backofficeDbContext.Dispose();
        _serviceProvider.Dispose();
        _tenancyConnection.Close();
        _tenancyConnection.Dispose();
        _backofficeConnection.Close();
        _backofficeConnection.Dispose();
    }

    [Fact]
    public async Task ProcessDataPurgePassSafelyAsync_Should_Purge_Tenancy_And_Backoffice_Stale_Data()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var worker = _serviceProvider.GetRequiredService<DataRetentionPurgingBackgroundService>();

        // Arrange Tenancy
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Fazenda Expurgo", CNPJ = "11223344000199" };
        _tenancyDbContext.Tenants.Add(tenant);

        var staleUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "usuario.antigo@expurgo.com",
            FullName = "Usuario Antigo",
            EmailConfirmationToken = "expired-email-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-14),
            PasswordResetToken = "expired-reset-token",
            PasswordResetTokenExpiresAt = now.AddDays(-10)
        };
        var activeUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "usuario.ativo@expurgo.com",
            FullName = "Usuario Ativo",
            EmailConfirmationToken = "active-email-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(1)
        };
        _tenancyDbContext.Users.AddRange(staleUser, activeUser);

        var staleInvite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "abandonado@convidado.com",
            Role = UserRole.OperadorCurral,
            InviteToken = "stale-invite-token",
            CreatedAt = now.AddDays(-60),
            ExpiresAt = now.AddDays(-45),
            IsAccepted = false
        };
        var activeInvite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "ativo@convidado.com",
            Role = UserRole.Veterinario,
            InviteToken = "active-invite-token",
            CreatedAt = now.AddDays(-5),
            ExpiresAt = now.AddDays(2),
            IsAccepted = false
        };
        _tenancyDbContext.TeamInvites.AddRange(staleInvite, activeInvite);
        await _tenancyDbContext.SaveChangesAsync();

        // Arrange Backoffice
        var adminId = Guid.NewGuid();
        var staleRevokedSession = AdminSession.Create(
            adminId, "revoked-token", "revoked-refresh", "127.0.0.1", "Agent",
            TimeSpan.FromHours(-250), TimeSpan.FromHours(-200));
        staleRevokedSession.Revoke();

        var activeAdminSession = AdminSession.Create(
            adminId, "active-token", "active-refresh", "127.0.0.1", "Agent",
            TimeSpan.FromHours(2), TimeSpan.FromHours(10));

        _backofficeDbContext.AdminSessions.AddRange(staleRevokedSession, activeAdminSession);
        await _backofficeDbContext.SaveChangesAsync();

        // Act
        var result = await worker.ProcessDataPurgePassSafelyAsync(referenceDateUtc: now, dryRun: false, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DryRun.Should().BeFalse();

        // Tenancy assertions
        result.Tenancy.ClearedEmailConfirmationTokens.Should().Be(1);
        result.Tenancy.ClearedPasswordResetTokens.Should().Be(1);
        result.Tenancy.PurgedTeamInvites.Should().Be(1);

        _tenancyDbContext.ChangeTracker.Clear();
        _backofficeDbContext.ChangeTracker.Clear();

        var dbStaleUser = await _tenancyDbContext.Users.AsNoTracking().FirstAsync(u => u.Id == staleUser.Id);
        dbStaleUser.EmailConfirmationToken.Should().BeNull();
        dbStaleUser.PasswordResetToken.Should().BeNull();

        var dbActiveUser = await _tenancyDbContext.Users.FirstAsync(u => u.Id == activeUser.Id);
        dbActiveUser.EmailConfirmationToken.Should().Be("active-email-token");

        var dbInvites = await _tenancyDbContext.TeamInvites.ToListAsync();
        dbInvites.Should().HaveCount(1);
        dbInvites.Should().Contain(i => i.Id == activeInvite.Id);

        // Backoffice assertions
        result.Backoffice.TotalPurged.Should().Be(1);
        result.Backoffice.PurgedRevokedSessions.Should().Be(1);

        var dbSessions = await _backofficeDbContext.AdminSessions.ToListAsync();
        dbSessions.Should().HaveCount(1);
        dbSessions.Should().Contain(s => s.Id == activeAdminSession.Id);

        var auditLog = await _backofficeDbContext.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("ADMIN_SESSIONS_PURGED");
        auditLog.Category.Should().Be(AuditCategory.Compliance);
        auditLog.Severity.Should().Be(AuditSeverity.Low);
        auditLog.RecordHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessDataPurgePassSafelyAsync_WhenDryRun_Should_Not_Mutate_Any_Database()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var worker = _serviceProvider.GetRequiredService<DataRetentionPurgingBackgroundService>();

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Fazenda DryRun", CNPJ = "99887766000111" };
        _tenancyDbContext.Tenants.Add(tenant);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "dryrun@user.com",
            FullName = "Usuario DryRun",
            EmailConfirmationToken = "expired-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-20)
        };
        _tenancyDbContext.Users.Add(user);

        var staleInvite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "convidado.dryrun@teste.com",
            Role = UserRole.OperadorCurral,
            InviteToken = "stale-invite",
            CreatedAt = now.AddDays(-50),
            ExpiresAt = now.AddDays(-35),
            IsAccepted = false
        };
        _tenancyDbContext.TeamInvites.Add(staleInvite);
        await _tenancyDbContext.SaveChangesAsync();

        var adminId = Guid.NewGuid();
        var staleSession = AdminSession.Create(
            adminId, "dryrun-session", "dryrun-refresh", "127.0.0.1", "Agent",
            TimeSpan.FromHours(-300), TimeSpan.FromHours(-250));
        staleSession.Revoke();
        _backofficeDbContext.AdminSessions.Add(staleSession);
        await _backofficeDbContext.SaveChangesAsync();

        // Act
        var result = await worker.ProcessDataPurgePassSafelyAsync(referenceDateUtc: now, dryRun: true, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DryRun.Should().BeTrue();
        result.Tenancy.ClearedEmailConfirmationTokens.Should().Be(1);
        result.Tenancy.PurgedTeamInvites.Should().Be(1);
        result.Backoffice.TotalPurged.Should().Be(1);

        // Check Tenancy persistence didn't change
        var dbUser = await _tenancyDbContext.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        dbUser.EmailConfirmationToken.Should().Be("expired-token");

        var dbInvite = await _tenancyDbContext.TeamInvites.AsNoTracking().FirstOrDefaultAsync(i => i.Id == staleInvite.Id);
        dbInvite.Should().NotBeNull();

        // Check Backoffice persistence didn't change
        var dbSession = await _backofficeDbContext.AdminSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staleSession.Id);
        dbSession.Should().NotBeNull();

        var auditCount = await _backofficeDbContext.AuditLogs.CountAsync();
        auditCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessDataPurgePassSafelyAsync_WhenCancelled_Should_Return_Null_Gracefully()
    {
        // Arrange
        var worker = _serviceProvider.GetRequiredService<DataRetentionPurgingBackgroundService>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await worker.ProcessDataPurgePassSafelyAsync(cancellationToken: cts.Token);

        // Assert
        result.Should().BeNull();
    }
}
