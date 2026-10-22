using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class PayoutQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ReferralDbContext> _options;

    public PayoutQueriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPayoutDashboardKpis_ShouldCalculateCorrectMetrics()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "KPI-PARTNER").Value;
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, Guid.NewGuid()).Value;

        context.Participants.Add(partner);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var pending = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 150m, 150m, "BRL", DateTime.UtcNow.AddDays(10)).Value;
        var available = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 300m, 300m, "BRL", DateTime.UtcNow.AddDays(-1)).Value;
        available.ReleaseForPayout();

        var paidOut = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 500m, 500m, "BRL", DateTime.UtcNow.AddDays(-20)).Value;
        paidOut.ReleaseForPayout();
        paidOut.MarkPaidOut("PIX_PAID_1", "receipt.pdf", Guid.NewGuid(), DateTime.UtcNow);

        context.RewardLedger.AddRange(pending, available, paidOut);
        await context.SaveChangesAsync();

        var handler = new GetPayoutDashboardKpisQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetPayoutDashboardKpisQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var kpis = result.Value;
        kpis.TotalPendingMaturityAmount.Should().Be(150m);
        kpis.TotalPendingMaturityCount.Should().Be(1);
        kpis.TotalAvailableForPayoutAmount.Should().Be(300m);
        kpis.TotalAvailablePartnersCount.Should().Be(1);
        kpis.TotalPaidOutThisMonthAmount.Should().Be(500m);
        kpis.TotalPaidOutThisMonthCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAvailablePartnerPayouts_ShouldAggregateCommissionsByPartner()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var profile = CommercialPartnerProfile.Create(
            partner.Id,
            fullName: "Consultoria Boi Forte",
            tradeName: "Boi Forte",
            email: "contato@boiforte.com.br",
            phoneNumber: "11988887777",
            category: PartnerCategory.ZootechnicianConsultant,
            city: "Cuiabá",
            state: "MT",
            notes: null).Value;

        var contract = PartnerContract.Create(
            partner.Id,
            CommissionType.PercentageRecurring,
            commissionValue: 15m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        var code = ReferralCode.Create(partner.Id, "BOI-FORTE").Value;
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, Guid.NewGuid()).Value;

        context.Participants.Add(partner);
        context.PartnerProfiles.Add(profile);
        context.PartnerContracts.Add(contract);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var c1 = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 200m, 200m, "BRL", DateTime.UtcNow.AddDays(-5)).Value;
        var c2 = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 300m, 300m, "BRL", DateTime.UtcNow.AddDays(-2)).Value;
        c1.ReleaseForPayout();
        c2.ReleaseForPayout();

        context.RewardLedger.AddRange(c1, c2);
        await context.SaveChangesAsync();

        var handler = new GetAvailablePartnerPayoutsQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetAvailablePartnerPayoutsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var payouts = result.Value;
        payouts.Should().HaveCount(1);

        var p = payouts[0];
        p.ParticipantId.Should().Be(partner.Id);
        p.PartnerName.Should().Be("Consultoria Boi Forte");
        p.TradeName.Should().Be("Boi Forte");
        p.LegalDocumentNumber.Should().Be("123.456.789-09");
        p.PixKey.Should().Be("12345678909");
        p.PixKeyType.Should().Be(PixKeyType.Cpf);
        p.TotalAvailableAmount.Should().Be(500m);
        p.CommissionsCount.Should().Be(2);
        p.Commissions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExportPixBatchCsv_ShouldReturnValidCsvStreamAndFilename()
    {
        // Arrange
        var csvGenerator = new CriaCerto.Modules.Referral.Application.Features.Payouts.Services.PixBatchCsvGenerator();

        var payout = new CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos.AvailablePartnerPayoutDto(
            Guid.NewGuid(), "Agro Parcerias", "Agro", "12.345.678/0001-99", "agro@pix.com", PixKeyType.Email, 450m, 1, Array.Empty<CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos.CommissionDetailDto>()
        );

        var mediator = new FakeMediator(CriaCerto.BuildingBlocks.Abstractions.Results.Result.Success<IReadOnlyList<CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos.AvailablePartnerPayoutDto>>(new[] { payout }));
        var handler = new ExportPixBatchCsvQueryHandler(mediator, csvGenerator);

        // Act
        var result = await handler.Handle(new ExportPixBatchCsvQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().StartWith("lote_pix_criacerto_").And.EndWith(".csv");
        result.Value.ContentType.Should().Contain("csv");
        result.Value.FileBytes.Length.Should().BeGreaterThan(0);
    }

    private sealed class FakeMediator : MediatR.IMediator
    {
        private readonly object _response;
        public FakeMediator(object response) => _response = response;

        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult((TResponse)_response);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : MediatR.IRequest =>
            Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            Task.FromResult<object?>(_response);

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : MediatR.INotification => Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
