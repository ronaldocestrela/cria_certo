using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Breeding.Application.Domain;

public class Bull
{
    public Guid Id { get; private set; }
    public string EarTag { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Breed { get; private set; } = string.Empty;
    public string? RegistryNumber { get; private set; }
    public DateTime? BirthDate { get; private set; }
    public bool IsActive { get; private set; }
    public Guid TenantId { get; private set; }

    private Bull() { }

    public static Result<Bull> Create(
        string earTag,
        string name,
        string breed,
        DateTime? birthDate,
        Guid tenantId,
        string? registryNumber = null)
    {
        if (string.IsNullOrWhiteSpace(earTag))
            return Result.Failure<Bull>(Error.Validation("Bull.EarTagRequired", "O brinco de identificação do touro é obrigatório."));

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Bull>(Error.Validation("Bull.NameRequired", "O nome do reprodutor é obrigatório."));

        if (string.IsNullOrWhiteSpace(breed))
            return Result.Failure<Bull>(Error.Validation("Bull.BreedRequired", "A raça do touro é obrigatória."));

        var bull = new Bull
        {
            Id = Guid.NewGuid(),
            EarTag = earTag.Trim(),
            Name = name.Trim(),
            Breed = breed.Trim(),
            BirthDate = birthDate,
            RegistryNumber = registryNumber?.Trim(),
            IsActive = true,
            TenantId = tenantId
        };

        return Result.Success(bull);
    }
}

public class SemenBatch
{
    public Guid Id { get; private set; }
    public string BatchCode { get; private set; } = string.Empty;
    public string BullName { get; private set; } = string.Empty;
    public string Breed { get; private set; } = string.Empty;
    public int StrawQuantity { get; private set; }
    public SemenType Type { get; private set; }
    public Guid TenantId { get; private set; }

    private SemenBatch() { }

    public static Result<SemenBatch> Create(
        string batchCode,
        string bullName,
        string breed,
        int strawQuantity,
        SemenType type,
        Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(batchCode))
            return Result.Failure<SemenBatch>(Error.Validation("SemenBatch.BatchCodeRequired", "O código da palheta/lote é obrigatório."));

        if (strawQuantity <= 0)
            return Result.Failure<SemenBatch>(Error.Validation("SemenBatch.InvalidQuantity", "A quantidade de palhetas deve ser maior que zero."));

        var batch = new SemenBatch
        {
            Id = Guid.NewGuid(),
            BatchCode = batchCode.Trim(),
            BullName = bullName.Trim(),
            Breed = breed.Trim(),
            StrawQuantity = strawQuantity,
            Type = type,
            TenantId = tenantId
        };

        return Result.Success(batch);
    }

    public Result UseStraws(int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("SemenBatch.InvalidQuantity", "Quantidade a utilizar deve ser positiva."));

        if (StrawQuantity < quantity)
            return Result.Failure(Error.Conflict("SemenBatch.InsufficientStock", $"Estoque insuficiente de sêmen. Disponível: {StrawQuantity}."));

        StrawQuantity -= quantity;
        return Result.Success();
    }
}
