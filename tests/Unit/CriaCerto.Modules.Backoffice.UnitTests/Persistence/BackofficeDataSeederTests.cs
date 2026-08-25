using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Persistence;

public class BackofficeDataSeederTests
{
    private static (BackofficeDbContext DbContext, SqliteConnection Connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new BackofficeDbContext(options);
        dbContext.Database.EnsureCreated();

        return (dbContext, connection);
    }

    [Fact]
    public async Task SeedIamAsync_WhenSavedBeforePlans_ShouldAllowLoginWithoutPlanCatalog()
    {
        var (dbContext, connection) = CreateDbContext();
        using (connection)
        using (dbContext)
        {
            var passwordHasher = new PasswordHasherService();

            await BackofficeDataSeeder.SeedIamAsync(dbContext, passwordHasher);
            await dbContext.SaveChangesAsync();

            (await dbContext.AdminUsers.CountAsync()).Should().Be(1);
            (await dbContext.PlanCatalogs.CountAsync()).Should().Be(0);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var handler = new AuthenticateAdminUserCommandHandler(
                dbContext,
                passwordHasher,
                new BackofficeTokenService(configuration),
                new TotpService());

            var result = await handler.Handle(
                new AuthenticateAdminUserCommand(
                    BackofficeDataSeeder.MasterAdminEmail,
                    BackofficeDataSeeder.MasterAdminPassword,
                    null,
                    "127.0.0.1",
                    "UnitTestAgent"),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task SeedAsync_WithResetBootstrapAdminPassword_ShouldRehashExistingAdmin()
    {
        var (dbContext, connection) = CreateDbContext();
        using (connection)
        using (dbContext)
        {
            var passwordHasher = new PasswordHasherService();
            await BackofficeDataSeeder.SeedAsync(dbContext, passwordHasher);

            var staleHash = passwordHasher.HashPassword("StalePassword123!");
            var admin = await dbContext.AdminUsers.SingleAsync();
            admin.UpdatePasswordHash(staleHash);
            await dbContext.SaveChangesAsync();

            passwordHasher.VerifyPassword(BackofficeDataSeeder.MasterAdminPassword, admin.PasswordHash)
                .Should().BeFalse();

            await BackofficeDataSeeder.SeedAsync(
                dbContext,
                passwordHasher,
                resetBootstrapAdminPassword: true);

            var refreshedAdmin = await dbContext.AdminUsers.SingleAsync();
            passwordHasher.VerifyPassword(BackofficeDataSeeder.MasterAdminPassword, refreshedAdmin.PasswordHash)
                .Should().BeTrue();
        }
    }
}
