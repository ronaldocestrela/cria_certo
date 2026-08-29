namespace CriaCerto.Web.Client.Models;

public enum ReproductiveStatus
{
    Open = 1,
    InIatfProtocol = 2,
    Inseminated = 3,
    Pregnant = 4,
    Culled = 5,
    Sold = 6,
    Empty = 7,
    Bred = 8,
    Lactating = 9,
    Active = 10
}

public enum LifecycleStatus
{
    Active = 1,
    InIatf = 2,
    Pregnant = 3,
    Open = 4,
    Culled = 5,
    Sold = 6,
    Quarantine = 7
}

public enum BodyConditionScore
{
    VeryThin = 1,
    Thin = 2,
    Moderate = 3,
    Good = 4,
    Fat = 5,
    Ideal = 6,
    VeryFat = 7
}

public enum DiagnosisMethod
{
    Ultrasound = 1,
    RectalPalpation = 2
}

public enum CalvingType
{
    Normal = 1,
    Dystocic = 2,
    Cesarean = 3
}

public enum BirthCondition
{
    Live = 1,
    Stillborn = 2
}

public sealed record PlantelEventDto(
    Guid Id,
    string EventType,
    string Title,
    string Description,
    DateTime Date,
    string? Notes = null)
{
    public DateTime EventDate => Date;
}

public sealed record DnpAlertBannerDto(
    int AlertCount,
    string Message);

public sealed record CattleListResponse<TAnimal>(List<TAnimal> Items, int TotalCount, int Page, int PageSize);

public sealed record TimelineEventDto(
    DateTime EventDate,
    string EventType,
    string Title,
    string Description,
    string Category,
    string IconName);

public sealed record CowSummaryDto(
    Guid Id,
    string EarTag,
    string? SisbovId,
    string? RfidTag,
    string? Nickname,
    string Breed,
    string Category,
    ReproductiveStatus Status,
    int ParityCount,
    DateTime? LastCalvingDate,
    double? IepMonths,
    decimal? BodyConditionScore);

public sealed record CowDetailDto(
    Guid Id,
    string EarTag,
    string? SisbovId,
    string? RfidTag,
    string? Tattoo,
    string? Nickname,
    string? RegistryNumber,
    string Breed,
    string Origin,
    DateTime? BirthDate,
    DateTime? EntryDate,
    decimal? EntryWeightKg,
    string? SireInfo,
    string? DamInfo,
    decimal? BodyConditionScore,
    string Category,
    ReproductiveStatus Status,
    int ParityCount,
    DateTime? LastCalvingDate,
    double? IepMonths,
    int? OpenDays,
    List<TimelineEventDto> Timeline);

public sealed class CreateAnimalRequest
{
    public string EarTag { get; set; } = string.Empty;
    public string Breed { get; set; } = "Nelore";
    public DateTime? BirthDate { get; set; }
    public Guid TenantId { get; set; }
    public string? SisbovId { get; set; }
    public string? RfidTag { get; set; }
    public string? Tattoo { get; set; }
    public string? Nickname { get; set; }
    public string? RegistryNumber { get; set; }
    public string Origin { get; set; } = "Nascimento Interno";
    public DateTime? EntryDate { get; set; } = DateTime.Today;
    public decimal? EntryWeightKg { get; set; }
    public string? SireInfo { get; set; }
    public string? DamInfo { get; set; }
    public decimal? BodyConditionScore { get; set; } = 3.0m;
    public string Category { get; set; } = "Matriz";
}

public sealed class UpdateAnimalRequest
{
    public Guid Id { get; set; }
    public string EarTag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string? SisbovId { get; set; }
    public string? RfidTag { get; set; }
    public string? Tattoo { get; set; }
    public string? Nickname { get; set; }
    public string? RegistryNumber { get; set; }
    public string Origin { get; set; } = "Nascimento Interno";
    public DateTime? EntryDate { get; set; }
    public decimal? EntryWeightKg { get; set; }
    public string? SireInfo { get; set; }
    public string? DamInfo { get; set; }
    public decimal? BodyConditionScore { get; set; }
    public string Category { get; set; } = "Matriz";
}

public sealed record BullSummaryDto(
    Guid Id,
    string EarTag,
    string Name,
    string Breed,
    string? RegistryNumber,
    bool IsActive);

public sealed record IatfProtocolDto(
    Guid Id,
    string Name,
    DateTime StartDate,
    DateTime InseminationDate,
    Guid SemenBatchId,
    int CowCount,
    Guid? BullId = null,
    string? BullName = null);

public sealed record CalvingDto(
    Guid Id,
    Guid MotherCowId,
    DateTime CalvingDate,
    CalvingType Type,
    Guid CalfId,
    string CalfTagId,
    BirthCondition Condition);

public sealed record WeaningDto(
    Guid Id,
    Guid CalfId,
    string CalfTagId,
    Guid MotherCowId,
    DateTime WeaningDate,
    decimal WeaningWeightKg,
    decimal Adjusted205DayWeightKg,
    Guid? DestinationLotId);
