using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionPortal;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateModularSubscription;
using CriaCerto.Modules.Tenancy.Application.Services;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class SubscriptionBillingAuthorizationTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly FakeStripePaymentService _stripeService;
    private readonly FakeSender _sender;
    private readonly SubscriptionUrlValidator _urlValidator;
    private readonly FakeJwtService _jwtService;

    public SubscriptionBillingAuthorizationTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _stripeService = new FakeStripePaymentService();
        _sender = new FakeSender();
        _urlValidator = new SubscriptionUrlValidator();
        _jwtService = new FakeJwtService();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Theory]
    [InlineData(UserRole.Veterinario)]
    [InlineData(UserRole.OperadorCurral)]
    [InlineData(UserRole.Zootecnista)]
    public async Task CreateCheckoutSession_Should_Fail_With_ForbiddenBilling_When_User_Is_Not_Admin(UserRole nonAdminRole)
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Branco",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Colaborador Operacional",
            Email = "operacional@ourobranco.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = nonAdminRole,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(tenant.Id, user.Id, "Pro");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ForbiddenBilling");
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Message.Should().Contain("Apenas administradores");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Succeed_When_User_Is_Admin()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Branco",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrador Fazenda",
            Email = "admin@ourobranco.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(tenant.Id, user.Id, "Pro");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Url.Should().Be("https://checkout.stripe.com/test-session");
        _stripeService.LastPlanId.Should().Be("Pro");
        _stripeService.LastPlanName.Should().Be("Plano Pro");
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenGivenCommercialPlanName_ShouldPassCanonicalPlanIdAndPlanName()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Branco",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrador Fazenda",
            Email = "admin@ourobranco.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(tenant.Id, user.Id, "Pro Fazenda");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stripeService.LastPlanId.Should().Be("Pro");
        _stripeService.LastPlanName.Should().Be("Plano Pro");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Fail_With_Validation_When_SuccessUrl_Or_CancelUrl_Is_OpenRedirect()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Branco",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrador Fazenda",
            Email = "admin@ourobranco.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            "Pro",
            "monthly",
            SuccessUrl: "https://evil-phishing.com/steal",
            CancelUrl: "http://localhost:8081/settings/subscription?canceled=true");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Subscription.InvalidRedirectUrl");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Fail_With_Conflict_When_Tenant_Already_Has_Active_StripeSubscription()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Branco",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            SubscribedPlan = "Pro",
            StripeCustomerId = "cus_existing_123",
            StripeSubscriptionId = "sub_active_123"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrador Fazenda",
            Email = "admin@ourobranco.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(tenant.Id, user.Id, "Enterprise");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(TenancyErrors.ActiveSubscriptionExists.Code);
        result.Error.Message.Should().Contain("assinatura ativa no Stripe");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Fail_With_Conflict_When_Tenant_Has_PastDue_StripeSubscription()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Santa Maria",
            CNPJ = "98.765.432/0001-10",
            Status = "PastDue",
            SubscribedPlan = "Pro",
            StripeCustomerId = "cus_existing_456",
            StripeSubscriptionId = "sub_pastdue_456"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrador Fazenda",
            Email = "admin@santamaria.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(tenant.Id, user.Id, "Pro");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(TenancyErrors.ActiveSubscriptionExists.Code);
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Succeed_When_Tenant_Has_Cancelled_StripeSubscription()
    {
        // Arrange - Previous subscription was cancelled, tenant re-subscribing
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Esperança",
            CNPJ = "11.222.333/0001-44",
            Status = "Cancelled",
            SubscribedPlan = "Starter",
            StripeCustomerId = "cus_existing_789",
            StripeSubscriptionId = "sub_old_cancelled"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrador Fazenda",
            Email = "admin@esperanca.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(tenant.Id, user.Id, "Pro");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Url.Should().Be("https://checkout.stripe.com/test-session");
    }

    [Theory]
    [InlineData(UserRole.Veterinario)]
    [InlineData(UserRole.OperadorCurral)]
    [InlineData(UserRole.Zootecnista)]
    public async Task CreatePortalSession_Should_Fail_With_ForbiddenBilling_When_User_Is_Not_Admin(UserRole nonAdminRole)
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Esperança",
            CNPJ = "98.765.432/0001-10",
            Status = "Active",
            SubscribedPlan = "Pro",
            StripeCustomerId = "cus_12345"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Técnico de Campo",
            Email = "campo@esperanca.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = nonAdminRole,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreatePortalSessionCommandHandler(_dbContext, _stripeService, _urlValidator);
        var command = new CreatePortalSessionCommand(tenant.Id, user.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ForbiddenBilling");
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Message.Should().Contain("Apenas administradores");
    }

    [Fact]
    public async Task CreatePortalSession_Should_Succeed_When_User_Is_Admin()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Esperança",
            CNPJ = "98.765.432/0001-10",
            Status = "Active",
            SubscribedPlan = "Pro",
            StripeCustomerId = "cus_12345"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Esperança",
            Email = "admin@esperanca.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreatePortalSessionCommandHandler(_dbContext, _stripeService, _urlValidator);
        var command = new CreatePortalSessionCommand(tenant.Id, user.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Url.Should().Be("https://billing.stripe.com/test-portal");
    }

    [Fact]
    public async Task CreatePortalSession_Should_Fail_With_Validation_When_ReturnUrl_Is_OpenRedirect()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Esperança",
            CNPJ = "98.765.432/0001-10",
            Status = "Active",
            SubscribedPlan = "Pro",
            StripeCustomerId = "cus_12345"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Esperança",
            Email = "admin@esperanca.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreatePortalSessionCommandHandler(_dbContext, _stripeService, _urlValidator);
        var command = new CreatePortalSessionCommand(tenant.Id, user.Id, ReturnUrl: "https://evil.com/fake-return");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Subscription.InvalidRedirectUrl");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Fail_When_User_Does_Not_Belong_To_Tenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Sol",
            CNPJ = "11.111.111/0001-11",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Usuário Externo",
            Email = "externo@fazenda.com",
            PasswordHash = "hash"
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(tenant.Id, user.Id, "Pro");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UnauthorizedTenant");
    }

    [Fact]
    public async Task CreatePortalSession_Should_Fail_When_User_Does_Not_Belong_To_Tenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Sol",
            CNPJ = "11.111.111/0001-11",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Usuário Externo",
            Email = "externo@fazenda.com",
            PasswordHash = "hash"
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new CreatePortalSessionCommandHandler(_dbContext, _stripeService, _urlValidator);
        var command = new CreatePortalSessionCommand(tenant.Id, user.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UnauthorizedTenant");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Include_Cows_Module_When_Omitted_In_SelectedModules()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Santa Maria",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            HeadTierCode = "TIER_500"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Modular",
            Email = "admin@santamaria.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Breeding", "Sanitary"],
            BillingCycle: "monthly");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stripeService.LastSubscribedModules.Should().NotBeNull();
        _stripeService.LastSubscribedModules.Should().Contain("Cows");
        _stripeService.LastSubscribedModules.Should().Contain("Breeding");
        _stripeService.LastSubscribedModules.Should().Contain("Sanitary");
        _stripeService.LastLineItems.Should().Contain(i => i.ModuleKey == "Cows");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Fail_When_CurrentHeadCount_Exceeds_Tier_MaxHeads()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Grande Porte",
            CNPJ = "12.345.678/0001-91",
            Status = "Active"
        };

        var unit = new ProductionUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = "UN-001",
            Name = "Retiro Principal",
            Capacity = 1000,
            CurrentHeadCount = 750
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Produtor",
            Email = "admin@grandeporte.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.ProductionUnits.Add(unit);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows", "Breeding"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("License.HeadCapacityExceeded");
        result.Error.Message.Should().Contain("750");
        result.Error.Message.Should().Contain("500");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Succeed_When_CurrentHeadCount_Is_Within_Tier_MaxHeads()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Pequeno Porte",
            CNPJ = "12.345.678/0001-92",
            Status = "Active"
        };

        var unit = new ProductionUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = "UN-001",
            Name = "Retiro A",
            Capacity = 500,
            CurrentHeadCount = 420
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Produtor",
            Email = "admin@pequenoporte.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.ProductionUnits.Add(unit);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows", "Breeding"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stripeService.LastHeadTierCode.Should().Be("TIER_500");
        _stripeService.LastMaxHeads.Should().Be(500);
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Assemble_MultiItem_LineItems_And_Metadata_For_All_Selected_Modules()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Esperança",
            CNPJ = "12.345.678/0001-93",
            Status = "Active"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Esperança",
            Email = "admin@esperanca.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_1500",
            SelectedModules: ["Cows", "Breeding", "Sanitary"],
            BillingCycle: "monthly");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stripeService.LastHeadTierCode.Should().Be("TIER_1500");
        _stripeService.LastMaxHeads.Should().Be(1500);
        _stripeService.LastSubscribedModules.Should().Equal(["Cows", "Breeding", "Sanitary"]);
        _stripeService.LastBillingCycle.Should().Be("monthly");
        _stripeService.LastLineItems.Should().HaveCount(3);

        var cowsItem = _stripeService.LastLineItems!.Single(i => i.ModuleKey == "Cows");
        cowsItem.UnitAmount.Should().Be(139m); // TIER_1500 Monthly Price for Cows
        cowsItem.Interval.Should().Be("month");

        var breedingItem = _stripeService.LastLineItems!.Single(i => i.ModuleKey == "Breeding");
        breedingItem.UnitAmount.Should().Be(89m); // TIER_1500 Monthly Price for Breeding
        breedingItem.Interval.Should().Be("month");

        var sanitaryItem = _stripeService.LastLineItems!.Single(i => i.ModuleKey == "Sanitary");
        sanitaryItem.UnitAmount.Should().Be(89m); // TIER_1500 Monthly Price for Sanitary
        sanitaryItem.Interval.Should().Be("month");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Support_Annual_Billing_Cycle_With_Annual_Prices()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Anual",
            CNPJ = "12.345.678/0001-94",
            Status = "Active"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Anual",
            Email = "admin@anual.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows", "Breeding"],
            BillingCycle: "annual");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stripeService.LastBillingCycle.Should().Be("annual");
        _stripeService.LastLineItems.Should().HaveCount(2);
        _stripeService.LastLineItems!.All(i => i.Interval == "year").Should().BeTrue();

        var cowsItem = _stripeService.LastLineItems!.Single(i => i.ModuleKey == "Cows");
        // TIER_500 Annual monthly equivalent is 63.20 -> * 12 = 758.40m
        cowsItem.UnitAmount.Should().Be(63.20m * 12);
        cowsItem.PriceId.Should().Be("price_cows_tier_500_annual");
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Fail_When_HeadTierCode_Is_Invalid()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Teste Tier Inválido",
            CNPJ = "12.345.678/0001-95",
            Status = "Active"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Inválido",
            Email = "admin@tierinvalido.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_NAO_EXISTENTE",
            SelectedModules: ["Cows"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.InvalidHeadTierCode.Code);
    }

    [Fact]
    public async Task CreateCheckoutSession_Should_Fail_When_SelectedModule_Is_Invalid()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Teste Módulo Inválido",
            CNPJ = "12.345.678/0001-96",
            Status = "Active"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Modulo Inválido",
            Email = "admin@moduloinvalido.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCheckoutSessionCommandHandler(_dbContext, _stripeService, _sender, _urlValidator);
        var command = new CreateCheckoutSessionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows", "ModuloInexistenteFicticio"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.InvalidModuleKey.Code);
    }

    [Theory]
    [InlineData(UserRole.Veterinario)]
    [InlineData(UserRole.OperadorCurral)]
    [InlineData(UserRole.Zootecnista)]
    public async Task UpdateModularSubscription_Should_Fail_With_ForbiddenBilling_When_User_Is_Not_Admin(UserRole nonAdminRole)
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Branco",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            StripeSubscriptionId = "sub_active_123"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Colaborador Operacional",
            Email = "operacional@ourobranco.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = nonAdminRole,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateModularSubscriptionCommandHandler(_dbContext, _stripeService, _jwtService, _sender);
        var command = new UpdateModularSubscriptionCommand(tenant.Id, user.Id, "TIER_1500", ["Cows", "Breeding"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ForbiddenBilling");
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task UpdateModularSubscription_Should_Fail_When_Tenant_Has_No_Active_Stripe_Subscription()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Sem Assinatura",
            CNPJ = "12.345.678/0001-91",
            Status = "Trial",
            StripeSubscriptionId = null
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Sem Sub",
            Email = "admin@semsub.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateModularSubscriptionCommandHandler(_dbContext, _stripeService, _jwtService, _sender);
        var command = new UpdateModularSubscriptionCommand(tenant.Id, user.Id, "TIER_1500", ["Cows"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.NoActiveSubscription.Code);
    }

    [Fact]
    public async Task UpdateModularSubscription_Should_Succeed_And_Update_Modules_When_Admin()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Progresso",
            CNPJ = "12.345.678/0001-92",
            Status = "Active",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            StripeSubscriptionId = "sub_active_progresso"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Progresso",
            Email = "admin@progresso.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateModularSubscriptionCommandHandler(_dbContext, _stripeService, _jwtService, _sender);
        var command = new UpdateModularSubscriptionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_1500",
            SelectedModules: ["Cows", "Breeding", "Sanitary"],
            BillingCycle: "annual");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HeadTierCode.Should().Be("TIER_1500");
        result.Value.HeadCapacity.Should().Be(1500);
        result.Value.SubscribedModules.Should().Contain(["Cows", "Breeding", "Sanitary"]);
        result.Value.Status.Should().Be("Active");
        result.Value.Token.Should().Be("fake-jwt-token");

        // Verifica estado atualizado do tenant no banco
        var updatedTenant = await _dbContext.Tenants.Include(t => t.SubscribedModules).FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.HeadTierCode.Should().Be("TIER_1500");
        updatedTenant.Capacity.Should().Be(1500);
        updatedTenant.SubscribedModules.Select(m => m.ModuleKey).Should().Contain(["Cows", "Breeding", "Sanitary"]);

        // Verifica histórico registrado
        var history = await _dbContext.SubscriptionHistories.FirstOrDefaultAsync(h => h.TenantId == tenant.Id);
        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.Upgrade);

        // Verifica envio ao Stripe
        _stripeService.LastUpdateHeadTierCode.Should().Be("TIER_1500");
        _stripeService.LastUpdateSubscribedModules.Should().Contain(["Cows", "Breeding", "Sanitary"]);
    }

    [Fact]
    public async Task UpdateModularSubscription_Should_Always_Include_Cows_Module_Even_If_Omitted()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Plantel Compulsório",
            CNPJ = "12.345.678/0001-93",
            Status = "Active",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            StripeSubscriptionId = "sub_active_plantel"
        };
        tenant.SetSubscribedModules(["Cows"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Plantel",
            Email = "admin@plantel.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateModularSubscriptionCommandHandler(_dbContext, _stripeService, _jwtService, _sender);
        // Usuário envia apenas "Breeding" sem "Cows"
        var command = new UpdateModularSubscriptionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Breeding"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubscribedModules.Should().Contain(PlanCapacityLimits.CoreModuleKey);
        result.Value.SubscribedModules.Should().Contain("Breeding");
    }

    [Fact]
    public async Task UpdateModularSubscription_Should_Fail_When_HeadCapacity_Exceeded_And_GracePeriod_Not_Allowed()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Excesso Cabeças",
            CNPJ = "12.345.678/0001-94",
            Status = "Active",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeSubscriptionId = "sub_active_excesso"
        };
        tenant.SetSubscribedModules(["Cows"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Excesso",
            Email = "admin@excesso.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var mockHerdCounter = new FakeHerdCounter(activeCattleCount: 750);
        var handler = new UpdateModularSubscriptionCommandHandler(_dbContext, _stripeService, _jwtService, _sender, mockHerdCounter);

        // Tenta downgrade para TIER_500 (capacidade 500) com 750 cabeças em estoque e sem AllowGracePeriod
        var command = new UpdateModularSubscriptionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"],
            AllowGracePeriod: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("License.HeadCapacityExceeded");
        result.Error.Message.Should().Contain("750");
        result.Error.Message.Should().Contain("500");
    }

    [Fact]
    public async Task UpdateModularSubscription_Should_Start_GracePeriod_When_HeadCapacity_Exceeded_And_GracePeriod_Allowed()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Grace Period",
            CNPJ = "12.345.678/0001-97",
            Status = "Active",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeSubscriptionId = "sub_active_grace"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Grace",
            Email = "admin@grace.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var mockHerdCounter = new FakeHerdCounter(activeCattleCount: 750);
        var handler = new UpdateModularSubscriptionCommandHandler(_dbContext, _stripeService, _jwtService, _sender, mockHerdCounter);

        var command = new UpdateModularSubscriptionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"],
            AllowGracePeriod: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("GracePeriodActive");
        result.Value.PendingHeadTierCode.Should().Be("TIER_500");
        result.Value.GracePeriodEndsAtUtc.Should().NotBeNull();
        result.Value.GracePeriodEndsAtUtc!.Value.Should().BeAfter(DateTime.UtcNow.AddDays(13));

        // Capacidade atual permanece preservada durante o grace period
        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Capacity.Should().Be(1500);
        updatedTenant.StatusReason.Should().Contain("Grace Period ativo de 14 dias");

        // Histórico de Grace Period registrado
        var history = await _dbContext.SubscriptionHistories.FirstOrDefaultAsync(h => h.TenantId == tenant.Id);
        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.DowngradeGracePeriodStarted);
        history.SnapshotHeadCount.Should().Be(750);
    }

    [Fact]
    public async Task UpdateModularSubscription_Should_Succeed_Immediate_Downgrade_When_Herd_Within_Target_Tier()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Downgrade Imediato",
            CNPJ = "12.345.678/0001-98",
            Status = "Active",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeSubscriptionId = "sub_active_downgrade_ok"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding", "Growth"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Downgrade OK",
            Email = "admin@downgradeok.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var mockHerdCounter = new FakeHerdCounter(activeCattleCount: 320); // 320 <= 500
        var handler = new UpdateModularSubscriptionCommandHandler(_dbContext, _stripeService, _jwtService, _sender, mockHerdCounter);

        var command = new UpdateModularSubscriptionCommand(
            tenant.Id,
            user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows", "Breeding"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Active");
        result.Value.HeadTierCode.Should().Be("TIER_500");
        result.Value.HeadCapacity.Should().Be(500);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Capacity.Should().Be(500);
        updatedTenant.HeadTierCode.Should().Be("TIER_500");

        var history = await _dbContext.SubscriptionHistories.FirstOrDefaultAsync(h => h.TenantId == tenant.Id);
        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.DowngradeImmediate);
    }

    private sealed class FakeHerdCounter : ITenantActiveHerdCounter
    {
        private readonly int _activeCattleCount;

        public FakeHerdCounter(int activeCattleCount)
        {
            _activeCattleCount = activeCattleCount;
        }

        public Task<int> GetActiveHeadCountAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_activeCattleCount);
        }
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin) => "fake-jwt-token";
        public string GeneratePartnerToken(User user) => "fake-partner-token";
    }

    private sealed class FakeStripePaymentService : IStripePaymentService
    {
        public string? LastPlanId { get; private set; }
        public string? LastPlanName { get; private set; }
        public string? LastHeadTierCode { get; private set; }
        public int LastMaxHeads { get; private set; }
        public IReadOnlyList<string>? LastSubscribedModules { get; private set; }
        public string? LastBillingCycle { get; private set; }
        public IReadOnlyList<StripeModularLineItem>? LastLineItems { get; private set; }
        public string? LastUpdateHeadTierCode { get; private set; }
        public IReadOnlyList<string>? LastUpdateSubscribedModules { get; private set; }

        public Task<string> GetOrCreateCustomerAsync(Tenant tenant, User user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("cus_fake_123");
        }

        public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
            Tenant tenant,
            User user,
            string planId,
            string planName,
            string billingCycle,
            decimal unitAmount,
            string successUrl,
            string cancelUrl,
            string? priceId = null,
            CancellationToken cancellationToken = default)
        {
            LastPlanId = planId;
            LastPlanName = planName;
            return Task.FromResult(new CheckoutSessionResult("cs_test_123", "https://checkout.stripe.com/test-session"));
        }

        public Task<CheckoutSessionResult> CreateModularCheckoutSessionAsync(
            Tenant tenant,
            User user,
            string headTierCode,
            int maxHeads,
            IReadOnlyList<string> subscribedModules,
            string billingCycle,
            IReadOnlyList<StripeModularLineItem> lineItems,
            string successUrl,
            string cancelUrl,
            string? planId = null,
            string? planName = null,
            CancellationToken cancellationToken = default)
        {
            LastPlanId = planId ?? headTierCode;
            LastPlanName = planName ?? headTierCode;
            LastHeadTierCode = headTierCode;
            LastMaxHeads = maxHeads;
            LastSubscribedModules = subscribedModules;
            LastBillingCycle = billingCycle;
            LastLineItems = lineItems;
            LastUpdateHeadTierCode = headTierCode;
            LastUpdateSubscribedModules = subscribedModules;
            return Task.FromResult(new CheckoutSessionResult("cs_test_123", "https://checkout.stripe.com/test-session"));
        }

        public Task<CustomerPortalSessionResult> CreateCustomerPortalSessionAsync(
            Tenant tenant,
            string returnUrl,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CustomerPortalSessionResult("https://billing.stripe.com/test-portal"));
        }

        public Task<StripeWebhookResult> ProcessWebhookAsync(
            string jsonPayload,
            string stripeSignatureHeader,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StripeWebhookResult(true, "checkout.session.completed", null));
        }

        public Task<Result> UpdateSubscriptionBillingDateAsync(
            string subscriptionId,
            DateTime newBillingDateUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }

        public Task<Result<StripeModularSubscriptionUpdateResult>> UpdateModularSubscriptionAsync(
            Tenant tenant,
            string headTierCode,
            int maxHeads,
            IReadOnlyList<string> subscribedModules,
            string billingCycle,
            IReadOnlyList<StripeModularLineItem> lineItems,
            CancellationToken cancellationToken = default)
        {
            LastHeadTierCode = headTierCode;
            LastMaxHeads = maxHeads;
            LastSubscribedModules = subscribedModules;
            LastBillingCycle = billingCycle;
            LastLineItems = lineItems;
            LastUpdateHeadTierCode = headTierCode;
            LastUpdateSubscribedModules = subscribedModules;

            var itemIds = lineItems.ToDictionary(
                i => i.ModuleKey,
                i => $"si_mock_{i.ModuleKey.ToLowerInvariant()}",
                StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(Result.Success(new StripeModularSubscriptionUpdateResult(
                tenant.StripeSubscriptionId ?? "sub_mock_123",
                "active",
                itemIds)));
        }
    }

    private sealed class FakeSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetSubscriptionPlansQuery)
            {
                var plans = new List<SubscriptionPlanDto>
                {
                    new("Starter", "Plano Starter", "Desc", 99m, 84m, 500, new List<string>(), false, null, null, null),
                    new("Pro", "Plano Pro", "Desc", 149m, 119m, 2500, new List<string>(), true, null, null, null)
                };

                return Task.FromResult((TResponse)(object)Result.Success(plans));
            }

            if (request is GetModularSubscriptionCatalogQuery)
            {
                var catalog = GetSubscriptionPlansQueryHandler.GetDefaultModularCatalog();
                return Task.FromResult((TResponse)(object)Result.Success(catalog));
            }

            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            throw new NotImplementedException();
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
