using CriaCerto.Modules.Tenancy.Application.Contracts;

namespace CriaCerto.Modules.Backoffice.UnitTests.TestData;

internal static class TenantBackofficeTestData
{
    public static TenantBackofficeDetailDto CreateDetail(
        Guid tenantId,
        string status = "Active",
        string churnRisk = "None") =>
        new(
            tenantId,
            "Fazenda",
            null,
            "12.345.678/0001-90",
            null,
            status,
            "Starter",
            500,
            500,
            false,
            "MT",
            "Sinop",
            "IE",
            1000,
            "Corte",
            "Medium",
            "CentroOeste",
            "Corte",
            churnRisk,
            Array.Empty<TenantOperationalTagDto>(),
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            0,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
