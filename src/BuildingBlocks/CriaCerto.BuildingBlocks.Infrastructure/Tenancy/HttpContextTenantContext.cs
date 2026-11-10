using System.Security.Claims;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CriaCerto.BuildingBlocks.Infrastructure.Tenancy;

public sealed class HttpContextTenantContext : ITenantContext
{
    private static readonly IReadOnlySet<string> EmptyModules =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var tenantClaim = user?.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
        }
    }

    public string? SubscribedPlan
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("SubscribedPlan")?.Value;
        }
    }

    public string? HeadTier
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var tierClaim = user?.FindFirst("head_tier")?.Value
                            ?? user?.FindFirst("HeadTier")?.Value;

            return string.IsNullOrWhiteSpace(tierClaim)
                ? null
                : tierClaim.Trim().ToUpperInvariant();
        }
    }

    public int HeadCapacity
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var capacityClaim = user?.FindFirst("head_capacity")?.Value
                                ?? user?.FindFirst("HeadCapacity")?.Value;

            if (int.TryParse(capacityClaim, out var capacity) && capacity > 0)
            {
                return capacity;
            }

            var tier = HeadTier;
            if (!string.IsNullOrWhiteSpace(tier))
            {
                return tier switch
                {
                    "TIER_500" => 500,
                    "TIER_1500" => 1500,
                    "TIER_3000" => 3000,
                    "TIER_5000_PLUS" => 10000,
                    _ => 500
                };
            }

            return TenantId.HasValue ? 500 : 0;
        }
    }

    public IReadOnlySet<string> SubscribedModules
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return EmptyModules;
            }

            var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var claim in user.FindAll("subscribed_module"))
            {
                var normalized = ModuleLicenseChecker.NormalizeModule(claim.Value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    modules.Add(normalized);
                }
            }

            foreach (var csvClaim in user.FindAll("subscribed_modules"))
            {
                if (string.IsNullOrWhiteSpace(csvClaim.Value))
                {
                    continue;
                }

                var parts = csvClaim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var part in parts)
                {
                    var normalized = ModuleLicenseChecker.NormalizeModule(part);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        modules.Add(normalized);
                    }
                }
            }

            if (modules.Count > 0)
            {
                modules.Add("Cows");
                return modules;
            }

            if (TenantId.HasValue)
            {
                modules.Add("Cows");
                return modules;
            }

            return EmptyModules;
        }
    }
}
