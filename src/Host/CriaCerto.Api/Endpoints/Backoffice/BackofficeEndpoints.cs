using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints.Backoffice;

public static class BackofficeEndpoints
{
    public static IEndpointRouteBuilder MapBackofficeEndpoints(this IEndpointRouteBuilder app)
    {
        // Public anonymous auth endpoints with rate limiting
        app.MapBackofficeAuthEndpoints();

        // Main authenticated Backoffice Route Group
        var backoffice = app.MapGroup("/api/v1/backoffice")
            .RequireAuthorization();

        backoffice.MapBackofficeGovernanceEndpoints();
        backoffice.MapBackofficeUserEndpoints();
        backoffice.MapBackofficeTenantEndpoints();
        backoffice.MapBackofficeCatalogEndpoints();
        backoffice.MapBackofficeReferralEndpoints();

        return app;
    }
}

