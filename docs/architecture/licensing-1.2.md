# Modular Self-Service Feature Gating & Licensing Architecture Guide

## Objective
Implement modular monolith self-service feature gating (`À La Carte`) and tiered head capacity limits (`HeadTierCapacityLimits`), decoupling module authorization from legacy static subscription plans (`Starter`, `Pro`, `Enterprise`).

---

## Modular Gating & Core vs. Add-on Modules

The system authorizes module access via `ModuleLicenseChecker.HasModuleAccess(IEnumerable<string>? subscribedModules, string? requiredModule)` using the tenant's active `SubscribedModules` set resolved from JWT claims (`subscribed_module` / `subscribed_modules`):

| Module / Area | Key (`ModuleKey`) | Classification | Access Rule |
| :--- | :---: | :---: | :--- |
| **Gestão do Plantel & Lotes Básicos** | `Cows` | **Core (Obrigatório)** | Always allowed for any active/trial tenant |
| **Identidade & Organização** | `Tenancy` | **Core (Sistema)** | Always allowed |
| **Reprodução, IATF & DG** | `Breeding` | Add-on Self-Service | Requires `"Breeding"` in `SubscribedModules` |
| **Maternidade, Partos & Desmama** | `Calving` | Add-on Self-Service | Requires `"Calving"` in `SubscribedModules` |
| **Recria, Engorda, Pesagens & Pastos** | `Growth` | Add-on Self-Service | Requires `"Growth"` in `SubscribedModules` |
| **Manejo Sanitário & Carência** | `Sanitary` | Add-on Self-Service | Requires `"Sanitary"` in `SubscribedModules` |
| **Nutrição, Silos & Trato TMR** | `Nutrition` | Add-on Self-Service | Requires `"Nutrition"` in `SubscribedModules` |
| **Inteligência Zootécnica & BI** | `Analytics` | Add-on Self-Service | Requires `"Analytics"` in `SubscribedModules` |

### Separation between `Cows` (Base Herd) and `Breeding` (IATF/DG)
- **Plantel Base (`[RequiresModule("Cows")]`):** Basic animal registry (`CreateCowCommand`, `UpdateCowCommand`, `GetCowQuery`, `ListCowsQuery`, `ListBullsQuery`) and basic lot management (`CreateLotCommand`, `MoveLotToPaddockCommand`, `CloseLotCommand`, `GetLotsQuery`) require `"Cows"`.
- **Reprodução Especializada (`[RequiresModule("Breeding")]`):** IATF protocols (`RegisterIatfProtocolCommand`, `GetIatfProtocolsQuery`) and pregnancy diagnoses (`RegisterPregnancyDiagnosisCommand`) require `"Breeding"`. Having `"Cows"` does not grant implicit access to `"Breeding"`.

---

## Head Tier Capacity Limits (`HeadTierCapacityLimits`)

Animal registry capacity is governed by the tenant's contracted head tier (`tenant.HeadTierCode` and `tenant.Capacity`):
- `TIER_500`: up to `500` heads (`Tier500MaxHeads`)
- `TIER_1500`: up to `1,500` heads (`Tier1500MaxHeads`)
- `TIER_3000`: up to `3,000` heads (`Tier3000MaxHeads`)
- `TIER_5000_PLUS`: up to `10,000` heads (`Tier5000PlusMaxHeads`)

When registering new animals (`CreateCowCommandHandler`), if the active herd count reaches `ITenantContext.HeadCapacity`, the operation fails with `License.HeadCapacityExceeded` (`ErrorType.Validation`).

---

## Backend Gating Pipeline

Commands and queries requiring module verification are decorated with `[RequiresModule("ModuleName")]`.

```
[Request Object]
       |
       v (MediatR Bus)
 [ModuleAccessBehavior] -- Check RequiresModuleAttribute
       |
       +---> [Yes] Resolve ITenantContext.SubscribedModules
       |            |
       |            +---> ModuleLicenseChecker.HasModuleAccess(SubscribedModules, requiredModule)
       |            +---> Has access? -> Proceed to Handler.
       |            +---> No access? -> Return failed Result.Failure(License.ModuleNotSubscribed).
       v
[Handler Execution]
```

- **Functional Results**: Blocked commands/queries return `Result` or `Result<T>` with `Error.Unauthorized("License.ModuleNotSubscribed", ...)` indicating which module must be added to the subscription.
- **Modular Self-Service Context (`ITenantContext` - 2.1 & 2.2)**: `ITenantContext` exposes `IReadOnlySet<string> SubscribedModules`, `int HeadCapacity`, and `string? HeadTier`, resolved by `HttpContextTenantContext` from JWT claims.

---

## Web Frontend Guard Shell

- **ModuleGuard.razor**: Extracts `subscribed_module` and `subscribed_modules` claims from the authenticated `ClaimsPrincipal` and evaluates `ModuleLicenseChecker.HasModuleAccess(subscribedModules, Module)`.
  - Base herd pages (`Registry.razor`, `AnimalDetail.razor`) guard against `Module="Cows"`.
  - Specialized reproductive pages (`IatfProtocols.razor`, `BatchBreedingLog.razor`, `PregnancyCheckTasks.razor`) guard against `Module="Breeding"`.
