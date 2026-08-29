using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Breeding.Application.Domain;

public class IatfProtocol
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime InseminationDate { get; private set; }
    public Guid SemenBatchId { get; private set; }
    public List<Guid> CowIds { get; private set; } = new();
    public Guid TenantId { get; private set; }

    public Guid? BullId { get; private set; }
    public string? BullName { get; private set; }

    private IatfProtocol() { }

    public static Result<IatfProtocol> Create(
        string name,
        DateTime startDate,
        DateTime inseminationDate,
        Guid semenBatchId,
        List<Guid> cowIds,
        Guid tenantId,
        Guid? bullId = null,
        string? bullName = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<IatfProtocol>(Error.Validation("IatfProtocol.NameRequired", "Nome do protocolo IATF é obrigatório."));

        if (inseminationDate <= startDate)
            return Result.Failure<IatfProtocol>(Error.Validation("IatfProtocol.InvalidDates", "Data de inseminação deve ser posterior ao início do protocolo."));

        if (cowIds == null || cowIds.Count == 0)
            return Result.Failure<IatfProtocol>(Error.Validation("IatfProtocol.NoCows", "Nenhuma matriz foi incluída no protocolo IATF."));

        var protocol = new IatfProtocol
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            StartDate = startDate,
            InseminationDate = inseminationDate,
            SemenBatchId = semenBatchId,
            CowIds = cowIds.Distinct().ToList(),
            TenantId = tenantId,
            BullId = bullId,
            BullName = string.IsNullOrWhiteSpace(bullName) ? null : bullName.Trim()
        };

        return Result.Success(protocol);
    }
}

public class PregnancyDiagnosis
{
    public Guid Id { get; private set; }
    public Guid CowId { get; private set; }
    public DateTime DiagnosisDate { get; private set; }
    public DiagnosisMethod Method { get; private set; }
    public bool IsPregnant { get; private set; }
    public int? GestationalAgeDays { get; private set; }
    public string? Notes { get; private set; }
    public Guid TenantId { get; private set; }

    private PregnancyDiagnosis() { }

    public static Result<PregnancyDiagnosis> Create(
        Guid cowId,
        DateTime diagnosisDate,
        DiagnosisMethod method,
        bool isPregnant,
        Guid tenantId,
        int? gestationalAgeDays = null,
        string? notes = null)
    {
        if (diagnosisDate > DateTime.UtcNow)
            return Result.Failure<PregnancyDiagnosis>(Error.Validation("PregnancyDiagnosis.InvalidDate", "Data de diagnóstico não pode ser no futuro."));

        if (isPregnant && gestationalAgeDays.HasValue && gestationalAgeDays.Value < 0)
            return Result.Failure<PregnancyDiagnosis>(Error.Validation("PregnancyDiagnosis.InvalidGestationalAge", "Idade gestacional deve ser positiva."));

        var diagnosis = new PregnancyDiagnosis
        {
            Id = Guid.NewGuid(),
            CowId = cowId,
            DiagnosisDate = diagnosisDate,
            Method = method,
            IsPregnant = isPregnant,
            GestationalAgeDays = gestationalAgeDays,
            Notes = notes?.Trim(),
            TenantId = tenantId
        };

        return Result.Success(diagnosis);
    }
}
