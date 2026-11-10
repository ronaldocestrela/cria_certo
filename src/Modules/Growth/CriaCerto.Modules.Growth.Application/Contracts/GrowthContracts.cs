using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using CriaCerto.Modules.Growth.Application.Abstractions;
using CriaCerto.Modules.Growth.Application.Domain;
using CriaCerto.Modules.Growth.Application.Domain.Services;
using CriaCerto.Modules.Growth.Application.Services.ScaleParsers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Growth.Application.Contracts;

// --- DTOs ---
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

// --- COMMANDS ---
public sealed record CreatePaddockCommand(
    string Name,
    string Code,
    decimal AreaHectares,
    decimal MaxCapacityUA,
    Guid TenantId,
    PaddockStatus Status = PaddockStatus.Active) : ICommand<PaddockDto>;

[RequiresModule("Cows")]
public sealed record CreateLotCommand(
    string Name,
    string Code,
    LotCategory Category,
    int HeadCount,
    decimal AverageWeightKg,
    Guid TenantId,
    Guid? InitialPaddockId = null) : ICommand<LotDto>;

[RequiresModule("Cows")]
public sealed record MoveLotToPaddockCommand(
    Guid LotId,
    Guid? DestinationPaddockId,
    string Notes,
    Guid TenantId) : ICommand<LotMovementDto>;

[RequiresModule("Cows")]
public sealed record CloseLotCommand(
    Guid LotId,
    Guid TenantId) : ICommand<LotDto>;

// --- QUERIES ---
public sealed record GetPaddocksWithStockingRateQuery(
    Guid TenantId) : IQuery<List<PaddockStockingRateDto>>;

[RequiresModule("Cows")]
public sealed record GetLotsQuery(
    Guid TenantId) : IQuery<List<LotDto>>;

// --- VALIDATORS ---
public sealed class CreatePaddockCommandValidator : AbstractValidator<CreatePaddockCommand>
{
    public CreatePaddockCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AreaHectares).GreaterThan(0);
        RuleFor(x => x.MaxCapacityUA).GreaterThan(0);
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public sealed class CreateLotCommandValidator : AbstractValidator<CreateLotCommand>
{
    public CreateLotCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.HeadCount).GreaterThan(0);
        RuleFor(x => x.AverageWeightKg).GreaterThan(0);
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public sealed class MoveLotToPaddockCommandValidator : AbstractValidator<MoveLotToPaddockCommand>
{
    public MoveLotToPaddockCommandValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public sealed class CloseLotCommandValidator : AbstractValidator<CloseLotCommand>
{
    public CloseLotCommandValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

// --- HANDLERS ---
public sealed class CreatePaddockCommandHandler : IRequestHandler<CreatePaddockCommand, Result<PaddockDto>>
{
    private readonly IGrowthDbContext _dbContext;

    public CreatePaddockCommandHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PaddockDto>> Handle(CreatePaddockCommand request, CancellationToken cancellationToken)
    {
        var existingCode = await _dbContext.Paddocks
            .AnyAsync(p => p.TenantId == request.TenantId && p.Code == request.Code.Trim().ToUpperInvariant(), cancellationToken);

        if (existingCode)
            return Result.Failure<PaddockDto>(Error.Conflict("PasturePaddock.DuplicateCode", "Já existe um piquete cadastrado com este código."));

        var paddockResult = PasturePaddock.Create(
            request.Name,
            request.Code,
            request.AreaHectares,
            request.MaxCapacityUA,
            request.TenantId,
            request.Status);

        if (paddockResult.IsFailure)
            return Result.Failure<PaddockDto>(paddockResult.Error);

        _dbContext.Paddocks.Add(paddockResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new PaddockDto(
            paddockResult.Value.Id,
            paddockResult.Value.Name,
            paddockResult.Value.Code,
            paddockResult.Value.AreaHectares,
            paddockResult.Value.MaxCapacityUA,
            paddockResult.Value.Status,
            paddockResult.Value.CreatedAtUtc));
    }
}

public sealed class CreateLotCommandHandler : IRequestHandler<CreateLotCommand, Result<LotDto>>
{
    private readonly IGrowthDbContext _dbContext;

    public CreateLotCommandHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<LotDto>> Handle(CreateLotCommand request, CancellationToken cancellationToken)
    {
        var existingCode = await _dbContext.Lots
            .AnyAsync(l => l.TenantId == request.TenantId && l.Code == request.Code.Trim().ToUpperInvariant() && l.Status == LotStatus.Active, cancellationToken);

        if (existingCode)
            return Result.Failure<LotDto>(Error.Conflict("Lot.DuplicateCode", "Já existe um lote ativo com este código."));

        string? paddockName = null;
        if (request.InitialPaddockId.HasValue)
        {
            var paddock = await _dbContext.Paddocks
                .FirstOrDefaultAsync(p => p.Id == request.InitialPaddockId.Value && p.TenantId == request.TenantId, cancellationToken);

            if (paddock is null)
                return Result.Failure<LotDto>(Error.NotFound("PasturePaddock.NotFound", "Piquete inicial não encontrado."));

            paddockName = paddock.Name;
        }

        var lotResult = Lot.Create(
            request.Name,
            request.Code,
            request.Category,
            request.HeadCount,
            request.AverageWeightKg,
            request.TenantId,
            request.InitialPaddockId);

        if (lotResult.IsFailure)
            return Result.Failure<LotDto>(lotResult.Error);

        _dbContext.Lots.Add(lotResult.Value);

        if (request.InitialPaddockId.HasValue)
        {
            var movementResult = LotMovement.Create(
                lotResult.Value.Id,
                null,
                request.InitialPaddockId.Value,
                request.HeadCount,
                request.TenantId,
                "Entrada inicial no lote");

            if (movementResult.IsSuccess)
                _dbContext.LotMovements.Add(movementResult.Value);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new LotDto(
            lotResult.Value.Id,
            lotResult.Value.Name,
            lotResult.Value.Code,
            lotResult.Value.Category,
            lotResult.Value.CurrentPaddockId,
            paddockName,
            lotResult.Value.HeadCount,
            lotResult.Value.AverageWeightKg,
            lotResult.Value.TotalWeightKg,
            lotResult.Value.TotalUA,
            lotResult.Value.Status,
            lotResult.Value.CreatedAtUtc));
    }
}

public sealed class MoveLotToPaddockCommandHandler : IRequestHandler<MoveLotToPaddockCommand, Result<LotMovementDto>>
{
    private readonly IGrowthDbContext _dbContext;

    public MoveLotToPaddockCommandHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<LotMovementDto>> Handle(MoveLotToPaddockCommand request, CancellationToken cancellationToken)
    {
        var lot = await _dbContext.Lots
            .FirstOrDefaultAsync(l => l.Id == request.LotId && l.TenantId == request.TenantId, cancellationToken);

        if (lot is null)
            return Result.Failure<LotMovementDto>(Error.NotFound("Lot.NotFound", "Lote não encontrado."));

        if (request.DestinationPaddockId.HasValue)
        {
            var paddock = await _dbContext.Paddocks
                .FirstOrDefaultAsync(p => p.Id == request.DestinationPaddockId.Value && p.TenantId == request.TenantId, cancellationToken);

            if (paddock is null)
                return Result.Failure<LotMovementDto>(Error.NotFound("PasturePaddock.NotFound", "Piquete de destino não encontrado."));
        }

        Guid? sourcePaddockId = lot.CurrentPaddockId;

        Result assignResult = request.DestinationPaddockId.HasValue
            ? lot.AssignToPaddock(request.DestinationPaddockId.Value)
            : lot.RemoveFromPaddock();

        if (assignResult.IsFailure)
            return Result.Failure<LotMovementDto>(assignResult.Error);

        var movementResult = LotMovement.Create(
            lot.Id,
            sourcePaddockId,
            request.DestinationPaddockId,
            lot.HeadCount,
            request.TenantId,
            request.Notes);

        if (movementResult.IsFailure)
            return Result.Failure<LotMovementDto>(movementResult.Error);

        _dbContext.LotMovements.Add(movementResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new LotMovementDto(
            movementResult.Value.Id,
            movementResult.Value.LotId,
            movementResult.Value.SourcePaddockId,
            movementResult.Value.DestinationPaddockId,
            movementResult.Value.MovementDate,
            movementResult.Value.HeadCountMoved,
            movementResult.Value.Notes));
    }
}

public sealed class CloseLotCommandHandler : IRequestHandler<CloseLotCommand, Result<LotDto>>
{
    private readonly IGrowthDbContext _dbContext;

    public CloseLotCommandHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<LotDto>> Handle(CloseLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await _dbContext.Lots
            .FirstOrDefaultAsync(l => l.Id == request.LotId && l.TenantId == request.TenantId, cancellationToken);

        if (lot is null)
            return Result.Failure<LotDto>(Error.NotFound("Lot.NotFound", "Lote não encontrado."));

        var closeResult = lot.CloseLot();
        if (closeResult.IsFailure)
            return Result.Failure<LotDto>(closeResult.Error);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new LotDto(
            lot.Id,
            lot.Name,
            lot.Code,
            lot.Category,
            lot.CurrentPaddockId,
            null,
            lot.HeadCount,
            lot.AverageWeightKg,
            lot.TotalWeightKg,
            lot.TotalUA,
            lot.Status,
            lot.CreatedAtUtc));
    }
}

public sealed class GetPaddocksWithStockingRateQueryHandler : IRequestHandler<GetPaddocksWithStockingRateQuery, Result<List<PaddockStockingRateDto>>>
{
    private readonly IGrowthDbContext _dbContext;

    public GetPaddocksWithStockingRateQueryHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<PaddockStockingRateDto>>> Handle(GetPaddocksWithStockingRateQuery request, CancellationToken cancellationToken)
    {
        var paddocks = await _dbContext.Paddocks
            .Where(p => p.TenantId == request.TenantId)
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        var activeLots = await _dbContext.Lots
            .Where(l => l.TenantId == request.TenantId && l.Status == LotStatus.Active && l.CurrentPaddockId != null)
            .ToListAsync(cancellationToken);

        var result = new List<PaddockStockingRateDto>();

        foreach (var p in paddocks)
        {
            var paddockLots = activeLots.Where(l => l.CurrentPaddockId == p.Id).ToList();
            decimal totalWeight = paddockLots.Sum(l => l.TotalWeightKg);
            decimal totalUA = StockingRateCalculator.CalculateTotalUA(totalWeight);
            decimal stockingRate = StockingRateCalculator.CalculateStockingRate(totalUA, p.AreaHectares);
            int headCount = paddockLots.Sum(l => l.HeadCount);
            int lotsCount = paddockLots.Count;
            bool overgrazed = StockingRateCalculator.IsOvergrazed(totalUA, p.MaxCapacityUA);
            bool nearCapacity = StockingRateCalculator.IsNearCapacity(totalUA, p.MaxCapacityUA);

            result.Add(new PaddockStockingRateDto(
                p.Id,
                p.Name,
                p.Code,
                p.AreaHectares,
                p.MaxCapacityUA,
                p.Status,
                totalUA,
                stockingRate,
                headCount,
                lotsCount,
                overgrazed,
                nearCapacity));
        }

        return Result.Success(result);
    }
}

public sealed class GetLotsQueryHandler : IRequestHandler<GetLotsQuery, Result<List<LotDto>>>
{
    private readonly IGrowthDbContext _dbContext;

    public GetLotsQueryHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<LotDto>>> Handle(GetLotsQuery request, CancellationToken cancellationToken)
    {
        var lots = await _dbContext.Lots
            .Where(l => l.TenantId == request.TenantId)
            .OrderBy(l => l.Code)
            .ToListAsync(cancellationToken);

        var paddockIds = lots.Where(l => l.CurrentPaddockId != null).Select(l => l.CurrentPaddockId!.Value).Distinct().ToList();
        var paddocksDict = await _dbContext.Paddocks
            .Where(p => paddockIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var result = lots.Select(l => new LotDto(
            l.Id,
            l.Name,
            l.Code,
            l.Category,
            l.CurrentPaddockId,
            l.CurrentPaddockId.HasValue && paddocksDict.TryGetValue(l.CurrentPaddockId.Value, out var name) ? name : null,
            l.HeadCount,
            l.AverageWeightKg,
            l.TotalWeightKg,
            l.TotalUA,
            l.Status,
            l.CreatedAtUtc)).ToList();

        return Result.Success(result);
    }
}

// --- WEIGHING DTOs ---
public sealed record WeighingDto(
    Guid Id,
    Guid TenantId,
    string AnimalTagId,
    Guid? LotId,
    DateTime WeighingDate,
    decimal WeightKg,
    decimal CarcassYieldPercentage,
    decimal CalculatedArrobasTotal,
    decimal CalculatedAdgKgPerDay,
    decimal CalculatedMonthlyArrobaGain,
    bool IsWeightLossWarning,
    string Notes,
    DateTime CreatedAtUtc);

public sealed record SingleWeighingEntryDto(
    string AnimalTagId,
    decimal WeightKg,
    decimal CarcassYieldPercentage = Weighing.DefaultCarcassYieldPercentage,
    string Notes = "");

public sealed record AnimalWeighingHistoryDto(
    string AnimalTagId,
    decimal LatestWeightKg,
    decimal LatestArrobas,
    decimal AverageAdgKgPerDay,
    int WeighingsCount,
    List<WeighingDto> History);

public sealed record LotWeighingSummaryDto(
    Guid LotId,
    string LotName,
    int TotalAnimalsWeighed,
    decimal AverageWeightKg,
    decimal AverageAdgKgPerDay,
    decimal AverageArrobasPerAnimal,
    int WeightLossCount);

// --- WEIGHING COMMANDS ---
public sealed record RecordWeighingCommand(
    Guid TenantId,
    string AnimalTagId,
    Guid? LotId,
    DateTime WeighingDate,
    decimal WeightKg,
    decimal CarcassYieldPercentage = Weighing.DefaultCarcassYieldPercentage,
    string Notes = "") : ICommand<WeighingDto>;

public sealed record BatchRecordWeighingCommand(
    Guid TenantId,
    Guid? LotId,
    DateTime WeighingDate,
    List<SingleWeighingEntryDto> Entries) : ICommand<List<WeighingDto>>;

// --- WEIGHING QUERIES ---
public sealed record GetAnimalWeighingHistoryQuery(
    Guid TenantId,
    string AnimalTagId) : IQuery<AnimalWeighingHistoryDto>;

public sealed record GetLotWeighingSummaryQuery(
    Guid TenantId,
    Guid LotId) : IQuery<LotWeighingSummaryDto>;

public sealed record GetRecentWeighingsQuery(
    Guid TenantId,
    Guid? LotId = null,
    int Top = 50) : IQuery<List<WeighingDto>>;

// --- WEIGHING VALIDATORS ---
public sealed class RecordWeighingCommandValidator : AbstractValidator<RecordWeighingCommand>
{
    public RecordWeighingCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.AnimalTagId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.WeightKg).GreaterThan(0);
        RuleFor(x => x.CarcassYieldPercentage).InclusiveBetween(Weighing.MinCarcassYieldPercentage, Weighing.MaxCarcassYieldPercentage);
    }
}

public sealed class BatchRecordWeighingCommandValidator : AbstractValidator<BatchRecordWeighingCommand>
{
    public BatchRecordWeighingCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty();
    }
}

// --- WEIGHING HANDLERS ---
public sealed class RecordWeighingCommandHandler : IRequestHandler<RecordWeighingCommand, Result<WeighingDto>>
{
    private readonly IGrowthDbContext _dbContext;

    public RecordWeighingCommandHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<WeighingDto>> Handle(RecordWeighingCommand request, CancellationToken cancellationToken)
    {
        var weighingResult = Weighing.Create(
            request.TenantId,
            request.AnimalTagId,
            request.LotId,
            request.WeighingDate,
            request.WeightKg,
            request.CarcassYieldPercentage,
            request.Notes);

        if (weighingResult.IsFailure)
            return Result.Failure<WeighingDto>(weighingResult.Error);

        var weighing = weighingResult.Value;

        // Fetch previous weighing for this animal to calculate ADG/GPD
        var previousWeighing = await _dbContext.Weighings
            .Where(w => w.TenantId == request.TenantId && w.AnimalTagId == weighing.AnimalTagId && w.WeighingDate < weighing.WeighingDate)
            .OrderByDescending(w => w.WeighingDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousWeighing is not null)
        {
            weighing.ApplyPreviousWeighing(previousWeighing);
        }

        _dbContext.Weighings.Add(weighing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(weighing));
    }

    public static WeighingDto MapToDto(Weighing w) => new(
        w.Id,
        w.TenantId,
        w.AnimalTagId,
        w.LotId,
        w.WeighingDate,
        w.WeightKg,
        w.CarcassYieldPercentage,
        w.CalculatedArrobasTotal,
        w.CalculatedAdgKgPerDay,
        w.CalculatedMonthlyArrobaGain,
        w.IsWeightLossWarning,
        w.Notes,
        w.CreatedAtUtc);
}

public sealed class BatchRecordWeighingCommandHandler : IRequestHandler<BatchRecordWeighingCommand, Result<List<WeighingDto>>>
{
    private readonly IGrowthDbContext _dbContext;

    public BatchRecordWeighingCommandHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<WeighingDto>>> Handle(BatchRecordWeighingCommand request, CancellationToken cancellationToken)
    {
        var results = new List<WeighingDto>();

        foreach (var entry in request.Entries)
        {
            var weighingResult = Weighing.Create(
                request.TenantId,
                entry.AnimalTagId,
                request.LotId,
                request.WeighingDate,
                entry.WeightKg,
                entry.CarcassYieldPercentage,
                entry.Notes);

            if (weighingResult.IsFailure)
                return Result.Failure<List<WeighingDto>>(weighingResult.Error);

            var weighing = weighingResult.Value;

            var previousWeighing = await _dbContext.Weighings
                .Where(w => w.TenantId == request.TenantId && w.AnimalTagId == weighing.AnimalTagId && w.WeighingDate < weighing.WeighingDate)
                .OrderByDescending(w => w.WeighingDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousWeighing is not null)
            {
                weighing.ApplyPreviousWeighing(previousWeighing);
            }

            _dbContext.Weighings.Add(weighing);
            results.Add(RecordWeighingCommandHandler.MapToDto(weighing));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(results);
    }
}

public sealed class GetAnimalWeighingHistoryQueryHandler : IRequestHandler<GetAnimalWeighingHistoryQuery, Result<AnimalWeighingHistoryDto>>
{
    private readonly IGrowthDbContext _dbContext;

    public GetAnimalWeighingHistoryQueryHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<AnimalWeighingHistoryDto>> Handle(GetAnimalWeighingHistoryQuery request, CancellationToken cancellationToken)
    {
        string tag = request.AnimalTagId.Trim().ToUpperInvariant();

        var weighings = await _dbContext.Weighings
            .Where(w => w.TenantId == request.TenantId && w.AnimalTagId == tag)
            .OrderByDescending(w => w.WeighingDate)
            .ToListAsync(cancellationToken);

        if (!weighings.Any())
            return Result.Failure<AnimalWeighingHistoryDto>(Error.NotFound("Weighing.NotFound", $"Nenhuma pesagem encontrada para o animal {tag}."));

        var dtos = weighings.Select(RecordWeighingCommandHandler.MapToDto).ToList();
        var latest = dtos.First();
        var validGpds = dtos.Where(d => d.CalculatedAdgKgPerDay != 0.0m).Select(d => d.CalculatedAdgKgPerDay).ToList();
        decimal avgGpd = validGpds.Any() ? Math.Round(validGpds.Average(), 2) : 0.0m;

        return Result.Success(new AnimalWeighingHistoryDto(
            tag,
            latest.WeightKg,
            latest.CalculatedArrobasTotal,
            avgGpd,
            dtos.Count,
            dtos));
    }
}

public sealed class GetLotWeighingSummaryQueryHandler : IRequestHandler<GetLotWeighingSummaryQuery, Result<LotWeighingSummaryDto>>
{
    private readonly IGrowthDbContext _dbContext;

    public GetLotWeighingSummaryQueryHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<LotWeighingSummaryDto>> Handle(GetLotWeighingSummaryQuery request, CancellationToken cancellationToken)
    {
        var lot = await _dbContext.Lots
            .FirstOrDefaultAsync(l => l.Id == request.LotId && l.TenantId == request.TenantId, cancellationToken);

        if (lot is null)
            return Result.Failure<LotWeighingSummaryDto>(Error.NotFound("Lot.NotFound", "Lote não encontrado."));

        var weighings = await _dbContext.Weighings
            .Where(w => w.TenantId == request.TenantId && w.LotId == request.LotId)
            .OrderByDescending(w => w.WeighingDate)
            .ToListAsync(cancellationToken);

        if (!weighings.Any())
        {
            return Result.Success(new LotWeighingSummaryDto(
                lot.Id,
                lot.Name,
                0,
                0.0m,
                0.0m,
                0.0m,
                0));
        }

        var latestPerAnimal = weighings
            .GroupBy(w => w.AnimalTagId)
            .Select(g => g.First())
            .ToList();

        decimal avgWeight = Math.Round(latestPerAnimal.Average(w => w.WeightKg), 2);
        decimal avgArrobas = Math.Round(latestPerAnimal.Average(w => w.CalculatedArrobasTotal), 2);
        var validGpds = latestPerAnimal.Where(w => w.CalculatedAdgKgPerDay != 0.0m).Select(w => w.CalculatedAdgKgPerDay).ToList();
        decimal avgAdg = validGpds.Any() ? Math.Round(validGpds.Average(), 2) : 0.0m;
        int lossCount = latestPerAnimal.Count(w => w.IsWeightLossWarning);

        return Result.Success(new LotWeighingSummaryDto(
            lot.Id,
            lot.Name,
            latestPerAnimal.Count,
            avgWeight,
            avgAdg,
            avgArrobas,
            lossCount));
    }
}

public sealed class GetRecentWeighingsQueryHandler : IRequestHandler<GetRecentWeighingsQuery, Result<List<WeighingDto>>>
{
    private readonly IGrowthDbContext _dbContext;

    public GetRecentWeighingsQueryHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<WeighingDto>>> Handle(GetRecentWeighingsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Weighings.Where(w => w.TenantId == request.TenantId);

        if (request.LotId.HasValue)
            query = query.Where(w => w.LotId == request.LotId.Value);

        var weighings = await query
            .OrderByDescending(w => w.WeighingDate)
            .Take(request.Top)
            .ToListAsync(cancellationToken);

        return Result.Success(weighings.Select(RecordWeighingCommandHandler.MapToDto).ToList());
    }
}

// --- SCALE IMPORT & ANOMALIES ENUMS / DTOs / COMMANDS / HANDLERS ---

public enum ScaleModelEnum
{
    AutoDetect = 0,
    TruTest = 1,
    Coimma = 2,
    Toledo = 3,
    GenericCsv = 4
}

public sealed record WeighingImportRowResultDto(
    int RowNumber,
    string AnimalTagId,
    decimal WeightKg,
    bool IsSuccess,
    bool IsWeightLossAnomaly,
    string? Message);

public sealed record ImportWeighingFileResultDto(
    string FileName,
    ScaleModelEnum ScaleModel,
    int TotalRowsProcessed,
    int SuccessCount,
    int ErrorCount,
    int AnomaliesDetectedCount,
    List<WeighingImportRowResultDto> RowResults);

public sealed record ImportWeighingFileCommand(
    byte[] FileContent,
    string FileName,
    ScaleModelEnum ScaleModel,
    Guid TenantId,
    Guid? LotId,
    DateTime DefaultWeighingDate,
    decimal DefaultCarcassYieldPercentage = Weighing.DefaultCarcassYieldPercentage) : ICommand<ImportWeighingFileResultDto>;

public sealed record GetWeighingAnomaliesQuery(
    Guid TenantId,
    Guid? LotId = null) : IQuery<List<AnimalWeighingHistoryDto>>;

public sealed class ImportWeighingFileCommandValidator : AbstractValidator<ImportWeighingFileCommand>
{
    public ImportWeighingFileCommandValidator()
    {
        RuleFor(x => x.FileContent).NotEmpty().WithMessage("O conteúdo do arquivo é obrigatório.");
        RuleFor(x => x.FileName).NotEmpty().WithMessage("O nome do arquivo é obrigatório.");
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DefaultCarcassYieldPercentage).InclusiveBetween(Weighing.MinCarcassYieldPercentage, Weighing.MaxCarcassYieldPercentage);
    }
}

public sealed class ImportWeighingFileCommandHandler : IRequestHandler<ImportWeighingFileCommand, Result<ImportWeighingFileResultDto>>
{
    private readonly IGrowthDbContext _dbContext;
    private readonly IScaleFileParserFactory _parserFactory;

    public ImportWeighingFileCommandHandler(IGrowthDbContext dbContext, IScaleFileParserFactory parserFactory)
    {
        _dbContext = dbContext;
        _parserFactory = parserFactory;
    }

    public async Task<Result<ImportWeighingFileResultDto>> Handle(ImportWeighingFileCommand request, CancellationToken cancellationToken)
    {
        if (request.FileContent == null || request.FileContent.Length == 0)
            return Result.Failure<ImportWeighingFileResultDto>(Error.Validation("ImportWeighing.EmptyFile", "O arquivo enviado está vazio."));

        var parser = _parserFactory.GetParser(request.ScaleModel);
        using var stream = new MemoryStream(request.FileContent);
        var parseResult = parser.Parse(stream, request.FileName);

        if (parseResult.IsFailure)
            return Result.Failure<ImportWeighingFileResultDto>(parseResult.Error);

        var parsedRows = parseResult.Value;
        var rowResults = new List<WeighingImportRowResultDto>();
        int successCount = 0;
        int errorCount = 0;
        int anomaliesCount = 0;

        foreach (var row in parsedRows)
        {
            if (!row.IsValid)
            {
                errorCount++;
                rowResults.Add(new WeighingImportRowResultDto(row.RowNumber, row.AnimalTagId, row.WeightKg, false, false, row.ErrorMessage ?? "Registro inválido no arquivo."));
                continue;
            }

            var date = row.WeighingDate ?? request.DefaultWeighingDate;
            var yield = row.CarcassYieldPercentage ?? request.DefaultCarcassYieldPercentage;

            var weighingResult = Weighing.Create(
                request.TenantId,
                row.AnimalTagId,
                request.LotId,
                date,
                row.WeightKg,
                yield,
                row.Notes);

            if (weighingResult.IsFailure)
            {
                errorCount++;
                rowResults.Add(new WeighingImportRowResultDto(row.RowNumber, row.AnimalTagId, row.WeightKg, false, false, weighingResult.Error.Message));
                continue;
            }

            var weighing = weighingResult.Value;

            var previousWeighing = await _dbContext.Weighings
                .Where(w => w.TenantId == request.TenantId && w.AnimalTagId == weighing.AnimalTagId && w.WeighingDate < weighing.WeighingDate)
                .OrderByDescending(w => w.WeighingDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousWeighing is not null)
            {
                weighing.ApplyPreviousWeighing(previousWeighing);
            }

            _dbContext.Weighings.Add(weighing);
            successCount++;

            // Anomaly detection check (consecutive weight loss)
            var animalHistory = await _dbContext.Weighings
                .Where(w => w.TenantId == request.TenantId && w.AnimalTagId == weighing.AnimalTagId)
                .ToListAsync(cancellationToken);

            animalHistory.Add(weighing);

            bool isAnomaly = WeightLossAnomalyService.IsConsecutiveWeightLoss(animalHistory);
            if (isAnomaly) anomaliesCount++;

            rowResults.Add(new WeighingImportRowResultDto(row.RowNumber, weighing.AnimalTagId, weighing.WeightKg, true, isAnomaly, "Importado com sucesso."));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportWeighingFileResultDto(
            request.FileName,
            parser.ScaleModel,
            parsedRows.Count,
            successCount,
            errorCount,
            anomaliesCount,
            rowResults));
    }
}

public sealed class GetWeighingAnomaliesQueryHandler : IRequestHandler<GetWeighingAnomaliesQuery, Result<List<AnimalWeighingHistoryDto>>>
{
    private readonly IGrowthDbContext _dbContext;

    public GetWeighingAnomaliesQueryHandler(IGrowthDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<AnimalWeighingHistoryDto>>> Handle(GetWeighingAnomaliesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Weighings.Where(w => w.TenantId == request.TenantId);
        if (request.LotId.HasValue)
            query = query.Where(w => w.LotId == request.LotId.Value);

        var weighings = await query.ToListAsync(cancellationToken);
        var groupedByAnimal = weighings.GroupBy(w => w.AnimalTagId);

        var anomalyHistories = new List<AnimalWeighingHistoryDto>();

        foreach (var group in groupedByAnimal)
        {
            var historyList = group.OrderByDescending(w => w.WeighingDate).ToList();
            if (WeightLossAnomalyService.IsConsecutiveWeightLoss(historyList))
            {
                var dtos = historyList.Select(RecordWeighingCommandHandler.MapToDto).ToList();
                var latest = dtos.First();
                var validGpds = dtos.Where(d => d.CalculatedAdgKgPerDay != 0.0m).Select(d => d.CalculatedAdgKgPerDay).ToList();
                decimal avgGpd = validGpds.Any() ? Math.Round(validGpds.Average(), 2) : 0.0m;

                anomalyHistories.Add(new AnimalWeighingHistoryDto(
                    group.Key,
                    latest.WeightKg,
                    latest.CalculatedArrobasTotal,
                    avgGpd,
                    dtos.Count,
                    dtos));
            }
        }

        return Result.Success(anomalyHistories);
    }
}

