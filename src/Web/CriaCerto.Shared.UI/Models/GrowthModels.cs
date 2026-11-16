namespace CriaCerto.Shared.UI.Models;

public enum PaddockStatus
{
    Active = 1,
    Resting = 2,    // Pousio
    Maintenance = 3 // Reforma/Manutenção
}

public enum LotCategory
{
    Bezerros = 1,
    Recria = 2,
    Garrotes = 3,
    Engorda = 4,
    Matrizes = 5,
    Reprodutores = 6
}

public enum LotStatus
{
    Active = 1,
    Closed = 2
}

public sealed record PaddockDto(
    Guid Id,
    string Name,
    string Code,
    decimal AreaHectares,
    decimal MaxCapacityUA,
    PaddockStatus Status,
    DateTime CreatedAtUtc);

public sealed record PaddockStockingRateDto(
    Guid Id,
    string Name,
    string Code,
    decimal AreaHectares,
    decimal MaxCapacityUA,
    PaddockStatus Status,
    decimal CurrentTotalUA,
    decimal CurrentStockingRateUAPerHa,
    int TotalHeadCount,
    int AssignedLotsCount,
    bool IsOvergrazed,
    bool IsNearCapacity);

public sealed record LotDto(
    Guid Id,
    string Name,
    string Code,
    LotCategory Category,
    Guid? CurrentPaddockId,
    string? PaddockName,
    int HeadCount,
    decimal AverageWeightKg,
    decimal TotalWeightKg,
    decimal TotalUA,
    LotStatus Status,
    DateTime CreatedAtUtc);

public sealed record LotMovementDto(
    Guid Id,
    Guid LotId,
    Guid? SourcePaddockId,
    Guid? DestinationPaddockId,
    DateTime MovementDate,
    int HeadCountMoved,
    string Notes);

public sealed record CreatePaddockCommand(
    string Name,
    string Code,
    decimal AreaHectares,
    decimal MaxCapacityUA,
    Guid TenantId,
    PaddockStatus Status = PaddockStatus.Active);

public sealed record CreateLotCommand(
    string Name,
    string Code,
    LotCategory Category,
    int HeadCount,
    decimal AverageWeightKg,
    Guid TenantId,
    Guid? InitialPaddockId = null);

public sealed record MoveLotToPaddockCommand(
    Guid LotId,
    Guid? DestinationPaddockId,
    string Notes,
    Guid TenantId);

public sealed record CloseLotCommand(
    Guid LotId,
    Guid TenantId);

public sealed record WeighingImportRowResultDto(
    int RowNumber,
    string AnimalTagId,
    decimal WeightKg,
    bool IsSuccess,
    bool IsWeightLossAnomaly,
    string? Message);

public sealed record ImportWeighingFileResultDto(
    string FileName,
    int ScaleModel,
    int TotalRowsProcessed,
    int SuccessCount,
    int ErrorCount,
    int AnomaliesDetectedCount,
    List<WeighingImportRowResultDto> RowResults);

public sealed record SlaughterEligibilityDto(
    Guid AnimalId,
    bool IsEligibleForSlaughter,
    int RemainingWithdrawalDays,
    string? BlockingTreatmentName,
    DateTime? ActiveWithdrawalEndsAtUtc);

public sealed record DispatchAnimalCommand(
    Guid TenantId,
    Guid AnimalId,
    string AnimalEarTag,
    string Destination,
    DateTime DispatchDate,
    bool IsSlaughter);

public sealed record AnimalDispatchResultDto(
    Guid DispatchId,
    Guid TenantId,
    Guid? AnimalId,
    Guid? LotId,
    string EarTagOrLotCode,
    string Destination,
    DateTime DispatchDate,
    bool IsSlaughter,
    string Status);
