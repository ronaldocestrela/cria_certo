using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Queries;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class GetCommercialPartnersQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;

    public GetCommercialPartnersQueryHandlerTests()
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
    public async Task Handle_ShouldReturnPagedPartnersWithActiveContracts()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var p1 = ReferralProgramParticipant.CreateCommercialPartner(adminId).Value;
        var prof1 = CommercialPartnerProfile.Create(p1.Id, "Consultor Alpha", null, "alpha@agro.com", "+5511999990001", PartnerCategory.ZootechnicianConsultant).Value;
        var c1 = PartnerContract.Create(p1.Id, CommissionType.PercentageRecurring, 20m, 12, 30, "alpha@agro.com", PixKeyType.Email, "11111111111", DateTime.UtcNow).Value;
        var code1 = ReferralCode.Create(p1.Id, "ALPHA-AGRO").Value;

        var p2 = ReferralProgramParticipant.CreateCommercialPartner(adminId).Value;
        var prof2 = CommercialPartnerProfile.Create(p2.Id, "Revenda Beta", "Beta Agropecuária", "beta@agro.com", "+5511999990002", PartnerCategory.AgriculturalDealer).Value;
        var c2 = PartnerContract.Create(p2.Id, CommissionType.FixedPerConversion, 350m, null, 30, "beta@agro.com", PixKeyType.Email, "22222222222", DateTime.UtcNow).Value;
        var code2 = ReferralCode.Create(p2.Id, "BETA-AGRO").Value;

        _dbContext.Participants.AddRange(p1, p2);
        _dbContext.PartnerProfiles.AddRange(prof1, prof2);
        _dbContext.PartnerContracts.AddRange(c1, c2);
        _dbContext.ReferralCodes.AddRange(code1, code2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetCommercialPartnersQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetCommercialPartnersQuery(SearchTerm: "Beta"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().FullName.Should().Be("Revenda Beta");
        result.Value.Items.First().CommissionType.Should().Be(CommissionType.FixedPerConversion);
        result.Value.Items.First().CommissionValue.Should().Be(350m);
    }
}
