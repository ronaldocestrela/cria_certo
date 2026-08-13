using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Breeding.Application.Domain;

public class Cow
{
    public Guid Id { get; private set; }
    public string EarTag { get; private set; } = string.Empty;
    public string? SisbovId { get; private set; }
    public string? RfidTag { get; private set; }
    public string? Tattoo { get; private set; }
    public string? Nickname { get; private set; }
    public string? RegistryNumber { get; private set; }
    public string Breed { get; private set; } = string.Empty;
    public string Origin { get; private set; } = "Nascimento Interno";
    public DateTime? BirthDate { get; private set; }
    public DateTime? EntryDate { get; private set; }
    public decimal? EntryWeightKg { get; private set; }
    public string? SireInfo { get; private set; }
    public string? DamInfo { get; private set; }
    public decimal? BodyConditionScore { get; private set; }
    public string Category { get; private set; } = "Matriz";
    public ReproductiveStatus Status { get; private set; }
    public int ParityCount { get; private set; }
    public DateTime? LastCalvingDate { get; private set; }
    public Guid TenantId { get; private set; }

    private Cow() { }

    public static Result<Cow> Create(
        string earTag,
        string breed,
        DateTime? birthDate,
        Guid tenantId,
        string? sisbovId = null,
        string? rfidTag = null,
        string? tattoo = null,
        string? nickname = null,
        string? registryNumber = null,
        string origin = "Nascimento Interno",
        DateTime? entryDate = null,
        decimal? entryWeightKg = null,
        string? sireInfo = null,
        string? damInfo = null,
        decimal? bodyConditionScore = null,
        string category = "Matriz")
    {
        if (string.IsNullOrWhiteSpace(earTag))
            return Result.Failure<Cow>(Error.Validation("Cow.EarTagRequired", "O brinco de identificação do animal é obrigatório."));

        if (string.IsNullOrWhiteSpace(breed))
            return Result.Failure<Cow>(Error.Validation("Cow.BreedRequired", "A raça do animal é obrigatória."));

        if (birthDate.HasValue && birthDate.Value > DateTime.UtcNow)
            return Result.Failure<Cow>(Error.Validation("Cow.InvalidBirthDate", "Data de nascimento não pode ser no futuro."));

        if (entryDate.HasValue && birthDate.HasValue && entryDate.Value < birthDate.Value)
            return Result.Failure<Cow>(Error.Validation("Cow.InvalidEntryDate", "Data de entrada não pode ser anterior à data de nascimento."));

        if (bodyConditionScore.HasValue && (bodyConditionScore.Value < 1.0m || bodyConditionScore.Value > 5.0m))
            return Result.Failure<Cow>(Error.Validation("Cow.InvalidBcs", "O Escore de Condição Corporal (ECC) deve estar entre 1.0 e 5.0."));

        var cow = new Cow
        {
            Id = Guid.NewGuid(),
            EarTag = earTag.Trim(),
            Breed = breed.Trim(),
            BirthDate = birthDate,
            Status = ReproductiveStatus.Open,
            ParityCount = 0,
            LastCalvingDate = null,
            TenantId = tenantId,
            SisbovId = sisbovId?.Trim(),
            RfidTag = rfidTag?.Trim(),
            Tattoo = tattoo?.Trim(),
            Nickname = nickname?.Trim(),
            RegistryNumber = registryNumber?.Trim(),
            Origin = string.IsNullOrWhiteSpace(origin) ? "Nascimento Interno" : origin.Trim(),
            EntryDate = entryDate,
            EntryWeightKg = entryWeightKg,
            SireInfo = sireInfo?.Trim(),
            DamInfo = damInfo?.Trim(),
            BodyConditionScore = bodyConditionScore,
            Category = string.IsNullOrWhiteSpace(category) ? "Matriz" : category.Trim()
        };

        return Result.Success(cow);
    }

    public Result Update(
        string earTag,
        string breed,
        DateTime? birthDate,
        string? sisbovId = null,
        string? rfidTag = null,
        string? tattoo = null,
        string? nickname = null,
        string? registryNumber = null,
        string origin = "Nascimento Interno",
        DateTime? entryDate = null,
        decimal? entryWeightKg = null,
        string? sireInfo = null,
        string? damInfo = null,
        decimal? bodyConditionScore = null,
        string category = "Matriz")
    {
        if (string.IsNullOrWhiteSpace(earTag))
            return Result.Failure(Error.Validation("Cow.EarTagRequired", "O brinco de identificação é obrigatório."));

        if (string.IsNullOrWhiteSpace(breed))
            return Result.Failure(Error.Validation("Cow.BreedRequired", "A raça é obrigatória."));

        if (birthDate.HasValue && birthDate.Value > DateTime.UtcNow)
            return Result.Failure(Error.Validation("Cow.InvalidBirthDate", "Data de nascimento não pode ser no futuro."));

        if (entryDate.HasValue && birthDate.HasValue && entryDate.Value < birthDate.Value)
            return Result.Failure(Error.Validation("Cow.InvalidEntryDate", "Data de entrada não pode ser anterior à data de nascimento."));

        if (bodyConditionScore.HasValue && (bodyConditionScore.Value < 1.0m || bodyConditionScore.Value > 5.0m))
            return Result.Failure(Error.Validation("Cow.InvalidBcs", "O Escore de Condição Corporal (ECC) deve estar entre 1.0 e 5.0."));

        EarTag = earTag.Trim();
        Breed = breed.Trim();
        BirthDate = birthDate;
        SisbovId = sisbovId?.Trim();
        RfidTag = rfidTag?.Trim();
        Tattoo = tattoo?.Trim();
        Nickname = nickname?.Trim();
        RegistryNumber = registryNumber?.Trim();
        Origin = string.IsNullOrWhiteSpace(origin) ? "Nascimento Interno" : origin.Trim();
        EntryDate = entryDate;
        EntryWeightKg = entryWeightKg;
        SireInfo = sireInfo?.Trim();
        DamInfo = damInfo?.Trim();
        BodyConditionScore = bodyConditionScore;
        Category = string.IsNullOrWhiteSpace(category) ? "Matriz" : category.Trim();

        return Result.Success();
    }

    public Result StartIatfProtocol(Guid protocolId)
    {
        if (Status == ReproductiveStatus.Pregnant)
            return Result.Failure(Error.Conflict("Cow.AlreadyPregnant", "Matriz já está confirmada prenhe. Não é possível iniciar IATF."));

        if (Status == ReproductiveStatus.Culled || Status == ReproductiveStatus.Sold)
            return Result.Failure(Error.Conflict("Cow.Inactive", "Matriz inativa (descartada ou vendida)."));

        Status = ReproductiveStatus.InIatfProtocol;
        return Result.Success();
    }

    public Result RecordInsemination(DateTime inseminationDate, string semenBatchCode)
    {
        if (string.IsNullOrWhiteSpace(semenBatchCode))
            return Result.Failure(Error.Validation("Cow.SemenBatchRequired", "O código do lote de sêmen é obrigatório."));

        if (Status == ReproductiveStatus.Pregnant)
            return Result.Failure(Error.Conflict("Cow.AlreadyPregnant", "Matriz já está prenhe."));

        Status = ReproductiveStatus.Inseminated;
        return Result.Success();
    }

    public Result RecordPregnancyDiagnosis(bool isPregnant, DateTime diagnosisDate)
    {
        if (Status == ReproductiveStatus.Culled || Status == ReproductiveStatus.Sold)
            return Result.Failure(Error.Conflict("Cow.Inactive", "Matriz inativa."));

        Status = isPregnant ? ReproductiveStatus.Pregnant : ReproductiveStatus.Open;
        return Result.Success();
    }

    public Result RecordCalving(DateTime calvingDate)
    {
        if (calvingDate > DateTime.UtcNow)
            return Result.Failure(Error.Validation("Cow.InvalidCalvingDate", "Data de parto inválida."));

        ParityCount++;
        LastCalvingDate = calvingDate;
        Status = ReproductiveStatus.Open;
        return Result.Success();
    }
}
