using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateModularSubscription;
using CriaCerto.Modules.Tenancy.Application.Services;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Features.SubscriptionCheckout;

public class ModularCheckoutAndCapacityTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly MockStripePaymentService _stripeService;
    private readonly MockSender _sender;
    private readonly MockJwtService _jwtService;
    private readonly SubscriptionUrlValidator _urlValidator;

    public ModularCheckoutAndCapacityTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _stripeService = new MockStripePaymentService();
        _sender = new MockSender();
        _jwtService = new MockJwtService();
        _urlValidator = new SubscriptionUrlValidator();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Theory]
    [InlineData("TIER_500", 501, 500)]
    [InlineData("TIER_1500", 1501, 1500)]
    [InlineData("TIER_3000", 3001, 3000)]
    [InlineData("TIER_5000_PLUS", 10001, 10000)]
    public async Task CreateCheckoutSession_Should_Fail_With_HeadCapacityExceeded_When_ActiveHerd_Exceeds_Tier_MaxHeads(
        string targetTier, int activeHerdCount, int tierMaxHeads)
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Excedente",
            CNPJ = "12.345.678/0001-80",
            Status = "Trial",
            HeadTierCode = "TIER_500",
            Capacity = 500
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fazendaexcedente.com.br",
            FullName = "Produtor Rural",
            PasswordHash = "hash"
        };
        var userTenant = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = UserRole.Admin
        };
        user.UserTenants.Add(userTenant);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var herdCounter = new MockActiveHerdCounter(activeHerdCount);
        var handler = new CreateCheckoutSessionCommandHandler(
            _dbContext,
            _stripeService,
            _sender,
            _urlValidator,
            herdCounter);

        var command = new CreateCheckoutSessionCommand(
            TenantId: tenant.Id,
            UserId: user.Id,
            HeadTierCode: targetTier,
            SelectedModules: ["Cows", "Sanitary"],
            BillingCycle: "monthly"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("License.HeadCapacityExceeded");
        result.Error.Message.Should().Contain(activeHerdCount.ToString());
        result.Error.Message.Should().Contain(tierMaxHeads.ToString());
        result.Error.Message.Should().Contain(targetTier);
    }

    [Theory]
    [InlineData("TIER_500", 500)]
    [InlineData("TIER_1500", 1500)]
    [InlineData("TIER_3000", 3000)]
    [InlineData("TIER_5000_PLUS", 10000)]
    public async Task CreateCheckoutSession_Should_Succeed_When_ActiveHerd_IsWithin_Tier_Capacity(
        string targetTier, int activeHerdCount)
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Regular",
            CNPJ = "12.345.678/0001-81",
            Status = "Trial",
            HeadTierCode = "TIER_500",
            Capacity = 500
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fazendaregular.com.br",
            FullName = "Produtor Regular",
            PasswordHash = "hash"
        };
        var userTenant = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = UserRole.Admin
        };
        user.UserTenants.Add(userTenant);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var herdCounter = new MockActiveHerdCounter(activeHerdCount);
        var handler = new CreateCheckoutSessionCommandHandler(
            _dbContext,
            _stripeService,
            _sender,
            _urlValidator,
            herdCounter);

        var command = new CreateCheckoutSessionCommand(
            TenantId: tenant.Id,
            UserId: user.Id,
            HeadTierCode: targetTier,
            SelectedModules: ["Breeding", "Sanitary"], // Cows omitted, must be auto-included
            BillingCycle: "monthly"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stripeService.LastHeadTierCode.Should().Be(targetTier);
        _stripeService.LastSubscribedModules.Should().Contain("Cows");
        _stripeService.LastSubscribedModules.Should().Contain("Breeding");
        _stripeService.LastSubscribedModules.Should().Contain("Sanitary");
    }

    [Fact]
    public async Task UpdateModularSubscription_WhenActiveHerdExceedsTargetTier_And_GracePeriodDisabled_ShouldFail()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Downgrade Bloqueado",
            CNPJ = "12.345.678/0001-82",
            Status = "Active",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeCustomerId = "cus_downgrade_01",
            StripeSubscriptionId = "sub_downgrade_01"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fazendadowngrade.com.br",
            FullName = "Produtor Downgrade",
            PasswordHash = "hash"
        };
        var userTenant = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = UserRole.Admin
        };
        user.UserTenants.Add(userTenant);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // 750 cabeças ativas excedem TIER_500 (máximo 500)
        var herdCounter = new MockActiveHerdCounter(750);
        var handler = new UpdateModularSubscriptionCommandHandler(
            _dbContext,
            _stripeService,
            _jwtService,
            _sender,
            herdCounter);

        var command = new UpdateModularSubscriptionCommand(
            TenantId: tenant.Id,
            UserId: user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"],
            BillingCycle: "monthly",
            AllowGracePeriod: false
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("License.HeadCapacityExceeded");
        result.Error.Message.Should().Contain("750");
        result.Error.Message.Should().Contain("500");
    }

    [Fact]
    public async Task UpdateModularSubscription_WhenActiveHerdExceedsTargetTier_And_GracePeriodEnabled_ShouldActivateGracePeriod()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Downgrade Grace",
            CNPJ = "12.345.678/0001-83",
            Status = "Active",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeCustomerId = "cus_downgrade_02",
            StripeSubscriptionId = "sub_downgrade_02"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fazendagrace.com.br",
            FullName = "Produtor Grace",
            PasswordHash = "hash"
        };
        var userTenant = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = UserRole.Admin
        };
        user.UserTenants.Add(userTenant);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var herdCounter = new MockActiveHerdCounter(750);
        var handler = new UpdateModularSubscriptionCommandHandler(
            _dbContext,
            _stripeService,
            _jwtService,
            _sender,
            herdCounter);

        var command = new UpdateModularSubscriptionCommand(
            TenantId: tenant.Id,
            UserId: user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"],
            BillingCycle: "monthly",
            AllowGracePeriod: true
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("GracePeriodActive");
        result.Value.GracePeriodEndsAtUtc.Should().NotBeNull();
        result.Value.PendingHeadTierCode.Should().Be("TIER_500");

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Capacity.Should().Be(1500, "capacidade permanece preservada durante a carência de 14 dias");
        updatedTenant.StatusReason.Should().Contain("Grace Period ativo de 14 dias");

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.DowngradeGracePeriodStarted);
        history.SnapshotHeadCount.Should().Be(750);
    }

    [Fact]
    public async Task UpdateModularSubscription_WhenDowngradeWithinCapacity_ShouldApplyImmediately()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Downgrade Imediato",
            CNPJ = "12.345.678/0001-84",
            Status = "Active",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeCustomerId = "cus_downgrade_03",
            StripeSubscriptionId = "sub_downgrade_03"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fazendaimediato.com.br",
            FullName = "Produtor Imediato",
            PasswordHash = "hash"
        };
        var userTenant = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = UserRole.Admin
        };
        user.UserTenants.Add(userTenant);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // 450 cabeças ativas estão perfeitamente dentro de TIER_500 (max 500)
        var herdCounter = new MockActiveHerdCounter(450);
        var handler = new UpdateModularSubscriptionCommandHandler(
            _dbContext,
            _stripeService,
            _jwtService,
            _sender,
            herdCounter);

        var command = new UpdateModularSubscriptionCommand(
            TenantId: tenant.Id,
            UserId: user.Id,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"],
            BillingCycle: "monthly"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HeadTierCode.Should().Be("TIER_500");
        result.Value.HeadCapacity.Should().Be(500);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Capacity.Should().Be(500);
        updatedTenant.HeadTierCode.Should().Be("TIER_500");

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.DowngradeImmediate);
        history.SnapshotHeadCount.Should().Be(450);
    }

    [Fact]
    public async Task UpdateModularSubscription_WhenUpgradingTier_ShouldRecordUpgradeHistory()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Upgrade",
            CNPJ = "12.345.678/0001-85",
            Status = "Active",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            StripeCustomerId = "cus_upgrade_01",
            StripeSubscriptionId = "sub_upgrade_01"
        };
        tenant.SetSubscribedModules(["Cows"]);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fazendaupgrade.com.br",
            FullName = "Produtor Upgrade",
            PasswordHash = "hash"
        };
        var userTenant = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = UserRole.Admin
        };
        user.UserTenants.Add(userTenant);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var herdCounter = new MockActiveHerdCounter(400);
        var handler = new UpdateModularSubscriptionCommandHandler(
            _dbContext,
            _stripeService,
            _jwtService,
            _sender,
            herdCounter);

        var command = new UpdateModularSubscriptionCommand(
            TenantId: tenant.Id,
            UserId: user.Id,
            HeadTierCode: "TIER_3000",
            SelectedModules: ["Cows", "Sanitary", "Nutrition"],
            BillingCycle: "monthly"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HeadTierCode.Should().Be("TIER_3000");
        result.Value.HeadCapacity.Should().Be(3000);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Capacity.Should().Be(3000);
        updatedTenant.HeadTierCode.Should().Be("TIER_3000");

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.Upgrade);
        history.SnapshotHeadCount.Should().Be(400);
    }

    private sealed class MockActiveHerdCounter : ITenantActiveHerdCounter
    {
        private readonly int _count;
        public MockActiveHerdCounter(int count) => _count = count;
        public Task<int> GetActiveHeadCountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_count);
    }

    private sealed class MockJwtService : IJwtService
    {
        public string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin) => "mock-jwt";
        public string GeneratePartnerToken(User user) => "mock-partner-jwt";
    }

    private sealed class MockSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetModularSubscriptionCatalogQuery)
            {
                var catalog = GetSubscriptionPlansQueryHandler.GetDefaultModularCatalog();
                return Task.FromResult((TResponse)(object)Result.Success(catalog));
            }

            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotImplementedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class MockStripePaymentService : IStripePaymentService
    {
        public string? LastHeadTierCode { get; private set; }
        public IReadOnlyList<string>? LastSubscribedModules { get; private set; }

        public Task<string> GetOrCreateCustomerAsync(Tenant tenant, User user, CancellationToken cancellationToken = default) =>
            Task.FromResult("cus_mock_123");

        public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
            Tenant tenant, User user, string planId, string planName, string billingCycle, decimal unitAmount,
            string successUrl, string cancelUrl, string? priceId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CheckoutSessionResult("cs_123", "https://checkout.stripe.com/123"));

        public Task<CheckoutSessionResult> CreateModularCheckoutSessionAsync(
            Tenant tenant, User user, string headTierCode, int maxHeads, IReadOnlyList<string> subscribedModules,
            string billingCycle, IReadOnlyList<StripeModularLineItem> lineItems, string successUrl, string cancelUrl,
            string? planId = null, string? planName = null, CancellationToken cancellationToken = default)
        {
            LastHeadTierCode = headTierCode;
            LastSubscribedModules = subscribedModules;
            return Task.FromResult(new CheckoutSessionResult("cs_modular_123", "https://checkout.stripe.com/modular_123"));
        }

        public Task<CustomerPortalSessionResult> CreateCustomerPortalSessionAsync(Tenant tenant, string returnUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CustomerPortalSessionResult("https://portal.stripe.com/123"));

        public Task<StripeWebhookResult> ProcessWebhookAsync(string jsonPayload, string stripeSignatureHeader, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StripeWebhookResult(true, "mock", null));

        public Task<Result> UpdateSubscriptionBillingDateAsync(string subscriptionId, DateTime newBillingDateUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<StripeModularSubscriptionUpdateResult>> UpdateModularSubscriptionAsync(
            Tenant tenant, string headTierCode, int maxHeads, IReadOnlyList<string> subscribedModules,
            string billingCycle, IReadOnlyList<StripeModularLineItem> lineItems, CancellationToken cancellationToken = default)
        {
            LastHeadTierCode = headTierCode;
            LastSubscribedModules = subscribedModules;

            var itemIds = lineItems.ToDictionary(
                i => i.ModuleKey,
                i => $"si_mock_{i.ModuleKey.ToLowerInvariant()}",
                StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(Result.Success(new StripeModularSubscriptionUpdateResult(
                tenant.StripeSubscriptionId ?? "sub_mock",
                "active",
                itemIds)));
        }
    }
}
