using System.Security.Cryptography;
using System.Text;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class StripeWebhookSecurityTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public StripeWebhookSecurityTests()
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

    private StripePaymentService CreateService(StripeOptions options)
    {
        return new StripePaymentService(
            _dbContext,
            Options.Create(options),
            NullLogger<StripePaymentService>.Instance);
    }

    private static string GenerateStripeSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"t={timestamp},v1={hashHex}";
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessWebhookAsync_WhenWebhookSecretIsMissingOrEmpty_ShouldFailImmediately(string? secret)
    {
        // Arrange
        var options = new StripeOptions
        {
            ApiKey = "sk_test_123",
            WebhookSecret = secret!
        };
        var service = CreateService(options);
        var payload = "{\"id\":\"evt_test_123\",\"type\":\"invoice.paid\",\"object\":\"event\"}";
        var signature = "t=123456,v1=abcdef";

        // Act
        var result = await service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("WebhookSecret");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessWebhookAsync_WhenSignatureHeaderIsMissingOrEmpty_ShouldFailImmediately(string? signature)
    {
        // Arrange
        var options = new StripeOptions
        {
            ApiKey = "sk_test_123",
            WebhookSecret = "whsec_test_secret_123"
        };
        var service = CreateService(options);
        var payload = "{\"id\":\"evt_test_123\",\"type\":\"invoice.paid\",\"object\":\"event\"}";

        // Act
        var result = await service.ProcessWebhookAsync(payload, signature!);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Stripe-Signature");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSignatureIsInvalid_ShouldFailVerification()
    {
        // Arrange
        var options = new StripeOptions
        {
            ApiKey = "sk_test_123",
            WebhookSecret = "whsec_test_secret_123"
        };
        var service = CreateService(options);
        var payload = "{\"id\":\"evt_test_123\",\"type\":\"invoice.paid\",\"object\":\"event\"}";
        var fakeSignature = "t=1234567890,v1=badbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadb";

        // Act
        var result = await service.ProcessWebhookAsync(payload, fakeSignature);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Match(m => m.Contains("Assinatura inválida") || m.Contains("signature"));
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSignatureIsValid_ShouldSuccessfullyProcessWebhook()
    {
        // Arrange
        var secret = "whsec_test_valid_secret_key_456";
        var options = new StripeOptions
        {
            ApiKey = "sk_test_123",
            WebhookSecret = secret
        };
        var service = CreateService(options);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // A minimal valid Stripe event JSON payload
        var payload = $"{{\"id\":\"evt_test_valid\",\"type\":\"payment_intent.created\",\"object\":\"event\",\"data\":{{\"object\":{{\"id\":\"pi_test\"}}}}}}";
        var validSignature = GenerateStripeSignature(payload, secret, timestamp);

        // Act
        var result = await service.ProcessWebhookAsync(payload, validSignature);

        // Assert
        result.Success.Should().BeTrue();
        result.EventType.Should().Be("payment_intent.created");
    }

    [Fact]
    public void AddTenancyInfrastructure_InProductionWithoutWebhookSecret_ShouldFailValidationOnStart()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ASPNETCORE_ENVIRONMENT", "Production" },
            { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=test;Trusted_Connection=True;" },
            { "Stripe:ApiKey", "sk_test_123" },
            { "Stripe:WebhookSecret", "" }
        };

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        CriaCerto.Modules.Tenancy.Infrastructure.DependencyInjection.AddTenancyInfrastructure(services, configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var act = () => serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StripeOptions>>().Value;
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*STRIPE_WEBHOOK_SECRET é obrigatória em ambiente de Produção*");
    }

    [Fact]
    public void AddTenancyInfrastructure_InProductionWithWebhookSecret_ShouldSucceedValidationOnStart()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ASPNETCORE_ENVIRONMENT", "Production" },
            { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=test;Trusted_Connection=True;" },
            { "Stripe:ApiKey", "sk_test_123" },
            { "Stripe:WebhookSecret", "whsec_valid_secret" }
        };

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        CriaCerto.Modules.Tenancy.Infrastructure.DependencyInjection.AddTenancyInfrastructure(services, configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var act = () => serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StripeOptions>>().Value;
        act.Should().NotThrow();
    }
}
