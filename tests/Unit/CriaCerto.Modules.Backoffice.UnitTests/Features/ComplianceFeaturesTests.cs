using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Queries;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class ComplianceFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly IPiiDataMasker _masker;

    public ComplianceFeaturesTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();
        _masker = new PiiDataMasker();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task RevealSensitiveData_WhenJustificationTooShort_ShouldReturnFailure()
    {
        // Arrange
        var sender = Substitute.For<MediatR.ISender>();
        var handler = new RevealSensitiveDataCommandHandler(_dbContext, sender, _masker);
        var command = new RevealSensitiveDataCommand(
            Guid.NewGuid(), "admin@criacerto.com.br", "PlatformOwner", "192.168.1.1", null,
            new RevealSensitiveDataRequest("Tenant", Guid.NewGuid(), "CNPJ", "curto")
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Compliance.JustificationRequired");
    }

    [Fact]
    public async Task RevealSensitiveData_WhenValidTenantRequest_ShouldRevealAndRecordForensicAudit()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var rawCpf = "12345678900";
        var tenantDetail = new TenantBackofficeDetailDto(
            tenantId, "Fazenda Alvorada", "Fazenda Alvorada Ltda", rawCpf, "EXT-01",
            "Active", "Enterprise", 1500, 2000, false, "MS", "Campo Grande", "123456",
            500, "Bovinocultura", "Medium", "Centro-Oeste", "Corte", "Low",
            new List<TenantOperationalTagDto>(), "João Silva", "joao@fazenda.com.br",
            "Maria Santos", "maria@fazenda.com.br", false, null, null, 5, 2,
            DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow
        );

        var sender = Substitute.For<MediatR.ISender>();
        sender.Send(Arg.Is<GetTenantBackofficeDetailQuery>(q => q.TenantId == tenantId), Arg.Any<CancellationToken>())
            .Returns(Result.Success(tenantDetail));

        var handler = new RevealSensitiveDataCommandHandler(_dbContext, sender, _masker);
        var command = new RevealSensitiveDataCommand(
            Guid.NewGuid(), "auditor@criacerto.com.br", "ReadOnlyAuditor", "177.136.241.10", "Mozilla/5.0",
            new RevealSensitiveDataRequest("Tenant", tenantId, "CNPJ", "Verificação de conformidade fiscal e contrato")
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlainValue.Should().Be(rawCpf);
        result.Value.MaskedValue.Should().Be("***.456.789-**");

        // Verifica persistência de log forense
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Id == result.Value.AuditLogId);
        auditLog.Should().NotBeNull();
        auditLog!.Category.Should().Be(AuditCategory.Compliance);
        auditLog.Severity.Should().Be(AuditSeverity.High);
        auditLog.Action.Should().Be("PII_DATA_UNMASKED");
        auditLog.VerifyIntegrity().Should().BeTrue();
    }

    [Fact]
    public async Task RevealSensitiveData_WhenValidAdminUserRequest_ShouldRevealAndRecordAudit()
    {
        // Arrange
        var user = AdminUser.Create("Carlos Administrador", "carlos@criacerto.com.br", "hashed_password").Value;
        _dbContext.AdminUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        var sender = Substitute.For<MediatR.ISender>();
        var handler = new RevealSensitiveDataCommandHandler(_dbContext, sender, _masker);
        var command = new RevealSensitiveDataCommand(
            Guid.NewGuid(), "platform@criacerto.com.br", "PlatformOwner", "127.0.0.1", null,
            new RevealSensitiveDataRequest("AdminUser", user.Id, "Email", "Auditoria interna de privilégios de acesso")
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlainValue.Should().Be("carlos@criacerto.com.br");
        result.Value.MaskedValue.Should().Be("c***s@criacerto.com.br");

        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Id == result.Value.AuditLogId);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("PII_DATA_UNMASKED");
    }

    [Fact]
    public async Task GetComplianceOverview_ShouldCalculateMetricsCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var log1 = AuditLog.CreateForensic(
            Guid.NewGuid(), "auditor@criacerto.com.br", "PlatformOwner", "PII_DATA_UNMASKED",
            AuditCategory.Compliance, AuditSeverity.High, "Tenant/1/CNPJ",
            Guid.NewGuid(), "Fazenda 1", "192.168.1.1");

        var log2 = AuditLog.CreateForensic(
            Guid.NewGuid(), "support@criacerto.com.br", "SupportN2", "IMPERSONATION_STARTED",
            AuditCategory.Security, AuditSeverity.High, "Impersonation/1",
            Guid.NewGuid(), "Fazenda 2", "192.168.1.2");

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetComplianceOverviewQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetComplianceOverviewQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PiiAccessLast24Hours.Should().Be(2);
        result.Value.PiiUnmasksLast30Days.Should().Be(1);
        result.Value.IsForensicTrailValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessTrail_WithFilters_ShouldReturnFilteredItems()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var log1 = AuditLog.CreateForensic(
            Guid.NewGuid(), "operator1@criacerto.com.br", "SupportN2", "PII_DATA_UNMASKED",
            AuditCategory.Compliance, AuditSeverity.High, "Tenant/1",
            tenantA, "Fazenda A", "192.168.1.1", detailsJson: "{\"Justification\":\"Conferência de contrato\"}");

        var log2 = AuditLog.CreateForensic(
            Guid.NewGuid(), "operator2@criacerto.com.br", "SupportN1", "ACCESS_VIEW_LOGS",
            AuditCategory.Security, AuditSeverity.Low, "Logs",
            tenantB, "Fazenda B", "192.168.1.2");

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAccessTrailQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetAccessTrailQuery(TargetTenantId: tenantA), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.First().TargetTenantId.Should().Be(tenantA);
        result.Value.Items.First().Justification.Should().Be("Conferência de contrato");
    }

    [Fact]
    public async Task ExportAccessTrail_WhenCsvRequested_ShouldGenerateFileAndRecordAuditLog()
    {
        // Arrange
        var log = AuditLog.CreateForensic(
            Guid.NewGuid(), "admin@criacerto.com.br", "PlatformOwner", "PII_DATA_UNMASKED",
            AuditCategory.Compliance, AuditSeverity.High, "Tenant/1/CNPJ",
            Guid.NewGuid(), "Fazenda Alvorada", "192.168.1.1");

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        var handler = new ExportAccessTrailQueryHandler(_dbContext);
        var query = new ExportAccessTrailQuery(
            Guid.NewGuid(), "dpo@criacerto.com.br", "PlatformOwner", "10.0.0.1", null,
            new ExportAccessTrailRequest(Purpose: "Auditoria ANPD Art. 37", Format: "CSV")
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().StartWith("dossie-acesso-lgpd-");
        result.Value.ContentType.Should().Contain("text/csv");
        result.Value.Content.Length.Should().BeGreaterThan(0);
        result.Value.Sha256Hash.Should().NotBeNullOrEmpty();

        var auditRecord = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "COMPLIANCE_DOSSIER_EXPORTED");
        auditRecord.Should().NotBeNull();
        auditRecord!.Severity.Should().Be(AuditSeverity.Critical);
    }
}
