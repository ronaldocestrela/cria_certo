using System.Security.Claims;

namespace CriaCerto.Shared.UI.Services;

public interface IBackofficePermissionService
{
    Task<bool> HasPermissionAsync(string permission, string scope = "Global");
    Task<string> GetCurrentUserRoleAsync();
    void SetCurrentUser(ClaimsPrincipal user);
}
