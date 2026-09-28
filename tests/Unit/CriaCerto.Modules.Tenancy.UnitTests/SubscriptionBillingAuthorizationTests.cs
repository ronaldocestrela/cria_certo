using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionPortal;
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

    private sealed class FakeStripePaymentService : IStripePaymentService
    {
        public string? LastPlanId { get; private set; }
        public string? LastPlanName { get; private set; }

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
