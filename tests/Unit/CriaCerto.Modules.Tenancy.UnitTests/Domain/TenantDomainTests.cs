using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class TenantDomainTests
{
    private static Tenant CreateTenant(string status = "Active", bool isProtected = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Teste",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            Status = status,
            IsProtected = isProtected
        };

    [Fact]
    public void ChangeStatus_WhenValidTransition_ShouldUpdateStatusAndReason()
    {
        var tenant = CreateTenant("Active");
        var reason = "Inadimplência recorrente confirmada pelo financeiro.";

        var result = tenant.ChangeStatus(TenantStatus.Suspended, reason);

        result.IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be("Suspended");
        tenant.StatusReason.Should().Be(reason);
        tenant.StatusChangedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ChangeStatus_WhenInvalidTransition_ShouldReturnFailure()
    {
        var tenant = CreateTenant("Archived");

        var result = tenant.ChangeStatus(TenantStatus.Active, "Tentativa inválida de reativação.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidTransition");
    }

    [Fact]
    public void ChangeStatus_WhenSameStatus_ShouldReturnFailure()
    {
        var tenant = CreateTenant("Active");

        var result = tenant.ChangeStatus(TenantStatus.Active, "Justificativa válida com texto suficiente.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.AlreadyInStatus");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Curto demais")]
    public void ChangeStatus_WhenJustificationInvalid_ShouldReturnFailure(string? reason)
    {
        var tenant = CreateTenant("Active");

        var result = tenant.ChangeStatus(TenantStatus.Suspended, reason!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.JustificationRequired");
    }

    [Fact]
    public void ChangeStatus_WhenProtectedTenant_ShouldBlockSuspend()
    {
        var tenant = CreateTenant("Active", isProtected: true);

        var result = tenant.ChangeStatus(TenantStatus.Suspended, "Tentativa de suspensão em tenant protegido.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ProtectedTenant");
    }

    [Fact]
    public void SetProtection_WhenValidReason_ShouldUpdateFlag()
    {
        var tenant = CreateTenant("Active");

        var result = tenant.SetProtection(true, "Tenant estratégico da plataforma interna.");

        result.IsSuccess.Should().BeTrue();
        tenant.IsProtected.Should().BeTrue();
    }

    [Fact]
    public void SetProtection_WhenSameValue_ShouldReturnFailure()
    {
        var tenant = CreateTenant("Active");
        tenant.IsProtected = true;

        var result = tenant.SetProtection(true, "Justificativa válida com texto suficiente.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.AlreadyProtected");
    }

    [Theory]
    [InlineData("TIER_500", 500)]
    [InlineData("tier_1500", 1500)]
    [InlineData("TIER_3000", 3000)]
    [InlineData("TIER_5000_PLUS", 10000)]
    public void SetHeadTier_WhenValidTierCode_ShouldUpdateHeadTierCodeAndSyncCapacity(string tierCode, int expectedMaxHeads)
    {
        var tenant = CreateTenant("Active");

        var result = tenant.SetHeadTier(tierCode);

        result.IsSuccess.Should().BeTrue();
        tenant.HeadTierCode.Should().Be(tierCode.ToUpperInvariant());
        tenant.Capacity.Should().Be(expectedMaxHeads);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TIER_INVALID")]
    [InlineData("STARTER")]
    public void SetHeadTier_WhenInvalidTierCode_ShouldReturnFailure(string invalidTierCode)
    {
        var tenant = CreateTenant("Active");

        var result = tenant.SetHeadTier(invalidTierCode);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidHeadTierCode");
    }

    [Fact]
    public void SetSubscribedModules_WhenCalledWithoutCoreModule_ShouldAutomaticallyIncludeCows()
    {
        var tenant = CreateTenant("Active");
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cows"] = 79m,
            ["Breeding"] = 49m,
            ["Sanitary"] = 49m
        };
        var itemIds = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cows"] = "si_cows_1",
            ["Breeding"] = "si_breeding_1",
            ["Sanitary"] = "si_sanitary_1"
        };

        var result = tenant.SetSubscribedModules(["breeding", "SANITARY"], prices, itemIds);

        result.IsSuccess.Should().BeTrue();
        tenant.SubscribedModules.Should().HaveCount(3);
        tenant.SubscribedModules.Select(m => m.ModuleKey).Should().BeEquivalentTo(["Cows", "Breeding", "Sanitary"]);
        tenant.SubscribedModules.Single(m => m.ModuleKey == "Cows").MonthlyPriceSnapshot.Should().Be(79m);
        tenant.SubscribedModules.Single(m => m.ModuleKey == "Breeding").StripeSubscriptionItemId.Should().Be("si_breeding_1");
    }

    [Fact]
    public void SetSubscribedModules_WhenInvalidModuleKey_ShouldReturnFailure()
    {
        var tenant = CreateTenant("Active");

        var result = tenant.SetSubscribedModules(["Cows", "ModuloInexistente"]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidModuleKey");
    }

    [Fact]
    public void TenantSubscribedModule_Create_ShouldValidateInvariants()
    {
        var tenantId = Guid.NewGuid();

        var validResult = TenantSubscribedModule.Create(tenantId, "breeding", 89m, "si_123");
        validResult.IsSuccess.Should().BeTrue();
        validResult.Value.ModuleKey.Should().Be("Breeding");
        validResult.Value.MonthlyPriceSnapshot.Should().Be(89m);
        validResult.Value.StripeSubscriptionItemId.Should().Be("si_123");

        var emptyTenantResult = TenantSubscribedModule.Create(Guid.Empty, "Cows", 79m);
        emptyTenantResult.IsFailure.Should().BeTrue();
        emptyTenantResult.Error.Code.Should().Be("Tenant.InvalidSubscribedModuleData");

        var negativePriceResult = TenantSubscribedModule.Create(tenantId, "Cows", -10m);
        negativePriceResult.IsFailure.Should().BeTrue();
        negativePriceResult.Error.Code.Should().Be("Tenant.InvalidSubscribedModuleData");

        var invalidModuleResult = TenantSubscribedModule.Create(tenantId, "UnknownModule", 50m);
        invalidModuleResult.IsFailure.Should().BeTrue();
        invalidModuleResult.Error.Code.Should().Be("Tenant.InvalidModuleKey");
    }
}
