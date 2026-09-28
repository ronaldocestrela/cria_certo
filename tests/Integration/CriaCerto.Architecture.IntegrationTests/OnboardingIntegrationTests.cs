using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.CreateTenant;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using CriaCerto.Modules.Tenancy.Application.Features.RegisterUser;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class OnboardingIntegrationTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public OnboardingIntegrationTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _jwtService = new TestJwtService("valid_jwt_onboarding_token");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task FullOnboardingFlow_Register_OnboardFarm_Login_ShouldSucceedWithoutNoTenantAssociationError()
    {
        // Step 1: User Register
        var registerHandler = new RegisterUserCommandHandler(_dbContext);
        var registerCommand = new RegisterUserCommand("Produtor Teste", "produtor.onboarding@fazenda.com.br", "Senha@123", "67999998888");
        var registerResult = await registerHandler.Handle(registerCommand, CancellationToken.None);

        registerResult.IsSuccess.Should().BeTrue();
        var userId = registerResult.Value.Id;

        // Step 2: Attempt Login BEFORE onboarding (Should fail with Auth.NoTenantAssociation)
        var loginHandler = new LoginCommandHandler(_dbContext, _jwtService);
        var loginPreOnboard = await loginHandler.Handle(new LoginCommand("produtor.onboarding@fazenda.com.br", "Senha@123"), CancellationToken.None);

        loginPreOnboard.IsFailure.Should().BeTrue();
        loginPreOnboard.Error.Code.Should().Be("Auth.NoTenantAssociation");

        // Step 3: Complete Onboarding Wizard (CreateTenantCommand)
        var createTenantHandler = new CreateTenantCommandHandler(_dbContext, _jwtService, new NoOpTenantDatabaseProvisioner());
        var createTenantCommand = new CreateTenantCommand(
            userId,
            "Fazenda Vista Alegre",
            "98.765.432/0001-10",
            "MS",
            "Maracaju",
            "IE7890",
            1500,
            "Starter",
            500
        );

        var onboardingResult = await createTenantHandler.Handle(createTenantCommand, CancellationToken.None);

        onboardingResult.IsSuccess.Should().BeTrue();
        onboardingResult.Value.Token.Should().Be("valid_jwt_onboarding_token");
        onboardingResult.Value.UserId.Should().Be(userId);

        // Step 4: Login AFTER onboarding (Should NOW succeed with valid JWT token)
        var loginPostOnboard = await loginHandler.Handle(new LoginCommand("produtor.onboarding@fazenda.com.br", "Senha@123"), CancellationToken.None);

        loginPostOnboard.IsSuccess.Should().BeTrue();
        loginPostOnboard.Value.Token.Should().Be("valid_jwt_onboarding_token");
        loginPostOnboard.Value.UserId.Should().Be(userId);
    }

    private sealed class TestJwtService : IJwtService
    {
        private readonly string _token;
        public TestJwtService(string token) => _token = token;
        public string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin) => _token;
    }

    private sealed class NoOpTenantDatabaseProvisioner : CriaCerto.BuildingBlocks.Abstractions.Tenancy.ITenantDatabaseProvisioner
    {
        public Task EnsureTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
