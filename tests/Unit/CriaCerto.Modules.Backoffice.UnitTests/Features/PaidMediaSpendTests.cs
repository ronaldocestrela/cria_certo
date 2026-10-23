using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Queries;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class PaidMediaSpendTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _db;

    public PaidMediaSpendTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _db = new BackofficeDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public void Create_ShouldSucceed_WhenDataIsValid()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var startDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = PaidMediaSpend.Create(
            PaidMediaChannel.GoogleAds,
            startDateUtc: startDate,
            endDateUtc: endDate,
            amount: 1500.50m,
            acquisitionsCount: 10,
            createdByAdminId: adminId,
            notes: "Campanha Institucional Pecuária");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Channel.Should().Be(PaidMediaChannel.GoogleAds);
        result.Value.Amount.Should().Be(1500.50m);
        result.Value.AcquisitionsCount.Should().Be(10);
        result.Value.CreatedByAdminId.Should().Be(adminId);
        result.Value.Notes.Should().Be("Campanha Institucional Pecuária");
        result.Value.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Create_ShouldFail_WhenAmountIsZeroOrNegative(decimal invalidAmount)
    {
        // Act
        var result = PaidMediaSpend.Create(
            PaidMediaChannel.MetaAds,
            startDateUtc: DateTime.UtcNow.AddDays(-30),
            endDateUtc: DateTime.UtcNow,
            amount: invalidAmount,
            acquisitionsCount: 5,
            createdByAdminId: Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PaidMediaSpend.InvalidAmount");
    }

    [Fact]
    public void Create_ShouldFail_WhenEndDateIsBeforeStartDate()
    {
        // Act
        var result = PaidMediaSpend.Create(
            PaidMediaChannel.MetaAds,
            startDateUtc: DateTime.UtcNow,
            endDateUtc: DateTime.UtcNow.AddDays(-1),
            amount: 500m,
            acquisitionsCount: 2,
            createdByAdminId: Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PaidMediaSpend.InvalidPeriod");
    }

    [Fact]
    public void Create_ShouldFail_WhenAcquisitionsCountIsNegative()
    {
        // Act
        var result = PaidMediaSpend.Create(
            PaidMediaChannel.MetaAds,
            startDateUtc: DateTime.UtcNow.AddDays(-10),
            endDateUtc: DateTime.UtcNow,
            amount: 500m,
            acquisitionsCount: -1,
            createdByAdminId: Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PaidMediaSpend.InvalidAcquisitionsCount");
    }

    [Fact]
    public void Create_ShouldFail_WhenAdminIdIsEmpty()
    {
        // Act
        var result = PaidMediaSpend.Create(
            PaidMediaChannel.MetaAds,
            startDateUtc: DateTime.UtcNow.AddDays(-10),
            endDateUtc: DateTime.UtcNow,
            amount: 500m,
            acquisitionsCount: 1,
            createdByAdminId: Guid.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PaidMediaSpend.AdminRequired");
    }

    [Fact]
    public async Task CreatePaidMediaSpendCommandHandler_ShouldPersist_AndAudit()
    {
        // Arrange
        var handler = new CreatePaidMediaSpendCommandHandler(_db);
        var adminId = Guid.NewGuid();
        var command = new CreatePaidMediaSpendCommand(
            Channel: "GoogleAds",
            PeriodStartDateUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndDateUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Amount: 3200m,
            AcquisitionsCount: 8,
            Notes: "Anúncios Search Agro",
            PerformedByAdminUserId: adminId,
            PerformedByAdminEmail: "marketing@criacerto.com.br",
            IpAddress: "192.168.1.1"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Channel.Should().Be("GoogleAds");
        result.Value.Amount.Should().Be(3200m);
        result.Value.AcquisitionsCount.Should().Be(8);
        result.Value.CacDirect.Should().Be(400m);

        var saved = await _db.PaidMediaSpends.FirstOrDefaultAsync(p => p.Id == result.Value.Id);
        saved.Should().NotBeNull();
        saved!.Amount.Should().Be(3200m);

        var auditLogs = await _db.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle(a => a.Action == "PaidMediaSpend.Created");
    }

    [Fact]
    public async Task GetPaidMediaSpendsQueryHandler_ShouldFilterByDateRange()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        var spend1 = PaidMediaSpend.Create(
            PaidMediaChannel.GoogleAds,
            startDateUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            endDateUtc: new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc),
            amount: 1000m,
            acquisitionsCount: 5,
            createdByAdminId: adminId).Value;

        var spend2 = PaidMediaSpend.Create(
            PaidMediaChannel.MetaAds,
            startDateUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            endDateUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            amount: 2000m,
            acquisitionsCount: 10,
            createdByAdminId: adminId).Value;

        _db.PaidMediaSpends.AddRange(spend1, spend2);
        await _db.SaveChangesAsync();

        var handler = new GetPaidMediaSpendsQueryHandler(_db);

        // Act - Querying September
        var result = await handler.Handle(new GetPaidMediaSpendsQuery(
            StartDateUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc)),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Channel.Should().Be("MetaAds");
        result.Value.First().Amount.Should().Be(2000m);
        result.Value.First().CacDirect.Should().Be(200m);
    }
}
