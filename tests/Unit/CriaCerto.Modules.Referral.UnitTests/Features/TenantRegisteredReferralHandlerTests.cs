using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.EventHandlers;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class TenantRegisteredReferralHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;

    public TenantRegisteredReferralHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ReferralDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Create_Pending_Attribution_When_Valid_ReferralCode_Provided()
    {
        // Arrange
        var referrerTenantId = Guid.NewGuid();
        var referrerParticipant = ReferralProgramParticipant.CreateCustomer(referrerTenantId).Value;
        var referralCode = ReferralCode.Create(referrerParticipant.Id, "FAZENDA-AMIGA").Value;

        _dbContext.Participants.Add(referrerParticipant);
        _dbContext.ReferralCodes.Add(referralCode);
        await _dbContext.SaveChangesAsync();

        var handler = new TenantRegisteredReferralHandler(_dbContext);

        var newTenantId = Guid.NewGuid();
        var notification = new TenantRegisteredIntegrationEvent(
            TenantId: newTenantId,
            UserId: Guid.NewGuid(),
            UserEmail: "novo@fazenda.com.br",
            ReferralCode: "fazenda-amiga",
            OriginIpAddress: "192.168.1.100",
            UserAgent: "Mozilla/5.0",
            RegisteredAtUtc: DateTime.UtcNow
        );

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var attribution = await _dbContext.Attributions.SingleOrDefaultAsync(a => a.ReferredTenantId == newTenantId);
        attribution.Should().NotBeNull();
        attribution!.Status.Should().Be(AttributionStatus.Pending);
        attribution.ReferralCodeId.Should().Be(referralCode.Id);
        attribution.ReferrerParticipantId.Should().Be(referrerParticipant.Id);
        attribution.OriginIpAddress.Should().Be("192.168.1.100");
        attribution.UserAgent.Should().Be("Mozilla/5.0");
    }

    [Fact]
    public async Task Handle_Should_Do_Nothing_When_ReferralCode_Is_Null_Or_Whitespace()
    {
        // Arrange
        var handler = new TenantRegisteredReferralHandler(_dbContext);
        var notification = new TenantRegisteredIntegrationEvent(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserEmail: "semref@fazenda.com.br",
            ReferralCode: null,
            OriginIpAddress: null,
            UserAgent: null,
            RegisteredAtUtc: DateTime.UtcNow
        );

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var attributions = await _dbContext.Attributions.ToListAsync();
        attributions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Do_Nothing_When_ReferralCode_Is_NotFound_Or_Inactive()
    {
        // Arrange
        var handler = new TenantRegisteredReferralHandler(_dbContext);
        var notification = new TenantRegisteredIntegrationEvent(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserEmail: "invalido@fazenda.com.br",
            ReferralCode: "INEXISTENTE999",
            OriginIpAddress: null,
            UserAgent: null,
            RegisteredAtUtc: DateTime.UtcNow
        );

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var attributions = await _dbContext.Attributions.ToListAsync();
        attributions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Flag_As_Fraud_When_Participant_Is_Same_Tenant_Attempting_Self_Referral()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var referrerParticipant = ReferralProgramParticipant.CreateCustomer(tenantId).Value;
        var referralCode = ReferralCode.Create(referrerParticipant.Id, "AUTO-REF").Value;

        _dbContext.Participants.Add(referrerParticipant);
        _dbContext.ReferralCodes.Add(referralCode);
        await _dbContext.SaveChangesAsync();

        var handler = new TenantRegisteredReferralHandler(_dbContext);
        var notification = new TenantRegisteredIntegrationEvent(
            TenantId: tenantId, // Mesma fazenda
            UserId: Guid.NewGuid(),
            UserEmail: "dono@fazenda.com.br",
            ReferralCode: "AUTO-REF",
            OriginIpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0",
            RegisteredAtUtc: DateTime.UtcNow
        );

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var attribution = await _dbContext.Attributions.SingleOrDefaultAsync(a => a.ReferredTenantId == tenantId);
        attribution.Should().NotBeNull();
        attribution!.Status.Should().Be(AttributionStatus.Fraudulent);
        attribution.FraudReason.Should().Contain("Auto-indicação");
    }

    [Fact]
    public async Task Handle_Should_Be_Idempotent_When_Attribution_Already_Exists()
    {
        // Arrange
        var referrerParticipant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid()).Value;
        var referralCode = ReferralCode.Create(referrerParticipant.Id, "CODIGO123").Value;
        var referredTenantId = Guid.NewGuid();

        _dbContext.Participants.Add(referrerParticipant);
        _dbContext.ReferralCodes.Add(referralCode);

        var existingAttribution = ReferralAttribution.Create(referralCode.Id, referrerParticipant.Id, referredTenantId).Value;
        _dbContext.Attributions.Add(existingAttribution);
        await _dbContext.SaveChangesAsync();

        var handler = new TenantRegisteredReferralHandler(_dbContext);
        var notification = new TenantRegisteredIntegrationEvent(
            TenantId: referredTenantId,
            UserId: Guid.NewGuid(),
            UserEmail: "jaexiste@fazenda.com.br",
            ReferralCode: "CODIGO123",
            OriginIpAddress: null,
            UserAgent: null,
            RegisteredAtUtc: DateTime.UtcNow
        );

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var attributions = await _dbContext.Attributions.Where(a => a.ReferredTenantId == referredTenantId).ToListAsync();
        attributions.Should().HaveCount(1);
    }
}
