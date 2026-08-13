using System.Security.Claims;
using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Security;

public interface IPermissionEvaluator
{
    Result<bool> HasPermission(ClaimsPrincipal? user, string permissionName, string requiredScope = BackofficePermissions.ScopeGlobal);
}
