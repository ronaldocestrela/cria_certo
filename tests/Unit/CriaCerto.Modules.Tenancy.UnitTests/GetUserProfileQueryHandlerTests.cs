using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.GetUserProfile;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class GetUserProfileQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public GetUserProfileQueryHandlerTests()
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
    public async Task Handle_Should_Return_Profile_With_Tenants_When_User_Exists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Santa Maria",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            SubscribedPlan = "Enterprise",
            Status = "Active"
        };

        var user = new User
        {
            Id = userId,
            Email = "produtor@santamaria.com.br",
            FullName = "Carlos Eduardo",
            PhoneNumber = "+5567999887766",
            IsEmailConfirmed = true,
            EmailConfirmedAtUtc = DateTime.UtcNow.AddDays(-10),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            TermsAcceptedAtUtc = DateTime.UtcNow.AddDays(-30),
            PrivacyPolicyAcceptedAtUtc = DateTime.UtcNow.AddDays(-30),
            TermsVersion = "1.0",
            PrivacyPolicyVersion = "1.0"
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            User = user,
            TenantId = tenantId,
            Tenant = tenant,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow.AddDays(-30)
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new GetUserProfileQueryHandler(_dbContext);
        var query = new GetUserProfileQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.Email.Should().Be("produtor@santamaria.com.br");
        result.Value.FullName.Should().Be("Carlos Eduardo");
        result.Value.PhoneNumber.Should().Be("+5567999887766");
        result.Value.IsEmailConfirmed.Should().BeTrue();
        result.Value.TermsVersion.Should().Be("1.0");
        result.Value.PrivacyPolicyVersion.Should().Be("1.0");
        result.Value.AssociatedTenants.Should().HaveCount(1);
        result.Value.AssociatedTenants[0].TenantName.Should().Be("Fazenda Santa Maria");
        result.Value.AssociatedTenants[0].Role.Should().Be("Admin");
        result.Value.AssociatedTenants[0].SubscribedPlan.Should().Be("Enterprise");
    }

    [Fact]
    public async Task Handle_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        var handler = new GetUserProfileQueryHandler(_dbContext);
        var query = new GetUserProfileQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }
}
