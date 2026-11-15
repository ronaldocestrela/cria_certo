namespace CriaCerto.Shared.UI.Models;

public enum BreedingMethod
{
    ArtificialInsemination = 1,
    TimedArtificialInsemination = 2,
    NaturalService = 3
}

public enum PregnancyDiagnosisResult
{
    Pregnant = 1,
    Empty = 2,
    ReturnToEstrus = 3,
    AbortOrDoubt = 4
}

public enum PregnancyDiagnosisMethod
{
    Ultrasound = 1,
    ReturnToEstrus = 2
}

public sealed record BreedingBatchLineDto(
    string TagId,
    BreedingMethod Method,
    string? BoarOrSemenRef,
    string? Technician,
    BodyConditionScore? BodyConditionScoreAtBreeding,
    string? Location,
    string? Notes);

public sealed record RegisterBreedingBatchRequest(
    DateOnly EventDate,
    List<BreedingBatchLineDto> Lines);

public sealed record RegisterBreedingBatchResponse(
    List<BreedingEventDto> Events,
    int RegisteredCount);

public sealed record BreedingEventDto(
    Guid Id,
    Guid SowId,
    string SowTagId,
    DateOnly EventDate,
    BreedingMethod Method,
    string? BoarOrSemenRef,
    string? Technician,
    BodyConditionScore? BodyConditionScoreAtBreeding,
    string? Location);

public sealed record RegisterPregnancyDiagnosisRequest(
    Guid SowId,
    DateOnly DiagnosisDate,
    PregnancyDiagnosisMethod Method,
    PregnancyDiagnosisResult Result,
    string? Notes);

public sealed record PregnancyDiagnosisDto(
    Guid Id,
    Guid SowId,
    string SowTagId,
    Guid? BreedingEventId,
    DateOnly DiagnosisDate,
    PregnancyDiagnosisMethod Method,
    PregnancyDiagnosisResult Result,
    ReproductiveStatus NewReproductiveStatus,
    int UpdatedDnpDays);

public sealed record PregnancyCheckTaskDto(
    Guid SowId,
    string TagId,
    string? Nickname,
    string? Location,
    Guid BreedingEventId,
    DateOnly BreedingDate,
    int DaysPostBreeding,
    string? Technician,
    BreedingMethod BreedingMethod,
    int DnpDays,
    bool RequiresAttention);

public sealed record PregnancyCheckQueueResponse(
    List<PregnancyCheckTaskDto> Items,
    int TotalCount,
    int PendingCount,
    int DnpAlertCount,
    int Page,
    int PageSize);

public sealed class BreedingEntryFormModel
{
    public string TagId { get; set; } = string.Empty;
    public BreedingMethod Method { get; set; } = BreedingMethod.ArtificialInsemination;
    public string BoarOrSemenRef { get; set; } = string.Empty;
    public string Technician { get; set; } = "Ricardo S.";
    public decimal EccScore { get; set; } = 3.5m;
}
