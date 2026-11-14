using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Application.Contracts;
using CriaCerto.Modules.Breeding.Application.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Breeding.Application.Features.BreedingOps;

[RequiresModule("Breeding")]
public sealed record RegisterIatfProtocolCommand(
    string Name,
    DateTime StartDate,
    DateTime InseminationDate,
    Guid SemenBatchId,
    IReadOnlyList<Guid> CowIds,
    Guid TenantId,
    Guid? BullId = null) : ICommand<IatfProtocolDto>;

[RequiresModule("Breeding")]
public sealed record RegisterPregnancyDiagnosisCommand(
    Guid CowId,
    DateTime DiagnosisDate,
    DiagnosisMethod Method,
    bool IsPregnant,
    Guid TenantId,
    int? GestationalAgeDays = null,
    string? Notes = null,
    bool Force = false) : ICommand<PregnancyDiagnosisDto>;

public sealed record BreedingBatchSyncLine(
    string TagId,
    string? SemenBatchCode = null,
    decimal? BodyConditionScore = null);

[RequiresModule("Breeding")]
public sealed record RecordInseminationCommand(
    string CowTag,
    DateTime InseminationDate,
    string SemenBatchCode,
    Guid TenantId,
    decimal? BodyConditionScore = null,
    bool Force = false) : ICommand<Guid>;

[RequiresModule("Breeding")]
public sealed record RegisterBreedingBatchCommand(
    DateTime EventDate,
    IReadOnlyList<BreedingBatchSyncLine> Lines,
    Guid TenantId,
    bool Force = false) : ICommand<int>;

public sealed class RegisterIatfProtocolCommandValidator : AbstractValidator<RegisterIatfProtocolCommand>
{
    public RegisterIatfProtocolCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CowIds).NotEmpty();
    }
}

public sealed class RegisterPregnancyDiagnosisCommandValidator : AbstractValidator<RegisterPregnancyDiagnosisCommand>
{
    public RegisterPregnancyDiagnosisCommandValidator()
    {
        RuleFor(x => x.CowId).NotEmpty();
        RuleFor(x => x.DiagnosisDate).LessThanOrEqualTo(DateTime.UtcNow);
    }
}

public sealed class RegisterIatfProtocolCommandHandler : IRequestHandler<RegisterIatfProtocolCommand, Result<IatfProtocolDto>>
{
    private readonly IBreedingDbContext _dbContext;

    public RegisterIatfProtocolCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<IatfProtocolDto>> Handle(RegisterIatfProtocolCommand request, CancellationToken cancellationToken)
    {
        string? bullName = null;
        if (request.BullId.HasValue && request.BullId.Value != Guid.Empty)
        {
            var bull = await _dbContext.Cows
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.BullId.Value &&
                                          (request.TenantId == Guid.Empty || c.TenantId == request.TenantId || c.TenantId == Guid.Empty), cancellationToken);

            if (bull is not null)
            {
                bullName = !string.IsNullOrWhiteSpace(bull.Nickname)
                    ? $"{bull.EarTag} - {bull.Nickname} ({bull.Breed})"
                    : $"{bull.EarTag} ({bull.Breed})";
            }
        }

        var protocolResult = IatfProtocol.Create(
            request.Name,
            request.StartDate,
            request.InseminationDate,
            request.SemenBatchId,
            request.CowIds.ToList(),
            request.TenantId,
            request.BullId,
            bullName);

        if (protocolResult.IsFailure)
            return Result.Failure<IatfProtocolDto>(protocolResult.Error);

        var cows = await _dbContext.Cows.Where(c => request.CowIds.Contains(c.Id)).ToListAsync(cancellationToken);
        foreach (var cow in cows)
        {
            var startResult = cow.StartIatfProtocol(protocolResult.Value.Id);
            if (startResult.IsFailure)
                return Result.Failure<IatfProtocolDto>(startResult.Error);
        }

        _dbContext.IatfProtocols.Add(protocolResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new IatfProtocolDto(
            protocolResult.Value.Id,
            protocolResult.Value.Name,
            protocolResult.Value.StartDate,
            protocolResult.Value.InseminationDate,
            protocolResult.Value.SemenBatchId,
            protocolResult.Value.CowIds.Count,
            protocolResult.Value.BullId,
            protocolResult.Value.BullName));
    }
}

public sealed class RegisterPregnancyDiagnosisCommandHandler : IRequestHandler<RegisterPregnancyDiagnosisCommand, Result<PregnancyDiagnosisDto>>
{
    private readonly IBreedingDbContext _dbContext;

    public RegisterPregnancyDiagnosisCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PregnancyDiagnosisDto>> Handle(RegisterPregnancyDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var cow = await _dbContext.Cows.FirstOrDefaultAsync(c => c.Id == request.CowId, cancellationToken);
        if (cow is null)
            return Result.Failure<PregnancyDiagnosisDto>(Error.NotFound("Cow.NotFound", "Matriz bovina não encontrada."));

        var diagResult = PregnancyDiagnosis.Create(request.CowId, request.DiagnosisDate, request.Method, request.IsPregnant, request.TenantId, request.GestationalAgeDays, request.Notes);
        if (diagResult.IsFailure)
            return Result.Failure<PregnancyDiagnosisDto>(diagResult.Error);

        var cowDiagResult = cow.RecordPregnancyDiagnosis(request.IsPregnant, request.DiagnosisDate);
        if (cowDiagResult.IsFailure)
            return Result.Failure<PregnancyDiagnosisDto>(cowDiagResult.Error);

        _dbContext.PregnancyDiagnoses.Add(diagResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new PregnancyDiagnosisDto(
            diagResult.Value.Id,
            cow.Id,
            cow.EarTag,
            diagResult.Value.DiagnosisDate,
            diagResult.Value.Method,
            diagResult.Value.IsPregnant,
            diagResult.Value.GestationalAgeDays,
            diagResult.Value.Notes));
    }
}

[RequiresModule("Breeding")]
public sealed record GetIatfProtocolsQuery(Guid TenantId) : IQuery<List<IatfProtocolDto>>;

public sealed class GetIatfProtocolsQueryHandler : IRequestHandler<GetIatfProtocolsQuery, Result<List<IatfProtocolDto>>>
{
    private readonly IBreedingDbContext _dbContext;

    public GetIatfProtocolsQueryHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<List<IatfProtocolDto>>> Handle(GetIatfProtocolsQuery request, CancellationToken cancellationToken)
    {
        var protocols = await _dbContext.IatfProtocols
            .AsNoTracking()
            .Where(p => p.TenantId == request.TenantId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var dtos = protocols.Select(p => new IatfProtocolDto(
            p.Id,
            p.Name,
            p.StartDate,
            p.InseminationDate,
            p.SemenBatchId,
            p.CowIds.Count,
            p.BullId,
            p.BullName)).ToList();

        return Result.Success(dtos);
    }
}

public sealed class RecordInseminationCommandHandler : IRequestHandler<RecordInseminationCommand, Result<Guid>>
{
    private readonly IBreedingDbContext _dbContext;

    public RecordInseminationCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<Guid>> Handle(RecordInseminationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CowTag))
            return Result.Failure<Guid>(Error.Validation("Cow.EarTagRequired", "O brinco da matriz é obrigatório."));

        var normalizedTag = request.CowTag.Trim();
        var cow = await _dbContext.Cows
            .FirstOrDefaultAsync(c => c.EarTag == normalizedTag &&
                                      (request.TenantId == Guid.Empty || c.TenantId == request.TenantId || c.TenantId == Guid.Empty), cancellationToken);

        if (cow is null)
            return Result.Failure<Guid>(Error.NotFound("Cow.NotFound", $"Matriz '{normalizedTag}' não encontrada."));

        if (request.Force && cow.Status == ReproductiveStatus.Pregnant)
        {
            cow.RecordPregnancyDiagnosis(false, request.InseminationDate);
        }

        var semenCode = string.IsNullOrWhiteSpace(request.SemenBatchCode) ? "LOTE-CAMPO" : request.SemenBatchCode.Trim();
        var insemResult = cow.RecordInsemination(request.InseminationDate, semenCode);
        if (insemResult.IsFailure)
            return Result.Failure<Guid>(insemResult.Error);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(cow.Id);
    }
}

public sealed class RegisterBreedingBatchCommandHandler : IRequestHandler<RegisterBreedingBatchCommand, Result<int>>
{
    private readonly IBreedingDbContext _dbContext;

    public RegisterBreedingBatchCommandHandler(IBreedingDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<int>> Handle(RegisterBreedingBatchCommand request, CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            return Result.Failure<int>(Error.Validation("BreedingBatch.Empty", "O lote de cobrição deve conter ao menos uma matriz."));

        var registeredCount = 0;

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.TagId))
                continue;

            var normalizedTag = line.TagId.Trim();
            var cow = await _dbContext.Cows
                .FirstOrDefaultAsync(c => c.EarTag == normalizedTag &&
                                          (request.TenantId == Guid.Empty || c.TenantId == request.TenantId || c.TenantId == Guid.Empty), cancellationToken);

            if (cow is null)
            {
                var createResult = Cow.Create(normalizedTag, "Nelore", null, request.TenantId, bodyConditionScore: line.BodyConditionScore);
                if (createResult.IsFailure)
                    return Result.Failure<int>(createResult.Error);

                cow = createResult.Value;
                _dbContext.Cows.Add(cow);
            }

            if (request.Force && cow.Status == ReproductiveStatus.Pregnant)
            {
                cow.RecordPregnancyDiagnosis(false, request.EventDate);
            }

            var semenCode = string.IsNullOrWhiteSpace(line.SemenBatchCode) ? "LOTE-CAMPO" : line.SemenBatchCode.Trim();
            var insemResult = cow.RecordInsemination(request.EventDate, semenCode);
            if (insemResult.IsFailure)
                return Result.Failure<int>(insemResult.Error);

            registeredCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(registeredCount);
    }
}


