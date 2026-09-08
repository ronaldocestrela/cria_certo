using System.Net;
using System.Net.Http.Json;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using CriaCerto.Modules.Sanitary.Application.Domain;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class SanitaryApiClientTests
{
    private readonly IJSRuntime _jsRuntime;

    public SanitaryApiClientTests()
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
    public async Task GetActiveCampaignsAsync_ShouldSendGetAndReturnList()
    {
        var expectedId = Guid.NewGuid();
        var mockHandler = new TestHttpMessageHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.ToString().Should().Contain("api/sanitary/campaigns");

            var responseList = new List<VaccinationCampaignDto>
            {
                new(expectedId, "Campanha Aftosa", CampaignType.Aftosa, DateTime.UtcNow, DateTime.UtcNow.AddDays(30), "Desc", true, DateTime.UtcNow)
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseList)
            };
            return response;
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost/") };
        var client = new SanitaryApiClient(httpClient, _jsRuntime);

        var result = await client.GetActiveCampaignsAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(expectedId);
        result[0].Name.Should().Be("Campanha Aftosa");
    }

    [Fact]
    public async Task CreateCampaignAsync_ShouldSendPostAndReturnDto()
    {
        var command = new CreateVaccinationCampaignCommand("Campanha Raiva", CampaignType.Raiva, DateTime.UtcNow, DateTime.UtcNow.AddDays(15), "Obs");
        var expectedDto = new VaccinationCampaignDto(Guid.NewGuid(), command.Name, command.Type, command.StartDateUtc, command.EndDateUtc, command.Description, true, DateTime.UtcNow);

        var mockHandler = new TestHttpMessageHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.ToString().Should().Contain("api/sanitary/campaigns");

            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(expectedDto)
            };
            return response;
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost/") };
        var client = new SanitaryApiClient(httpClient, _jsRuntime);

        var result = await client.CreateCampaignAsync(command);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Campanha Raiva");
    }

    [Fact]
    public async Task GetTreatmentsAsync_ShouldSendGetAndReturnList()
    {
        var expectedId = Guid.NewGuid();
        var mockHandler = new TestHttpMessageHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.ToString().Should().Contain("api/sanitary/treatments");

            var responseList = new List<TreatmentRecordDto>
            {
                new(expectedId, Guid.NewGuid(), null, "Dectomax", TreatmentType.Deworming, "LOT-1", "10ml", 28, DateTime.UtcNow, DateTime.UtcNow.AddDays(28), true, "Vet", "Notes")
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseList)
            };
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost/") };
        var client = new SanitaryApiClient(httpClient, _jsRuntime);

        var result = await client.GetTreatmentsAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(expectedId);
        result[0].ProductCommercialName.Should().Be("Dectomax");
    }

    [Fact]
    public async Task ApplyTreatmentAsync_ShouldSendPostAndReturnDto()
    {
        var command = new ApplyTreatmentCommand(Guid.NewGuid(), null, "Oxitetraciclina", TreatmentType.Medication, "LOT-2", "20ml", 14, DateTime.UtcNow, "Dr. Vet", "Notes");
        var expectedDto = new TreatmentRecordDto(Guid.NewGuid(), command.AnimalId, null, command.ProductCommercialName, command.Type, command.BatchNumber, command.Dosage, command.WithdrawalDays, command.ApplicationDateUtc, command.ApplicationDateUtc.AddDays(command.WithdrawalDays), true, command.AppliedByVeterinarian, command.Notes);

        var mockHandler = new TestHttpMessageHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.ToString().Should().Contain("api/sanitary/treatments");

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(expectedDto)
            };
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost/") };
        var client = new SanitaryApiClient(httpClient, _jsRuntime);

        var result = await client.ApplyTreatmentAsync(command);

        result.Should().NotBeNull();
        result!.ProductCommercialName.Should().Be("Oxitetraciclina");
    }
}
