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

public class ReferralApiClientTests
{
    private readonly IJSRuntime _jsRuntime;

    public ReferralApiClientTests()
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
    public async Task GetCustomerReferralDashboardAsync_WhenApiSucceeds_ShouldReturnDashboardModel()
    {
        // Arrange
        var expectedModel = new CustomerReferralDashboardModel(
            ReferralCode: "FAZENDA-MODELO",
            ReferralUrl: "https://criacerto.com.br/r/FAZENDA-MODELO",
            AccumulatedDiscountNextInvoice: 150.00m,
            FormattedAccumulatedDiscount: "R$ 150,00",
            ActiveReferralsCount: 3,
            PendingTrialReferralsCount: 2,
            CurrentPlanCode: "PRO",
            CurrentPlanName: "Plano Pro",
            DiscountPerReferralDescription: "R$ 50,00 por fazenda ativa",
            WhatsAppShareText: "Olá! Cadastre-se pelo meu link",
            ReferredFarms: new List<ReferredFarmSummaryModel>
            {
                new(Guid.NewGuid(), "F*****a S***a", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-5), "ActiveGeneratingDiscount", "Ativa", "+ R$ 50,00/mês")
            }
        );

        var handler = new TestHttpMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/referral/customer/dashboard");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedModel)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new ReferralApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.GetCustomerReferralDashboardAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReferralCode.Should().Be("FAZENDA-MODELO");
        result.Value.ActiveReferralsCount.Should().Be(3);
        result.Value.PendingTrialReferralsCount.Should().Be(2);
        result.Value.ReferredFarms.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCustomerReferralDashboardAsync_WhenApiReturnsError_ShouldReturnFailureResult()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(Error.Unauthorized("Auth.Unauthorized", "Não autorizado."))
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new ReferralApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.GetCustomerReferralDashboardAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task CustomizeReferralCodeAsync_WhenSuccessful_ShouldReturnNewCode()
    {
        // Arrange
        var expectedResponse = new CustomizeReferralCodeResponseModel("FAZENDA-NOVA", "https://criacerto.com.br/r/FAZENDA-NOVA");

        var handler = new TestHttpMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/referral/customer/code/customize");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedResponse)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new ReferralApiClient(httpClient, _jsRuntime);

        // Act
        var result = await client.CustomizeReferralCodeAsync("FAZENDA-NOVA");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("FAZENDA-NOVA");
    }
}
