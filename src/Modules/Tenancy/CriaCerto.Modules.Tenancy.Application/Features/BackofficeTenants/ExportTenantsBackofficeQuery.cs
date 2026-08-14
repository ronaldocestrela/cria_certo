using System.Globalization;
using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

public record ExportTenantsBackofficeQuery(
    string? SearchTerm = null,
    string? Status = null,
    string? SubscribedPlan = null,
    string? State = null,
    string? OwnerSearch = null,
    string? SizeSegment = null,
    string? CommercialRegion = null,
    string? ProductiveProfile = null,
    string? ChurnRisk = null,
    IReadOnlyCollection<Guid>? TagIds = null,
    bool IncludeInactiveTags = false
) : IRequest<Result<TenantExportResultDto>>;

public sealed class ExportTenantsBackofficeQueryHandler
    : IRequestHandler<ExportTenantsBackofficeQuery, Result<TenantExportResultDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public ExportTenantsBackofficeQueryHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantExportResultDto>> Handle(
        ExportTenantsBackofficeQuery request,
        CancellationToken cancellationToken)
    {
        var query = TenantBackofficeQueryBuilder.ApplyFilters(
            _dbContext.Tenants.AsNoTracking(),
            request.SearchTerm,
            request.Status,
            request.SubscribedPlan,
            request.State,
            request.OwnerSearch,
            request.SizeSegment,
            request.CommercialRegion,
            request.ProductiveProfile,
            request.ChurnRisk,
            request.TagIds,
            request.IncludeInactiveTags,
            _dbContext.TenantOperationalTags);

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount > TenantSegmentationCatalog.MaxExportRows)
        {
            return Result.Failure<TenantExportResultDto>(TenancyErrors.ExportLimitExceeded);
        }

        var tenants = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Take(TenantSegmentationCatalog.MaxExportRows)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var tagsByTenant = await TenantBackofficeMapper.LoadTagsByTenantIdsAsync(
            _dbContext.TenantOperationalTags,
            tenantIds,
            cancellationToken);

        var rows = tenants.Select(t =>
        {
            tagsByTenant.TryGetValue(t.Id, out var tags);
            var tagNames = tags is null ? string.Empty : string.Join("; ", tags.Select(x => x.Name));
            return new TenantExportRowDto(
                t.Id,
                t.Name,
                t.CNPJ,
                t.Status,
                t.SubscribedPlan,
                t.State,
                t.SizeSegment,
                t.CommercialRegion,
                t.ProductiveProfile,
                t.ChurnRisk,
                tagNames,
                t.TechnicalOwnerName,
                t.CommercialOwnerName,
                t.CreatedAtUtc);
        }).ToList();

        var csv = BuildCsv(rows);
        var fileName = $"tenants-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

        return Result.Success(new TenantExportResultDto(csv, fileName, rows.Count));
    }

    private static byte[] BuildCsv(IReadOnlyCollection<TenantExportRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Nome,CNPJ,Status,Plano,UF,Porte,Regiao,Perfil,Churn,Tags,OwnerTecnico,OwnerComercial,CriadoEmUtc");

        foreach (var row in rows)
        {
            sb.Append(CsvEscape(row.Id.ToString()));
            sb.Append(',');
            sb.Append(CsvEscape(row.Name));
            sb.Append(',');
            sb.Append(CsvEscape(row.CNPJ));
            sb.Append(',');
            sb.Append(CsvEscape(row.Status));
            sb.Append(',');
            sb.Append(CsvEscape(row.SubscribedPlan));
            sb.Append(',');
            sb.Append(CsvEscape(row.State));
            sb.Append(',');
            sb.Append(CsvEscape(row.SizeSegment));
            sb.Append(',');
            sb.Append(CsvEscape(row.CommercialRegion));
            sb.Append(',');
            sb.Append(CsvEscape(row.ProductiveProfile));
            sb.Append(',');
            sb.Append(CsvEscape(row.ChurnRisk));
            sb.Append(',');
            sb.Append(CsvEscape(row.Tags));
            sb.Append(',');
            sb.Append(CsvEscape(row.TechnicalOwnerName));
            sb.Append(',');
            sb.Append(CsvEscape(row.CommercialOwnerName));
            sb.Append(',');
            sb.Append(CsvEscape(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            sb.AppendLine();
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);
        return result;
    }

    private static string CsvEscape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }
}
