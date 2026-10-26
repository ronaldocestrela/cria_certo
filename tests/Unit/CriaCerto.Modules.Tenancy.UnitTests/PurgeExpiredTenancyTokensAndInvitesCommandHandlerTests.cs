using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.DataRetention;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class PurgeExpiredTenancyTokensAndInvitesCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public PurgeExpiredTenancyTokensAndInvitesCommandHandlerTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Purge_Expired_Tokens_And_Stale_Invites()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Fazenda Primavera", CNPJ = "12.345.678/0001-77" };
        _dbContext.Tenants.Add(tenant);

        // 1. User with tokens expired > 7 days
        var userWithOldTokens = new User
        {
            Id = Guid.NewGuid(),
            Email = "velho@primavera.com",
            FullName = "Usuario Velho",
            EmailConfirmationToken = "old-email-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-10),
            PasswordResetToken = "old-reset-token",
            PasswordResetTokenExpiresAt = now.AddDays(-15)
        };

        // 2. User with tokens expired only 2 days ago (within grace period)
        var userWithRecentTokens = new User
        {
            Id = Guid.NewGuid(),
            Email = "recente@primavera.com",
            FullName = "Usuario Recente",
            EmailConfirmationToken = "recent-email-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-2),
            PasswordResetToken = null,
            PasswordResetTokenExpiresAt = null
        };

        _dbContext.Users.AddRange(userWithOldTokens, userWithRecentTokens);

        // 3. TeamInvite expired 40 days ago (> 30 grace days) and unaccepted -> Must be purged
        var staleInvite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "convidado.abandonado@externo.com",
            Role = UserRole.OperadorCurral,
            InviteToken = "stale-invite-token",
            CreatedAt = now.AddDays(-50),
            ExpiresAt = now.AddDays(-40),
            IsAccepted = false
        };

        // 4. TeamInvite expired 10 days ago (< 30 grace days) and unaccepted -> Must be preserved
        var recentExpiredInvite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "convidado.recente@externo.com",
            Role = UserRole.Zootecnista,
            InviteToken = "recent-expired-token",
            CreatedAt = now.AddDays(-15),
            ExpiresAt = now.AddDays(-10),
            IsAccepted = false
        };

        // 5. TeamInvite accepted -> Must be preserved even if expired
        var acceptedInvite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "convidado.aceito@externo.com",
            Role = UserRole.Veterinario,
            InviteToken = "accepted-token",
            CreatedAt = now.AddDays(-60),
            ExpiresAt = now.AddDays(-50),
            IsAccepted = true,
            AcceptedAt = now.AddDays(-55)
        };

        _dbContext.TeamInvites.AddRange(staleInvite, recentExpiredInvite, acceptedInvite);
        await _dbContext.SaveChangesAsync();

        var handler = new PurgeExpiredTenancyTokensAndInvitesCommandHandler(_dbContext);
        var command = new PurgeExpiredTenancyTokensAndInvitesCommand(ReferenceDateUtc: now, TokenGraceDays: 7, InviteGraceDays: 30, DryRun: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.ClearedEmailConfirmationTokens.Should().Be(1);
        summary.ClearedPasswordResetTokens.Should().Be(1);
        summary.PurgedTeamInvites.Should().Be(1);
        summary.DryRun.Should().BeFalse();

        // Check database state
        var dbUserOld = await _dbContext.Users.FirstAsync(u => u.Id == userWithOldTokens.Id);
        dbUserOld.EmailConfirmationToken.Should().BeNull();
        dbUserOld.EmailConfirmationTokenExpiresAt.Should().BeNull();
        dbUserOld.PasswordResetToken.Should().BeNull();
        dbUserOld.PasswordResetTokenExpiresAt.Should().BeNull();

        var dbUserRecent = await _dbContext.Users.FirstAsync(u => u.Id == userWithRecentTokens.Id);
        dbUserRecent.EmailConfirmationToken.Should().Be("recent-email-token");

        var remainingInvites = await _dbContext.TeamInvites.ToListAsync();
        remainingInvites.Should().HaveCount(2);
        remainingInvites.Should().NotContain(i => i.Id == staleInvite.Id);
        remainingInvites.Should().Contain(i => i.Id == recentExpiredInvite.Id);
        remainingInvites.Should().Contain(i => i.Id == acceptedInvite.Id);
    }

    [Fact]
    public async Task Handle_WhenDryRun_Should_Not_Mutate_Database()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Fazenda Teste", CNPJ = "12.345.678/0001-99" };
        _dbContext.Tenants.Add(tenant);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "teste@teste.com",
            FullName = "Usuario Teste",
            EmailConfirmationToken = "expired-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-20)
        };
        _dbContext.Users.Add(user);

        var staleInvite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "convidado@teste.com",
            Role = UserRole.OperadorCurral,
            InviteToken = "stale-token",
            CreatedAt = now.AddDays(-50),
            ExpiresAt = now.AddDays(-35),
            IsAccepted = false
        };
        _dbContext.TeamInvites.Add(staleInvite);
        await _dbContext.SaveChangesAsync();

        var handler = new PurgeExpiredTenancyTokensAndInvitesCommandHandler(_dbContext);
        var command = new PurgeExpiredTenancyTokensAndInvitesCommand(ReferenceDateUtc: now, TokenGraceDays: 7, InviteGraceDays: 30, DryRun: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DryRun.Should().BeTrue();
        result.Value.ClearedEmailConfirmationTokens.Should().Be(1);
        result.Value.PurgedTeamInvites.Should().Be(1);

        // Re-query: nothing should have been modified
        var dbUser = await _dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        dbUser.EmailConfirmationToken.Should().Be("expired-token");

        var dbInvite = await _dbContext.TeamInvites.AsNoTracking().FirstOrDefaultAsync(i => i.Id == staleInvite.Id);
        dbInvite.Should().NotBeNull();
    }
}
