using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.RecordReferralClick;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class RecordReferralClickCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;

    public RecordReferralClickCommandHandlerTests()
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
    public async Task Handle_Should_Increment_ClicksCount_When_Code_Is_Valid_And_Active()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(participant.Id, "BOIGORDO2026").Value;
        _dbContext.Participants.Add(participant);
        _dbContext.ReferralCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var handler = new RecordReferralClickCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(new RecordReferralClickCommand("boigordo2026"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedCode = await _dbContext.ReferralCodes.SingleAsync(c => c.Id == code.Id);
        updatedCode.ClicksCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Code_Does_Not_Exist()
    {
        // Arrange
        var handler = new RecordReferralClickCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(new RecordReferralClickCommand("NONEXISTENT"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Code.NotFound.Code);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Code_Is_Inactive()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(participant.Id, "INACTIVECODE").Value;
        code.Deactivate();
        _dbContext.Participants.Add(participant);
        _dbContext.ReferralCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var handler = new RecordReferralClickCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(new RecordReferralClickCommand("INACTIVECODE"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Code.Inactive.Code);
    }
}
