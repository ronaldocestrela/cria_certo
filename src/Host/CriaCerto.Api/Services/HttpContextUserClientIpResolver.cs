using CriaCerto.Modules.Tenancy.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace CriaCerto.Api.Services;

public sealed class HttpContextUserClientIpResolver : IUserClientIpResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextUserClientIpResolver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? ResolveClientIp()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
