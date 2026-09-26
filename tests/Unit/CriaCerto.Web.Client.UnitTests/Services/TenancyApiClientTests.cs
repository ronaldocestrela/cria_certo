using System.Net;
using System.Net.Http.Json;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class TenancyApiClientTests
{
    private readonly IJSRuntime _jsRuntime;

    public TenancyApiClientTests()
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
    public async Task GetSubscriptionPlansAsync_WhenApiReturnsPlans_ShouldReturnPlansFromBackend()
    {
        // Arrange
        string? requestedUri = null;
        var plansFromBackend = new List<SubscriptionPlanModel>
        {
            new(
                PlanId: "Starter",
                Name: "Plano Starter Custom",
                Description: "Para pequenas fazendas",
                MonthlyPrice: 199.90m,
                AnnualPriceMonthly: 159.90m,
                HeadCapacityLimit: 600,
                IncludedModules: new[] { "Breeding", "Calving" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureModel>
                {
                    new("Modules.Breeding", "Reprodução", true)
                }
            ),
            new(
                PlanId: "Pro",
                Name: "Plano Pro Custom",
                Description: "Para fazendas profissionais",
                MonthlyPrice: 399.90m,
                AnnualPriceMonthly: 329.90m,
                HeadCapacityLimit: 3000,
                IncludedModules: new[] { "Breeding", "Calving", "Growth" },
                IsPopular: true,
                Features: new List<SubscriptionPlanFeatureModel>
                {
                    new("Modules.Growth", "Manejo e Pesagem", true)
                }
            )
        };

        var handler = new TestHttpMessageHandler(req =>
        {
            requestedUri = req.RequestUri!.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(plansFromBackend)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.criacerto.com.br/") };
        var apiClient = new TenancyApiClient(client, _jsRuntime);

        // Act
        var plans = await apiClient.GetSubscriptionPlansAsync();

        // Assert
        requestedUri.Should().Be("/api/v1/tenancy/plans");
        plans.Should().NotBeNull();
        plans.Should().HaveCount(2);

        var starter = plans.First(p => p.PlanId == "Starter");
        starter.Name.Should().Be("Plano Starter Custom");
        starter.MonthlyPrice.Should().Be(199.90m);
        starter.AnnualPriceMonthly.Should().Be(159.90m);
        starter.HeadCapacityLimit.Should().Be(600);

        var pro = plans.First(p => p.PlanId == "Pro");
        pro.Name.Should().Be("Plano Pro Custom");
        pro.MonthlyPrice.Should().Be(399.90m);
        pro.IsPopular.Should().BeTrue();
    }

    [Fact]
    public async Task GetSubscriptionPlansAsync_WhenApiReturnsError_ShouldFallbackToDefaultPlans()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.criacerto.com.br/") };
        var apiClient = new TenancyApiClient(client, _jsRuntime);

        // Act
        var plans = await apiClient.GetSubscriptionPlansAsync();

        // Assert
        plans.Should().NotBeNull();
        plans.Should().NotBeEmpty();
        plans.Should().Contain(p => p.PlanId == "Starter");
        plans.Should().Contain(p => p.PlanId == "Pro");
        plans.Should().Contain(p => p.PlanId == "Enterprise");
    }
}
