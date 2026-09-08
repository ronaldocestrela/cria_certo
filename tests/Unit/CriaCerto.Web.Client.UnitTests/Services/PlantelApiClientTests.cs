using System.Net;
using System.Net.Http.Json;
using CriaCerto.Web.Client.Models;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class PlantelApiClientTests
{
    private readonly IJSRuntime _jsRuntime;

    public PlantelApiClientTests()
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
    public async Task ListCowsAsync_WithPagination_ShouldIncludePageAndPageSizeInQueryString()
    {
        string? capturedUri = null;
        var mockHandler = new TestHttpMessageHandler(req =>
        {
            capturedUri = req.RequestUri!.ToString();

            var responsePayload = new CattleListResponse<CowSummaryDto>(
                new List<CowSummaryDto>
                {
                    new(Guid.NewGuid(), "BR-101", null, null, "Estrela", "Nelore", "Matriz", ReproductiveStatus.Active, 2, null, null, 3.5m),
                    new(Guid.NewGuid(), "BR-102", null, null, null, "Angus", "Novilha", ReproductiveStatus.Open, 0, null, null, 4.0m)
                },
                2,
                2,
                100
            );

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responsePayload)
            };
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost/") };
        var client = new PlantelApiClient(httpClient, _jsRuntime);

        var result = await client.ListCowsAsync(page: 2, pageSize: 100);

        capturedUri.Should().Contain("page=2");
        capturedUri.Should().Contain("pageSize=100");
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items[0].EarTag.Should().Be("BR-101");
        result.Items[1].EarTag.Should().Be("BR-102");
    }

    [Fact]
    public async Task ListCowsAsync_WithSearchAndStatus_ShouldIncludeAllParameters()
    {
        string? capturedUri = null;
        var mockHandler = new TestHttpMessageHandler(req =>
        {
            capturedUri = req.RequestUri!.ToString();

            var responsePayload = new CattleListResponse<CowSummaryDto>(
                new List<CowSummaryDto>(),
                0,
                1,
                50
            );

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responsePayload)
            };
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost/") };
        var client = new PlantelApiClient(httpClient, _jsRuntime);

        await client.ListCowsAsync(search: "NEL-", status: ReproductiveStatus.Pregnant, pageSize: 50);

        capturedUri.Should().Contain("search=NEL-");
        capturedUri.Should().Contain("status=Pregnant");
        capturedUri.Should().Contain("pageSize=50");
        capturedUri.Should().NotContain("page=");
    }
}
