using System.Reflection;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Security;

public sealed class FeatureFlagEvaluationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly DbContext _dbContext;
    private readonly IFeatureFlagEvaluator _evaluator;

    public FeatureFlagEvaluationBehavior(
        DbContext dbContext,
        IFeatureFlagEvaluator evaluator)
    {
        _dbContext = dbContext;
        _evaluator = evaluator;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requireFlagAttr = request.GetType().GetCustomAttribute<RequireFeatureFlagAttribute>();
        if (requireFlagAttr is null)
        {
            return await next();
        }

        var normalizedKey = requireFlagAttr.FeatureFlagKey.Trim().ToLowerInvariant();
        var flag = await _dbContext.Set<FeatureFlag>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == normalizedKey, cancellationToken);

        // Se a flag não existe
        if (flag is null)
        {
            return CreateFailureResult(FeatureFlagErrors.NotFound);
        }

        // Se o Kill-Switch estiver ativo, corte emergencial absoluto
        if (flag.KillSwitchActive)
        {
            return CreateFailureResult(FeatureFlagErrors.KillSwitchActive);
        }

        // Se a flag estiver globalmente desabilitada
        if (!flag.IsEnabled)
        {
            return CreateFailureResult(FeatureFlagErrors.FeatureFlagDisabled);
        }

        // Determinar identidade do ator
        var (actorEmail, actorRole) = ResolveActorContext(request);

        var isEligible = _evaluator.Evaluate(flag, actorEmail, actorRole);
        if (!isEligible)
        {
            return CreateFailureResult(FeatureFlagErrors.WaveNotReached);
        }

        return await next();
    }

    private (string Email, string Role) ResolveActorContext(TRequest request)
    {
        if (request is IBackofficeActorRequest actorReq)
        {
            var role = !string.IsNullOrWhiteSpace(actorReq.ActorRole)
                ? actorReq.ActorRole
                : (actorReq.ActorEmail.Equals("admin@criacerto.com.br", StringComparison.OrdinalIgnoreCase)
                    ? "PlatformOwner"
                    : "SupportN1");

            return (actorReq.ActorEmail, role);
        }

        // Fallback via reflexão caso o comando tenha propriedades ActorEmail e ActorRole
        var emailProp = request.GetType().GetProperty("ActorEmail")?.GetValue(request) as string;
        var roleProp = request.GetType().GetProperty("ActorRole")?.GetValue(request) as string;

        if (!string.IsNullOrWhiteSpace(emailProp))
        {
            var role = !string.IsNullOrWhiteSpace(roleProp)
                ? roleProp
                : (emailProp.Equals("admin@criacerto.com.br", StringComparison.OrdinalIgnoreCase)
                    ? "PlatformOwner"
                    : "SupportN1");

            return (emailProp, role);
        }

        return ("system@criacerto.com.br", "PlatformOwner");
    }

    private static TResponse CreateFailureResult(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods()
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                .MakeGenericMethod(valueType);

            return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
        }

        throw new InvalidOperationException($"Não foi possível instanciar Result de falha para o tipo {typeof(TResponse).Name}");
    }
}
