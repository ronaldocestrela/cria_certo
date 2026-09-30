using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class UpdateTenantForAdminCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;

    public UpdateTenantForAdminCommandHandlerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<TenancyDbContext>().UseSqlite(_connection).Options;
        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Update_CurrentPeriodEndUtc_Successfully()
    {
        var tenantId = Guid.NewGuid();
        var originalTenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Esperança",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            State = "MT",
            City = "Sinop",
            Capacity = 500,
            SubscribedPlan = "Starter",
            Status = "Active",
            CurrentPeriodEndUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _dbContext.Tenants.Add(originalTenant);
        await _dbContext.SaveChangesAsync();

        var newRenewalDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var handler = new UpdateTenantForAdminCommandHandler(_dbContext);
        var command = new UpdateTenantForAdminCommand(
            tenantId,
            "Fazenda Esperança Atualizada",
            null,
            "12.345.678/0001-90",
            null,
            "MT",
            "Sinop",
            "",
            100,
            500,
            "Corte",
            null,
            null,
            null,
            null,
            CurrentPeriodEndUtc: newRenewalDate,
            UpdateCurrentPeriodEnd: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentPeriodEndUtc.Should().Be(newRenewalDate);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenantId);
        updatedTenant.CurrentPeriodEndUtc.Should().Be(newRenewalDate);
        updatedTenant.Name.Should().Be("Fazenda Esperança Atualizada");
    }

    [Fact]
    public async Task Handle_Should_Preserve_Existing_Cnpj_And_Owners_When_Masked()
    {
        var tenantId = Guid.NewGuid();
        var originalTenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Modelo",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            TechnicalOwnerName = "João Silva",
            TechnicalOwnerEmail = "joao.silva@fazenda.com.br",
            CommercialOwnerName = "Maria Souza",
            CommercialOwnerEmail = "maria.souza@fazenda.com.br",
            State = "GO",
            City = "Rio Verde",
            Capacity = 500,
            SubscribedPlan = "Starter",
            Status = "Active"
        };
        _dbContext.Tenants.Add(originalTenant);
        await _dbContext.SaveChangesAsync();

        var newRenewalDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var handler = new UpdateTenantForAdminCommandHandler(_dbContext);

        // Simulated request sent by UI containing masked values
        var command = new UpdateTenantForAdminCommand(
            tenantId,
            "Fazenda Modelo",
            null,
            "12.***.***/0001-**", // Masked CNPJ
            null,
            "GO",
            "Rio Verde",
            "",
            200,
            500,
            "Corte",
            "J*** o", // Masked Technical Owner Name
            "j***a@fazenda.com.br", // Masked Technical Owner Email
            "M*** a", // Masked Commercial Owner Name
            "m***a@fazenda.com.br", // Masked Commercial Owner Email
            CurrentPeriodEndUtc: newRenewalDate,
            UpdateCurrentPeriodEnd: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentPeriodEndUtc.Should().Be(newRenewalDate);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenantId);
        // Sensitive data must NOT be overwritten with asterisks
        updatedTenant.CNPJ.Should().Be("12.345.678/0001-90");
        updatedTenant.CnpjNormalized.Should().Be("12345678000190");
        updatedTenant.TechnicalOwnerName.Should().Be("João Silva");
        updatedTenant.TechnicalOwnerEmail.Should().Be("joao.silva@fazenda.com.br");
        updatedTenant.CommercialOwnerName.Should().Be("Maria Souza");
        updatedTenant.CommercialOwnerEmail.Should().Be("maria.souza@fazenda.com.br");
        updatedTenant.CurrentPeriodEndUtc.Should().Be(newRenewalDate);
    }

    [Fact]
    public async Task Validator_Should_Accept_Masked_Cnpj_And_Reject_Invalid_Unmasked_Cnpj()
    {
        var validator = new UpdateTenantForAdminCommandValidator(_dbContext);

        var validMaskedCommand = new UpdateTenantForAdminCommand(
            Guid.NewGuid(), "Fazenda Teste", null, "12.***.***/0001-**", null,
            "MS", "Campo Grande", "", 100, 100, "Corte", null, null, null, null);

        var validResult = await validator.ValidateAsync(validMaskedCommand);
        validResult.IsValid.Should().BeTrue();

        var invalidUnmaskedCommand = new UpdateTenantForAdminCommand(
            Guid.NewGuid(), "Fazenda Teste", null, "12345", null,
            "MS", "Campo Grande", "", 100, 100, "Corte", null, null, null, null);

        var invalidResult = await validator.ValidateAsync(invalidUnmaskedCommand);
        invalidResult.IsValid.Should().BeFalse();
        invalidResult.Errors.Should().Contain(e => e.PropertyName == "CNPJ");
    }

    [Fact]
    public async Task Handle_Should_Sync_With_StripePaymentService_When_Tenant_Has_StripeSubscriptionId()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = "sub_test_stripe_123";
        var originalTenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Stripe Sync",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            State = "GO",
            City = "Jataí",
            Capacity = 500,
            SubscribedPlan = "Pro",
            Status = "Active",
            StripeSubscriptionId = subscriptionId,
            CurrentPeriodEndUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _dbContext.Tenants.Add(originalTenant);
        await _dbContext.SaveChangesAsync();

        var stripePaymentService = new TestStripePaymentService();
        var newBillingDate = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var handler = new UpdateTenantForAdminCommandHandler(_dbContext, stripePaymentService);
        var command = new UpdateTenantForAdminCommand(
            tenantId,
            "Fazenda Stripe Sync",
            null,
            "12.345.678/0001-90",
            null,
            "GO",
            "Jataí",
            "",
            300,
            500,
            "Corte",
            null,
            null,
            null,
            null,
            CurrentPeriodEndUtc: newBillingDate,
            UpdateCurrentPeriodEnd: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentPeriodEndUtc.Should().Be(newBillingDate);

        stripePaymentService.LastSubscriptionIdUpdated.Should().Be(subscriptionId);
        stripePaymentService.LastBillingDateUpdated.Should().Be(newBillingDate);

        var updatedTenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenantId);
        updatedTenant.CurrentPeriodEndUtc.Should().Be(newBillingDate);

        var history = await _dbContext.SubscriptionHistories
            .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.ActionType == SubscriptionActionType.Renewal);
        history.Should().NotBeNull();
        history!.Justification.Should().Contain("Data de cobrança/renovação alterada administrativamente");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_StripePaymentService_Returns_Error()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = "sub_test_stripe_error";
        var originalTenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Stripe Error",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            State = "GO",
            City = "Jataí",
            Capacity = 500,
            SubscribedPlan = "Pro",
            Status = "Active",
            StripeSubscriptionId = subscriptionId,
            CurrentPeriodEndUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _dbContext.Tenants.Add(originalTenant);
        await _dbContext.SaveChangesAsync();

        var expectedError = Error.Failure("Stripe.UpdateFailed", "Subscription cannot be updated.");
        var stripePaymentService = new TestStripePaymentService { ReturnError = expectedError };
        var newBillingDate = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var handler = new UpdateTenantForAdminCommandHandler(_dbContext, stripePaymentService);
        var command = new UpdateTenantForAdminCommand(
            tenantId,
            "Fazenda Stripe Error",
            null,
            "12.345.678/0001-90",
            null,
            "GO",
            "Jataí",
            "",
            300,
            500,
            "Corte",
            null,
            null,
            null,
            null,
            CurrentPeriodEndUtc: newBillingDate,
            UpdateCurrentPeriodEnd: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Stripe.UpdateFailed");
        result.Error.Message.Should().Be("Subscription cannot be updated.");
    }

    private sealed class TestStripePaymentService : IStripePaymentService
    {
        public string? LastSubscriptionIdUpdated { get; private set; }
        public DateTime? LastBillingDateUpdated { get; private set; }
        public Error? ReturnError { get; set; }

        public Task<string> GetOrCreateCustomerAsync(Tenant tenant, User user, CancellationToken cancellationToken = default) =>
            Task.FromResult("cus_test");

        public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
            Tenant tenant, User user, string planId, string planName, string billingCycle,
            decimal unitAmount, string successUrl, string cancelUrl, string? priceId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CheckoutSessionResult("cs_test", "https://checkout.stripe.com/test"));

        public Task<CustomerPortalSessionResult> CreateCustomerPortalSessionAsync(
            Tenant tenant, string returnUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CustomerPortalSessionResult("https://billing.stripe.com/portal"));

        public Task<StripeWebhookResult> ProcessWebhookAsync(
            string jsonPayload, string stripeSignatureHeader, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StripeWebhookResult(true, "test", null));

        public Task<Result> UpdateSubscriptionBillingDateAsync(
            string subscriptionId, DateTime newBillingDateUtc, CancellationToken cancellationToken = default)
        {
            LastSubscriptionIdUpdated = subscriptionId;
            LastBillingDateUpdated = newBillingDateUtc;

            if (ReturnError != null)
            {
                return Task.FromResult(Result.Failure(ReturnError));
            }

            return Task.FromResult(Result.Success());
        }
    }
}
