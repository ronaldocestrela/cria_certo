using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using MediatR;

namespace CriaCerto.Modules.Analytics.Application.Contracts;

public enum ReportTypeEnum
{
    ExecutiveScorecard,
    HerdInventory,
    GtaSupport
}

public enum ReportFormatEnum
{
    Csv,
    Excel,
    Pdf
}

public enum PeriodTypeEnum
{
    CurrentHarvest,
    OffSeason,
    CurrentMonth,
    CustomRange
}

public sealed record HerdCategorySummaryDto(
    string CategoryName,
    int Quantity,
    decimal TotalWeightKg,
    decimal TotalArrobas,
    decimal AverageWeightKg);

public sealed record GtaAgeGroupBreakdownDto(
    string AgeGroupLabel,
    int MalesCount,
    int FemalesCount,
    int TotalCount);

public sealed record ExportReportResultDto(
    string FileName,
    string ContentType,
    byte[] FileContents);

public sealed record ExecutiveAnalyticsInput(
    int TotalCows,
    int PregnantCows,
    int CalvesWeaned,
    decimal TotalPastureHectares,
    decimal TotalAnimalUnits,
    decimal AverageGpdKg,
    decimal AverageCostPerArroba,
    int AnimalsUnderWithdrawal);

public sealed record ExecutiveScorecardDto(
    decimal PregnancyRatePercentage,
    decimal WeaningRatePercentage,
    decimal StockingRateUAPerHa,
    decimal AverageGpdKg,
    decimal CostPerArrobaProduced,
    int AnimalsUnderSlaughterWithdrawal,
    string OverallHealthStatus);

public sealed record GetExecutiveAnalyticsQuery(
    int TotalCows,
    int PregnantCows,
    int CalvesWeaned,
    decimal TotalPastureHectares,
    decimal TotalAnimalUnits,
    decimal AverageGpdKg,
    decimal AverageCostPerArroba,
    int AnimalsUnderWithdrawal) : IQuery<ExecutiveScorecardDto>;

public sealed record ExportBovineReportQuery(
    ExecutiveScorecardDto Scorecard,
    ReportTypeEnum ReportType = ReportTypeEnum.ExecutiveScorecard,
    ReportFormatEnum Format = ReportFormatEnum.Csv,
    PeriodTypeEnum PeriodType = PeriodTypeEnum.CurrentHarvest,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    List<HerdCategorySummaryDto>? InventoryCategories = null,
    List<GtaAgeGroupBreakdownDto>? GtaAgeGroups = null) : IQuery<ExportReportResultDto>;

public sealed record GpdMonthlyPointDto(
    string MonthLabel,
    decimal AverageGpdKg,
    int WeighingsCount);

public sealed record ExecutiveDashboardDto(
    string FarmName,
    decimal FarmAreaHectares,
    ExecutiveScorecardDto Scorecard,
    List<GpdMonthlyPointDto> GpdEvolution,
    int TotalActiveCows,
    int PregnantCows,
    int CalvesWeaned,
    decimal TotalPastureHectares,
    decimal TotalAnimalUnits,
    int ActiveSlaughterBlocks,
    string HealthStatusDetails);

public sealed record GetTenantExecutiveDashboardQuery(
    Guid? TenantId = null) : IQuery<ExecutiveDashboardDto>;

