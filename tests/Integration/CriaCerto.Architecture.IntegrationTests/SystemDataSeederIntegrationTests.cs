using CriaCerto.Api.Seeders;
using CriaCerto.BuildingBlocks.Application.Features.GetReferenceBreeds;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using CriaCerto.Modules.Sanitary.Application.Features.GetVaccineCalendar;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class SystemDataSeederIntegrationTests : IDisposable
{
    private readonly SqliteConnection _foundationConnection;
    private readonly SqliteConnection _sanitaryConnection;
    private readonly SqliteConnection _backofficeConnection;

    private readonly FoundationDbContext _foundationDb;
    private readonly SanitaryDbContext _sanitaryDb;
    private readonly BackofficeDbContext _backofficeDb;
    private readonly PasswordHasherService _passwordHasher;

    public SystemDataSeederIntegrationTests()
    {
        _foundationConnection = new SqliteConnection("Filename=:memory:");
        _foundationConnection.Open();

        _sanitaryConnection = new SqliteConnection("Filename=:memory:");
        _sanitaryConnection.Open();

        _backofficeConnection = new SqliteConnection("Filename=:memory:");
        _backofficeConnection.Open();

        var foundationOptions = new DbContextOptionsBuilder<FoundationDbContext>()
            .UseSqlite(_foundationConnection)
            .Options;

        var sanitaryOptions = new DbContextOptionsBuilder<SanitaryDbContext>()
            .UseSqlite(_sanitaryConnection)
            .Options;

        var backofficeOptions = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(_backofficeConnection)
            .Options;

        _foundationDb = new FoundationDbContext(foundationOptions);
        _sanitaryDb = new SanitaryDbContext(sanitaryOptions);
        _backofficeDb = new BackofficeDbContext(backofficeOptions);
        _passwordHasher = new PasswordHasherService();

        _foundationDb.Database.EnsureCreated();
        _sanitaryDb.Database.EnsureCreated();
        _backofficeDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _foundationDb.Dispose();
        _sanitaryDb.Dispose();
        _backofficeDb.Dispose();

        _foundationConnection.Close();
        _foundationConnection.Dispose();

        _sanitaryConnection.Close();
        _sanitaryConnection.Dispose();

        _backofficeConnection.Close();
        _backofficeConnection.Dispose();
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseIsEmpty_ShouldPopulateBreedsVaccinesAndBackofficeData()
    {
        // Act
        await SystemDataSeeder.SeedDataAsync(_foundationDb, _sanitaryDb, _backofficeDb, _passwordHasher, CancellationToken.None);

        // Assert - Breeds
        var breeds = await _foundationDb.BovineBreeds.ToListAsync();
        breeds.Should().NotBeEmpty();
        breeds.Should().Contain(b => b.Name == "Nelore" && b.Code == "NEL");
        breeds.Should().Contain(b => b.Name == "Angus" && b.Code == "ANG");

        // Assert - Vaccines
        var vaccines = await _sanitaryDb.VaccineReferences.ToListAsync();
        vaccines.Should().NotBeEmpty();
        vaccines.Should().Contain(v => v.DiseaseName == "Febre Aftosa" && v.IsMandatoryMAPA);

        // Assert - Backoffice Permissions
        var permissions = await _backofficeDb.Permissions.ToListAsync();
        permissions.Should().NotBeEmpty();
        permissions.Select(p => p.Name).Should().Contain(BackofficePermissions.AllPermissions);

        // Assert - Backoffice Admin Roles
        var roles = await _backofficeDb.AdminRoles.Include(r => r.Permissions).ToListAsync();
        roles.Should().NotBeEmpty();
        roles.Select(r => r.Name).Should().Contain(BackofficeRoles.AllRoles);

        var platformOwnerRole = roles.First(r => r.Name == BackofficeRoles.PlatformOwner);
        platformOwnerRole.Permissions.Should().HaveCount(BackofficePermissions.AllPermissions.Count);

        // Assert - Backoffice Master Admin User
        var adminUsers = await _backofficeDb.AdminUsers.Include(u => u.Roles).ToListAsync();
        adminUsers.Should().HaveCount(1);

        var masterAdmin = adminUsers.First();
        masterAdmin.Email.Should().Be("admin@criacerto.com.br");
        masterAdmin.IsActive.Should().BeTrue();
        masterAdmin.Roles.Should().Contain(r => r.Name == BackofficeRoles.PlatformOwner);
        _passwordHasher.VerifyPassword("AdminPassword123!", masterAdmin.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_WhenExecutedMultipleTimes_ShouldBeIdempotentWithoutDuplicates()
    {
        // First seed
        await SystemDataSeeder.SeedDataAsync(_foundationDb, _sanitaryDb, _backofficeDb, _passwordHasher, CancellationToken.None);
        var breedsInitialCount = await _foundationDb.BovineBreeds.CountAsync();
        var vaccinesInitialCount = await _sanitaryDb.VaccineReferences.CountAsync();
        var permissionsInitialCount = await _backofficeDb.Permissions.CountAsync();
        var rolesInitialCount = await _backofficeDb.AdminRoles.CountAsync();
        var usersInitialCount = await _backofficeDb.AdminUsers.CountAsync();

        // Second & Third seeds
        await SystemDataSeeder.SeedDataAsync(_foundationDb, _sanitaryDb, _backofficeDb, _passwordHasher, CancellationToken.None);
        await SystemDataSeeder.SeedDataAsync(_foundationDb, _sanitaryDb, _backofficeDb, _passwordHasher, CancellationToken.None);

        // Assert
        (await _foundationDb.BovineBreeds.CountAsync()).Should().Be(breedsInitialCount);
        (await _sanitaryDb.VaccineReferences.CountAsync()).Should().Be(vaccinesInitialCount);
        (await _backofficeDb.Permissions.CountAsync()).Should().Be(permissionsInitialCount);
        (await _backofficeDb.AdminRoles.CountAsync()).Should().Be(rolesInitialCount);
        (await _backofficeDb.AdminUsers.CountAsync()).Should().Be(usersInitialCount);
    }

    [Fact]
    public async Task GetReferenceBreedsQuery_ShouldReturnPopulatedBreedsWithResultSuccess()
    {
        await SystemDataSeeder.SeedDataAsync(_foundationDb, _sanitaryDb, _backofficeDb, _passwordHasher, CancellationToken.None);

        var handler = new GetReferenceBreedsQueryHandler(_foundationDb);
        var result = await handler.Handle(new GetReferenceBreedsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Any(b => b.Name == "Nelore").Should().BeTrue();
    }

    [Fact]
    public async Task GetVaccineCalendarQuery_ShouldReturnPopulatedCalendarWithResultSuccess()
    {
        await SystemDataSeeder.SeedDataAsync(_foundationDb, _sanitaryDb, _backofficeDb, _passwordHasher, CancellationToken.None);

        var handler = new GetVaccineCalendarQueryHandler(_sanitaryDb);
        var result = await handler.Handle(new GetVaccineCalendarQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Any(v => v.DiseaseName == "Febre Aftosa").Should().BeTrue();
    }
}
