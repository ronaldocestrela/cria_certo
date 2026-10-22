using System.Net;
using System.Net.Http.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Web.Client.Models;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class PartnerApiClientTests
{
    private readonly IJSRuntime _jsRuntime;

    public PartnerApiClientTests()
    {
        _jsRuntime = Substitute.For<IJSRuntime>();
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task GetDashboardAsync_WhenApiSucceeds_ShouldReturnDashboardModel()
    {
        // Arrange
        var expectedModel = new PartnerPortalDashboardModel(
            ParticipantId: Guid.NewGuid(),
            FullName: "Carlos Agro Consultor",
            TradeName: "AgroFirma",
            Email: "carlos@agrofirma.com.br",
            PhoneNumber: "62999998888",
            Category: 0,
            LegalDocumentNumber: "123.456.789-00",
            MainReferralCode: "CARLOS-AGRO",
            TotalClicks: 42,
            TrialSignupsCount: 5,
            PaidSubscriptionsCount: 3,
            ConversionRatePercentage: 37.5m,
            PendingMaturityAmount: 600m,
            AvailablePayoutAmount: 400m,
            TotalPaidAmount: 1200m,
            Currency: "BRL",
            CommissionType: 0,
            CommissionValue: 20m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PixKeyType: 2,
            PixKey: "carlos@agrofirma.com.br",
            Campaigns: new List<PartnerCampaignSummaryModel>
            {
                new(Guid.NewGuid(), "CARLOS-AGRO", "Principal", 30, true, DateTime.UtcNow.AddDays(-20)),
                new(Guid.NewGuid(), "CARLOS-PALESTRA", "Palestra", 12, true, DateTime.UtcNow.AddDays(-5))
            }
        );

        var handler = new TestHttpMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/referral/partner/dashboard");
            request.Method.Should().Be(HttpMethod.Get);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedModel)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.criacerto.com.br") };
        var client = new PartnerApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.GetDashboardAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Carlos Agro Consultor");
        result.Value.TotalClicks.Should().Be(42);
        result.Value.AvailablePayoutAmount.Should().Be(400m);
        result.Value.Campaigns.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStatementAsync_WhenApiSucceeds_ShouldReturnStatementWithItems()
    {
        // Arrange
        var statement = new PartnerCommissionStatementModel(
            Items: new List<PartnerStatementItemModel>
            {
                new(
                    LedgerId: Guid.NewGuid(),
                    CreatedAtUtc: DateTime.UtcNow.AddDays(-5),
                    ReferredTenantId: Guid.NewGuid(),
                    ReferredTenantName: "Fazenda Esperança",
                    InvoiceGrossAmount: 1000m,
                    CommissionRateDescription: "20% Recorrente",
                    NetCommissionAmount: 200m,
                    Currency: "BRL",
                    Status: 0,
                    MaturityDateUtc: DateTime.UtcNow.AddDays(25),
                    PayoutTransactionReference: null,
                    PayoutReceiptFilePath: null,
                    PayoutRegisteredAtUtc: null
                )
            },
            TotalCount: 1,
            Page: 1,
            PageSize: 10
        );

        var handler = new TestHttpMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Contain("/api/v1/referral/partner/statement");
            request.Method.Should().Be(HttpMethod.Get);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(statement)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.criacerto.com.br") };
        var client = new PartnerApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.GetStatementAsync(search: "Esperança");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items[0].ReferredTenantName.Should().Be("Fazenda Esperança");
        result.Value.Items[0].NetCommissionAmount.Should().Be(200m);
    }

    [Fact]
    public async Task UpdatePixKeyAsync_WhenApiSucceeds_ShouldReturnSuccess()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/referral/partner/pix-key");
            request.Method.Should().Be(HttpMethod.Put);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.criacerto.com.br") };
        var client = new PartnerApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.UpdatePixKeyAsync(0, "123.456.789-09");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCampaignAsync_WhenApiSucceeds_ShouldReturnCampaignModel()
    {
        // Arrange
        var expectedCampaign = new PartnerCampaignSummaryModel(
            Id: Guid.NewGuid(),
            Code: "CARLOS-INSTA",
            CampaignTitle: "Instagram",
            ClicksCount: 0,
            IsActive: true,
            CreatedAtUtc: DateTime.UtcNow
        );

        var handler = new TestHttpMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/referral/partner/campaigns");
            request.Method.Should().Be(HttpMethod.Post);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedCampaign)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.criacerto.com.br") };
        var client = new PartnerApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.CreateCampaignAsync("Instagram", "CARLOS-INSTA");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("CARLOS-INSTA");
        result.Value.CampaignTitle.Should().Be("Instagram");
    }

    [Fact]
    public async Task GetDashboardAsync_WhenApiReturnsNotFound_ShouldReturnFailure()
    {
        // Arrange
        var error = Error.NotFound("Partner.NotFound", "Perfil não encontrado.");

        var handler = new TestHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(error)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.criacerto.com.br") };
        var client = new PartnerApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.GetDashboardAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Partner.NotFound");
    }
}
