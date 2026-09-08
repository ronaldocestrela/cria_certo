using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Application.Contracts;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Breeding.Application.Features.Plantel;

[RequiresModule("Breeding")]
public sealed record CreateCowCommand(
    string EarTag,
    string Breed,
    DateTime? BirthDate = null,
    Guid TenantId = default,
    string? SisbovId = null,
    string? RfidTag = null,
    string? Tattoo = null,
    string? Nickname = null,
    string? RegistryNumber = null,
    string Origin = "Nascimento Interno",
    DateTime? EntryDate = null,
    decimal? EntryWeightKg = null,
    string? SireInfo = null,
    string? DamInfo = null,
    decimal? BodyConditionScore = null,
    string Category = "Matriz") : ICommand<CowDetailDto>;

[RequiresModule("Breeding")]
public sealed record UpdateCowCommand(
    Guid Id,
    string EarTag,
    string Breed,
    DateTime? BirthDate = null,
    string? SisbovId = null,
    string? RfidTag = null,
    string? Tattoo = null,
    string? Nickname = null,
    string? RegistryNumber = null,
    string Origin = "Nascimento Interno",
    DateTime? EntryDate = null,
    decimal? EntryWeightKg = null,
    string? SireInfo = null,
    string? DamInfo = null,
    decimal? BodyConditionScore = null,
    string Category = "Matriz") : ICommand<CowDetailDto>;

[RequiresModule("Breeding")]
public sealed record GetCowQuery(Guid Id) : IQuery<CowDetailDto>;

[RequiresModule("Breeding")]
public sealed record ListCowsQuery(string? Search, ReproductiveStatus? Status, int Page = 1, int PageSize = 25) : IQuery<CattleListResponse<CowSummaryDto>>;

[RequiresModule("Breeding")]
public sealed record ListBullsQuery(Guid TenantId) : IQuery<List<BullSummaryDto>>;

public sealed class CreateCowCommandValidator : AbstractValidator<CreateCowCommand>
{
    public CreateCowCommandValidator()
    {
        RuleFor(x => x.EarTag).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Breed).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BirthDate).LessThanOrEqualTo(DateTime.UtcNow).When(x => x.BirthDate.HasValue);
        RuleFor(x => x.BodyConditionScore).InclusiveBetween(1.0m, 5.0m).When(x => x.BodyConditionScore.HasValue);
    }
}

public sealed class UpdateCowCommandValidator : AbstractValidator<UpdateCowCommand>
{
    public UpdateCowCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.EarTag).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Breed).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BirthDate).LessThanOrEqualTo(DateTime.UtcNow).When(x => x.BirthDate.HasValue);
        RuleFor(x => x.BodyConditionScore).InclusiveBetween(1.0m, 5.0m).When(x => x.BodyConditionScore.HasValue);
    }
}

public sealed class CreateCowCommandHandler : IRequestHandler<CreateCowCommand, Result<CowDetailDto>>
{
    private readonly IBreedingDbContext _dbContext;

    public CreateCowCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<CowDetailDto>> Handle(CreateCowCommand request, CancellationToken cancellationToken)
    {
        var normalizedEarTag = request.EarTag.Trim().ToUpperInvariant();
        if (await _dbContext.Cows.AnyAsync(c => c.TenantId == request.TenantId && c.EarTag.ToUpper() == normalizedEarTag, cancellationToken))
        {
            return Result.Failure<CowDetailDto>(Error.Conflict("Cow.EarTagAlreadyExists", "Já existe um animal cadastrado com este brinco nesta fazenda."));
        }

        var cowResult = Cow.Create(
            request.EarTag,
            request.Breed,
            request.BirthDate,
            request.TenantId,
            request.SisbovId,
            request.RfidTag,
            request.Tattoo,
            request.Nickname,
            request.RegistryNumber,
            request.Origin,
            request.EntryDate,
            request.EntryWeightKg,
            request.SireInfo,
            request.DamInfo,
            request.BodyConditionScore,
            request.Category);

        if (cowResult.IsFailure)
            return Result.Failure<CowDetailDto>(cowResult.Error);

        _dbContext.Cows.Add(cowResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(cowResult.Value.ToDetailDto());
    }
}

public sealed class UpdateCowCommandHandler : IRequestHandler<UpdateCowCommand, Result<CowDetailDto>>
{
    private readonly IBreedingDbContext _dbContext;

    public UpdateCowCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<CowDetailDto>> Handle(UpdateCowCommand request, CancellationToken cancellationToken)
    {
        var cow = await _dbContext.Cows.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (cow is null)
            return Result.Failure<CowDetailDto>(Error.NotFound("Cow.NotFound", "Animal não encontrado."));

        var normalizedEarTag = request.EarTag.Trim().ToUpperInvariant();
        if (await _dbContext.Cows.AnyAsync(c => c.TenantId == cow.TenantId && c.Id != cow.Id && c.EarTag.ToUpper() == normalizedEarTag, cancellationToken))
        {
            return Result.Failure<CowDetailDto>(Error.Conflict("Cow.EarTagAlreadyExists", "Outro animal já utiliza este brinco."));
        }

        var updateResult = cow.Update(
            request.EarTag,
            request.Breed,
            request.BirthDate,
            request.SisbovId,
            request.RfidTag,
            request.Tattoo,
            request.Nickname,
            request.RegistryNumber,
            request.Origin,
            request.EntryDate,
            request.EntryWeightKg,
            request.SireInfo,
            request.DamInfo,
            request.BodyConditionScore,
            request.Category);

        if (updateResult.IsFailure)
            return Result.Failure<CowDetailDto>(updateResult.Error);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(cow.ToDetailDto());
    }
}

public sealed class GetCowQueryHandler : IRequestHandler<GetCowQuery, Result<CowDetailDto>>
{
    private readonly IBreedingDbContext _dbContext;

    public GetCowQueryHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<CowDetailDto>> Handle(GetCowQuery request, CancellationToken cancellationToken)
    {
        var cow = await _dbContext.Cows.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (cow is null)
            return Result.Failure<CowDetailDto>(Error.NotFound("Cow.NotFound", "Matriz/Bovino não encontrado."));

        var timeline = BuildTimeline(cow);
        return Result.Success(cow.ToDetailDto(timeline));
    }

    private static List<TimelineEventDto> BuildTimeline(Cow cow)
    {
        var events = new List<TimelineEventDto>();

        if (cow.BirthDate.HasValue)
        {
            events.Add(new(cow.BirthDate.Value, "Birth", "Nascimento do Animal", $"Raça {cow.Breed}. Origem: {cow.Origin}", "Cadastral", "cake"));
        }

        if (cow.EntryDate.HasValue)
        {
            events.Add(new(cow.EntryDate.Value, "Entry", "Entrada no Rebanho", $"Peso de Entrada: {(cow.EntryWeightKg.HasValue ? cow.EntryWeightKg + " kg" : "N/A")}", "Manejo", "login"));
        }

        if (cow.LastCalvingDate.HasValue)
        {
            events.Add(new(cow.LastCalvingDate.Value, "Calving", "Registro de Parto", $"Parto nº {cow.ParityCount} registrado com sucesso.", "Reprodução", "child_care"));
        }

        return events.OrderByDescending(e => e.EventDate).ToList();
    }
}

public sealed class ListCowsQueryHandler : IRequestHandler<ListCowsQuery, Result<CattleListResponse<CowSummaryDto>>>
{
    private readonly IBreedingDbContext _dbContext;

    public ListCowsQueryHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<CattleListResponse<CowSummaryDto>>> Handle(ListCowsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Cows.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToUpperInvariant();
            query = query.Where(c => c.EarTag.ToUpper().Contains(search) ||
                                     (c.Nickname != null && c.Nickname.ToUpper().Contains(search)) ||
                                     (c.SisbovId != null && c.SisbovId.ToUpper().Contains(search)) ||
                                     (c.RfidTag != null && c.RfidTag.ToUpper().Contains(search)));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.Status == request.Status.Value);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.EarTag)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => c.ToSummaryDto())
            .ToListAsync(cancellationToken);

        return Result.Success(new CattleListResponse<CowSummaryDto>(items, total, page, pageSize));
    }
}

public sealed class ListBullsQueryHandler : IRequestHandler<ListBullsQuery, Result<List<BullSummaryDto>>>
{
    private readonly IBreedingDbContext _dbContext;

    public ListBullsQueryHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<BullSummaryDto>>> Handle(ListBullsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Cows.AsNoTracking().AsQueryable();

        if (request.TenantId != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == request.TenantId || c.TenantId == Guid.Empty);
        }

        var bulls = await query
            .Where(c => (c.Category == "Reprodutor" || c.Category == "Touro") &&
                        c.Status != ReproductiveStatus.Culled &&
                        c.Status != ReproductiveStatus.Sold)
            .OrderBy(c => c.EarTag)
            .Select(c => new BullSummaryDto(
                c.Id,
                c.EarTag,
                c.Nickname ?? c.EarTag,
                c.Breed,
                c.RegistryNumber,
                true))
            .ToListAsync(cancellationToken);

        return Result.Success(bulls);
    }
}

internal static class CowMappings
{
    public static CowSummaryDto ToSummaryDto(this Cow cow) => new(
        cow.Id,
        cow.EarTag,
        cow.SisbovId,
        cow.RfidTag,
        cow.Nickname,
        cow.Breed,
        cow.Category,
        cow.Status,
        cow.ParityCount,
        cow.LastCalvingDate,
        IepCalculator.CalculateIepMonths(null, cow.LastCalvingDate ?? DateTime.UtcNow),
        cow.BodyConditionScore);

    public static CowDetailDto ToDetailDto(this Cow cow, IReadOnlyList<TimelineEventDto>? timeline = null) => new(
        cow.Id,
        cow.EarTag,
        cow.SisbovId,
        cow.RfidTag,
        cow.Tattoo,
        cow.Nickname,
        cow.RegistryNumber,
        cow.Breed,
        cow.Origin,
        cow.BirthDate,
        cow.EntryDate,
        cow.EntryWeightKg,
        cow.SireInfo,
        cow.DamInfo,
        cow.BodyConditionScore,
        cow.Category,
        cow.Status,
        cow.ParityCount,
        cow.LastCalvingDate,
        IepCalculator.CalculateIepMonths(null, cow.LastCalvingDate ?? DateTime.UtcNow),
        IepCalculator.CalculateOpenDays(cow.LastCalvingDate, DateTime.UtcNow),
        timeline ?? Array.Empty<TimelineEventDto>());
}
