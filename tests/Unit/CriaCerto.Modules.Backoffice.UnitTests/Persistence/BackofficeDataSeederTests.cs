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

    [Fact]
    public async Task SeedAsync_WithCustomMasterAdminOptions_ShouldCreateAdminWithCustomCredentials()
    {
        var (dbContext, connection) = CreateDbContext();
        using (connection)
        using (dbContext)
        {
            var passwordHasher = new PasswordHasherService();
            var customOptions = new MasterAdminOptions(
                Email: "custom.master@criacerto.com.br",
                Password: "SuperCustomAdminSecret999!",
                Name: "Administrador Customizado");

            await BackofficeDataSeeder.SeedAsync(
                dbContext,
                passwordHasher,
                masterAdminOptions: customOptions);

            var admin = await dbContext.AdminUsers.SingleAsync();
            admin.Email.Should().Be("custom.master@criacerto.com.br");
            admin.Name.Should().Be("Administrador Customizado");
            passwordHasher.VerifyPassword("SuperCustomAdminSecret999!", admin.PasswordHash).Should().BeTrue();

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
                    "custom.master@criacerto.com.br",
                    "SuperCustomAdminSecret999!",
                    null,
                    "127.0.0.1",
                    "UnitTestAgent"),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task SeedAsync_WithCustomMasterAdminOptions_WhenResetPasswordRequested_ShouldUpdateToCustomPassword()
    {
        var (dbContext, connection) = CreateDbContext();
        using (connection)
        using (dbContext)
        {
            var passwordHasher = new PasswordHasherService();
            var initialOptions = new MasterAdminOptions(
                Email: "ops.admin@criacerto.com.br",
                Password: "OldPassword123!",
                Name: "Ops Admin");

            await BackofficeDataSeeder.SeedAsync(
                dbContext,
                passwordHasher,
                masterAdminOptions: initialOptions);

            var updatedOptions = new MasterAdminOptions(
                Email: "ops.admin@criacerto.com.br",
                Password: "BrandNewSecurePassword456!",
                Name: "Ops Admin");

            await BackofficeDataSeeder.SeedAsync(
                dbContext,
                passwordHasher,
                resetBootstrapAdminPassword: true,
                masterAdminOptions: updatedOptions);

            var admin = await dbContext.AdminUsers.SingleAsync();
            passwordHasher.VerifyPassword("BrandNewSecurePassword456!", admin.PasswordHash).Should().BeTrue();
            passwordHasher.VerifyPassword("OldPassword123!", admin.PasswordHash).Should().BeFalse();
        }
    }

    [Fact]
    public async Task SeedIamAsync_WithNullOrEmptyOptions_ShouldFallbackToDefaults()
    {
        var (dbContext, connection) = CreateDbContext();
        using (connection)
        using (dbContext)
        {
            var passwordHasher = new PasswordHasherService();
            var emptyOptions = new MasterAdminOptions(
                Email: "   ",
                Password: null,
                Name: "");

            await BackofficeDataSeeder.SeedIamAsync(
                dbContext,
                passwordHasher,
                masterAdminOptions: emptyOptions);
            await dbContext.SaveChangesAsync();

            var admin = await dbContext.AdminUsers.SingleAsync();
            admin.Email.Should().Be(BackofficeDataSeeder.MasterAdminEmail);
            admin.Name.Should().Be(BackofficeDataSeeder.MasterAdminName);
            passwordHasher.VerifyPassword(BackofficeDataSeeder.MasterAdminPassword, admin.PasswordHash)
                .Should().BeTrue();
        }
    }
}

