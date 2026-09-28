# Plan-Based Feature Gating & Licensing (1.2) Architecture Guide

## Objective
Implement modular monolith tiered feature gating, locking advanced features based on tenant subscription plan levels (`Starter`, `Pro`, `Enterprise`).

---

## Plan Tiers & Module Access Matrix

The system maps tenants to modules via the `ModuleLicenseChecker` using their JWT `SubscribedPlan` claim:

| Module / Area | Starter Tier | Pro Tier | Enterprise Tier |
| :--- | :---: | :---: | :---: |
| **Breeding** (Plantel) | Yes | Yes | Yes |
| **Maternity** (Farrowing) | Yes | Yes | Yes |
| **Tenancy** (Identity) | Yes | Yes | Yes |
| **Nutrition** (Feeds & Costs) | **Locked** | Yes | Yes |
| **Sanitary** (Vaccines & Logs) | **Locked** | **Locked** | Yes |

### Canonical Plan Identifiers & Resilient Alias Normalization
To prevent access denial due to discrepancies between commercial branding (e.g., Stripe line items or marketing catalogs) and the authorization engine, `ModuleLicenseChecker.NormalizePlan(string? plan)` acts as the single source of truth for canonicalization:
- **Canonical Keys:** `Starter`, `Pro`, `Enterprise`.
- **Supported Aliases:** "Pro Fazenda", "Plano Pro", "Starter Pecuária", "Enterprise Confinamento", versioned releases (e.g. "Pro 2026.1"), and case-insensitive variations.
- **Fail-safe Fallback:** Empty/null inputs safely default to `Starter` tier.

---

## Backend Gating Pipeline

Commands and queries requiring subscription validation are decorated with the `[RequiresModule("ModuleName")]` attribute.

```
[Request Object]
       |
       v (MediatR Bus)
 [ModuleAccessBehavior] -- Check RequiresModuleAttribute
       |
       +---> [Yes] Resolve ITenantContext.SubscribedPlan claim
       |            |
       |            +---> Has access? -> Proceed to Handler.
       |            +---> No access? -> Return failed Result.Failure(License.AccessDenied).
       v
[Handler Execution]
```

- **Functional Results**: To avoid control-flow exceptions, blocked queries/commands return a standard failed `Result` or `Result<T>` with code `License.AccessDenied`.

---

## Web Frontend Guard Shell

- **ModuleGuard.razor**: Route/View wrapper that decodes the tenant's plan.
  - If allowed: Renders children.
  - If locked: Renders the premium **Stitch Block UI** card, prompting the user with value propositions and link to upgrade.
- **SubscriptionManagement.razor**: Config panel featuring animals registered/report quotas, monthly/annual toggles, and tiered pricing grids.
