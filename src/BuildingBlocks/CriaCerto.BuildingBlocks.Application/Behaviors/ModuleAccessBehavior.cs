using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using MediatR;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CriaCerto.BuildingBlocks.Application.Behaviors;

public sealed class ModuleAccessBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITenantContext _tenantContext;

    public ModuleAccessBehavior(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requiresModuleAttr = request.GetType().GetCustomAttribute<RequiresModuleAttribute>();
        if (requiresModuleAttr == null)
        {
            return await next();
        }

        var requiredModule = ModuleLicenseChecker.NormalizeModule(requiresModuleAttr.ModuleName);
        var subscribedModules = _tenantContext.SubscribedModules;

        if (!ModuleLicenseChecker.HasModuleAccess(subscribedModules, requiredModule))
        {
            var error = Error.Unauthorized(
                "License.ModuleNotSubscribed",
                $"O módulo '{requiredModule}' não está ativo na assinatura atual da fazenda. Adicione o módulo '{requiredModule}' à sua assinatura para acessar este recurso.");

            if (typeof(Result).IsAssignableFrom(typeof(TResponse)) && typeof(TResponse).IsClass)
            {
                if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var valueType = typeof(TResponse).GetGenericArguments()[0];
                    var failureMethod = typeof(Result)
                        .GetMethods()
                        .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                        .MakeGenericMethod(valueType);
                    return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
                }

                // If non-generic Result
                var failureNonGenericMethod = typeof(Result)
                    .GetMethods()
                    .First(m => m.Name == nameof(Result.Failure) && !m.IsGenericMethod);
                return (TResponse)failureNonGenericMethod.Invoke(null, new object[] { error })!;
            }

            throw new System.UnauthorizedAccessException($"Acesso negado ao módulo '{requiredModule}'.");
        }

        return await next();
    }
}
