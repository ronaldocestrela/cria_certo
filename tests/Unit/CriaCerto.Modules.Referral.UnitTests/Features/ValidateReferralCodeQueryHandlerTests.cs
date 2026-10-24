using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.ValidateReferralCode;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class ValidateReferralCodeQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;

    public ValidateReferralCodeQueryHandlerTests()
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
    public async Task Handle_Should_Return_Success_With_Dto_When_Code_Is_Valid_For_CommercialPartner()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCommercialPartner(adminId).Value;
        var code = ReferralCode.Create(participant.Id, "AGROCONSULT20", "Campanha Agrishow").Value;
        _dbContext.Participants.Add(participant);
        _dbContext.ReferralCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var handler = new ValidateReferralCodeQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new ValidateReferralCodeQuery("agroconsult20"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Code.Should().Be("AGROCONSULT20");
        result.Value.IsValid.Should().BeTrue();
        result.Value.CampaignTitle.Should().Be("Campanha Agrishow");
        result.Value.ParticipantType.Should().Be("CommercialPartner");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Code_Is_Not_Found()
    {
        // Arrange
        var handler = new ValidateReferralCodeQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new ValidateReferralCodeQuery("UNKNOWN123"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Code.NotFound.Code);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Code_Is_Inactive()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(participant.Id, "OLDCODE").Value;
        code.Deactivate();
        _dbContext.Participants.Add(participant);
        _dbContext.ReferralCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var handler = new ValidateReferralCodeQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new ValidateReferralCodeQuery("OLDCODE"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Code.Inactive.Code);
    }
}
