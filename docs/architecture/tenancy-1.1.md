# Identity & Multi-Tenancy (1.1) Architecture Guide

## Objective
Establish the core identity models, database-per-tenant connection resolution, and multi-tenant authentication UI.

## Tenancy Module (`src/Modules/Tenancy`)

### 1. Domain Entities
- **User**: Represents user identity containing `Id`, `Email`, `FullName`, `PasswordHash` (hashed using PBKDF2), `PhoneNumber`, `PasswordResetToken`, and `PasswordResetTokenExpiresAt`.
- **Tenant**: Represents farm organization detailing `Id`, `Name`, `CNPJ`, `Status` (Active/Suspended/Maintenance), `SubscribedPlan` (Starter/Pro/Enterprise), and zootecnic capacity constraints.
- **UserTenant**: Join table mapping users to multiple tenants.

### 2. Database Isolation Strategy
- **Master Database (catálogo configurável via connection string, padrão `criacerto_foundation`)**: Contém os schemas globais `foundation`, `tenancy`, `backoffice` e, no startup, também recebe os schemas dos módulos tenant enquanto não há `TenantId` no contexto HTTP.
- **Tenant Database (`criacerto_tenant_{TenantId:N}`)**: Catálogo independente provisionado por tenant com os schemas `breeding`, `calving`, `growth`, `nutrition` e `sanitary`.

---

## Authentication & User Onboarding Flow

### 1. Double-Step Login Sequence
```
[Client]                                              [Backend]
   |                                                      |
   |--- POST /api/auth/login (Email, Password) ---------->|
   |                                                      |-- Validate credentials
   |<-- 200 OK (RequiresTenantSelection: true) -----------|-- Retrieve mapped tenants
   |                                                      |
   |--- POST /api/auth/select-tenant (UserId, TenantId) ->|
   |                                                      |-- Generate JWT with claims
   |<-- 200 OK (Token: JWT) ------------------------------|   (TenantId, Plan, Name)
```

### 2. User Sign-Up & Password Recovery Flow (Sub-phase 1.2)
```
[Client]                                              [Backend]
   |                                                      |
   |--- POST /api/auth/register (User Data) ------------->|
   |                                                      |-- FluentValidation check
   |                                                      |-- Check duplicate email
   |<-- 201 Created (UserDto) ----------------------------|-- Hash password (PBKDF2)
   |                                                      |
   |--- POST /api/auth/forgot-password (Email) ---------->|
   |                                                      |-- Generate 1h Reset Token
   |<-- 200 OK (Token) -----------------------------------|
   |                                                      |
   |--- POST /api/auth/reset-password (Token, Password) ->|
   |                                                      |-- Validate token expiration
   |<-- 200 OK (Password Updated) ------------------------|-- Clear token & hash new password
```

---

## Web Frontend Design System

All UI elements are implemented with Blazor WebAssembly components using standard Vanilla CSS tokens configured in [app.css](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/app.css).

### Design Variables
- **Headline Font**: `Work Sans`
- **Technical/Label Font**: `JetBrains Mono`
- **Primary Color**: `#00652c` (Deep Green)
- **Primary Container**: `#15803d` (Vibrant Grass Green)
- **Canvas Background**: `#f7f9fb` (Ice White/Light Blue-Gray)
- **Surface Panels**: `#ffffff`

### Main Components
- **Login.razor**: Credentials step featuring scale micro-interactions transitions into a farm select step listing units with specific type badges (Warehouse, Analytics, Agriculture).
- **Register.razor**: User self-registration reactive form featuring real-time client/server validation and Stitch design system components.
- **ForgotPassword.razor**: Two-step password recovery assistant (request reset token -> reset password).
- **OrganizationManagement.razor**: High-fidelity Bento Grid rendering organization stats, AES-256 tenant data isolation status, and active barns tables.
