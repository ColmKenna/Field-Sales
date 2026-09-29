# Field Sales

[![CI](https://github.com/ColmKenna/IdentityServerWithAdminTemplate/actions/workflows/ci.yml/badge.svg)](https://github.com/ColmKenna/IdentityServerWithAdminTemplate/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

The Field Sales .NET 10 solution uses **Duende IdentityServer**, **ASP.NET Core Identity**, a staff Razor Pages website and a protected API. The identity host includes the `/Admin` console for users, roles and OIDC configuration.

The first staff website and business API are now part of this solution. The identity host still owns credentials and the `/Admin` console.

---

## 📑 Table of Contents

- [Overview & Architecture](#overview--architecture)
- [Key Features](#key-features)
  - [IdentityServer Host & Security](#identityserver-host--security)
  - [Admin Console UI](#admin-console-ui)
- [Project Structure](#project-structure)
- [Database Contexts](#database-contexts)
- [Prerequisites](#prerequisites)
- [Creating a New Instance from This Template](#creating-a-new-instance-from-this-template)
- [Getting Started](#getting-started)
  - [1. Configure User Secrets](#1-configure-user-secrets)
  - [2. Run the Solution (.NET Aspire)](#2-run-the-solution-net-aspire)
  - [3. Default Ports & Endpoints](#3-default-ports--endpoints)
  - [4. Development Seed Data](#4-development-seed-data)
- [Creating the First Administrator](#creating-the-first-administrator)
- [Database Migrations](#database-migrations)
- [Testing](#testing)
- [Configuration Reference](#configuration-reference)
- [Production Checklist](#production-checklist)
- [License](#license)

---

## Overview & Architecture

The identity host signs staff into one Razor Pages website, which calls the protected API on their behalf:

```text
                      +----------------------------------------+
                      |              .NET Aspire               |
                      |                (AppHost)               |
                      +-------------------+--------------------+
                                          |
                                          v
                          +---------------------------------+
                          |       FieldSales.Identity     |
                          |   Duende IdentityServer 8       |
                          |   + ASP.NET Core Identity       |
                          |   + /Admin Console UI           |
                          +----------------+----------------+
                                           |
                     +---------------------+---------------------+
                     |                     |                     |
                     v                     v                     v
              [IdentityDb]        [IdentityConfigDb]  [IdentityOperationalDb]
               users, roles          clients, scopes    grants, tokens, keys
               audit, DP keys        API resources      consents, sessions

       Staff browser --> [FieldSales.Web :7203] --> [FieldSales.Api :7204]
                              |
                         [StaffWebDb]                  [CatalogueDb]
                    encrypted tickets, DP keys, last area
```

`FieldSales.Web` uses authorization code with PKCE. Its browser cookie holds an opaque session key;
encrypted OIDC tickets and tokens live in `StaffWebDb`. `FieldSales.Api` validates the bearer token,
audience, scope and a business role at `/staff/session`. The Head Office category pages
call protected catalogue endpoints; category identities and parent links live in `CatalogueDb`.

- **FieldSales.Identity**: The primary authentication host running Duende IdentityServer with ASP.NET Core Identity. Houses the Razor Pages UI for account workflows (Login, Logout, Access Denied) and the `/Admin` management console.
- **FieldSales.Identity.Admin.Services**: A decoupled domain services library containing the business logic, validation, audit generation, and management operations for the admin console. It has no reference to the host's `DbContext` or user type; the host supplies adapters for the persistence ports it defines.
- **AppHost / ServiceDefaults**: .NET Aspire orchestration and shared service defaults (OpenTelemetry, health checks, resilience).
- **FieldSales.Web**: One staff Razor Pages BFF with I-01 entry, I-02 area choice, last-used permitted routing, protected rep/manager/head-office landings, server-side token refresh and sign-out. Head Office users can create root and nested categories at `/HeadOffice/Categories`.
- **FieldSales.Api**: Protected staff session and catalogue endpoints used by the BFF.

> [!NOTE]
> **SQL Server is a deliberate, load-bearing choice, not a default you can flip.** All five
> `DbContext` registrations use `UseSqlServer`, the committed migrations are SQL Server-specific,
> and the AppHost provisions a SQL Server container. Moving to PostgreSQL or another provider means
> changing those registrations *and* regenerating all five migration histories — the one
> substitution this template does not make cheap. Decide before you build on it.

---

## Key Features

### IdentityServer Host & Security

- **Duende IdentityServer 8 on .NET 10**: Fully configured OpenID Connect (OIDC) and OAuth 2.0 authorization server.
- **ASP.NET Core Identity Integration**: User account store with password hashing, account lockout, role management, and claim handling.
- **Enterprise Security Defaults**:
  - **Immediate Session Invalidation**: `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` ensures credentials and tokens revoked in the admin console take effect on the next request.
  - **Deny-by-Default Authorization**: A global fallback policy requires an authenticated user, so a page added outside the `/Admin` convention fails closed rather than being served anonymously. Anonymous routes state so explicitly.
  - **Signing Certificate Enforcement**: Outside Development the host refuses to start unless `IdentityServer:SigningCertificatePath` and its password are configured. There is no silent fallback to a developer signing key.
  - **ASP.NET Core Data Protection**: Keys persisted to EF Core (`ApplicationDbContext`), and encrypted at rest with a certificate that is likewise mandatory outside Development.
  - **No Administrator Resurrection**: Ordinary startup performs no administrator seeding, so an account deleted or demoted through the console stays that way across restarts. See [Creating the First Administrator](#creating-the-first-administrator).
  - **Complete Front-Channel Logout**: The post-logout page renders IdentityServer's `SignOutIFrameUrl`, so registered clients clear their own sessions before the user follows the validated return link.
  - **Operational Token Cleanup**: Expired authorization codes, refresh tokens, and reference tokens are purged on an interval rather than accumulating in `PersistedGrants` indefinitely.
  - **Defensive HTTP Headers**: Enforces `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, and `Referrer-Policy: strict-origin-when-cross-origin`.
  - **Production Startup Checks**: `IDatabaseSchemaReadinessValidator` validates that all migrations are in place prior to launching or seeding.
  - **Protected Administrator Guards**: Built-in protection prevents accidental deletion or demotion of the last active system administrator.

### Admin Console UI

The `/Admin` section is restricted to users in the `SysAdmin` role and provides a dashboard and management tools:

| Module | Description |
|---|---|
| **Clients** | Create and manage OAuth2/OIDC clients using presets (SPA, Web App, M2M, Blazor). Manage redirect/post-logout URIs, allowed scopes, grant types, token lifetimes, and secrets with secure one-time reveals. Supports client cloning. |
| **API Resources** | Register protected APIs, map associated user claims, and configure resource secrets. |
| **API Scopes** | Define granular authorization scopes and manage resource associations. |
| **Identity Resources** | Manage OIDC standard scopes (`openid`, `profile`, `email`, `roles`) with protection against editing built-in configurations. |
| **Users** | User search and pagination, profile editing, email confirmation toggles, password resets, role assignment, and custom claims. |
| **Roles** | Role creation and management with safeguards against modifying protected administrator roles. |
| **Persisted Grants** | Search, view, and revoke active authorization codes, refresh tokens, user consent grants, and reference tokens. |
| **Audit Logs** | Filterable, structured audit log tracking all security events, administrator mutations, outcomes, and failure reasons. |
| **Diagnostics** | Live status checks covering database connectivity, store health, signing credentials, and configuration warnings. |
| **Signing Keys** | View active and retired cryptographic signing keys. |

---

## Project Structure

```text
.
├── FieldSales.Identity/
│   └── src/FieldSales.Identity/         # Duende IdentityServer host & /Admin Razor Pages UI
├── FieldSales.Identity.Admin.Services/  # Domain services, validation & audit logic for Admin UI
├── FieldSales.Identity.Admin.Tests/     # Unit, integration, characterization & audit coverage tests
├── FieldSales.Web/                      # Staff Razor Pages BFF and server-side sessions
├── FieldSales.Web.Tests/                # Staff request-boundary and session security tests
├── FieldSales.Api/                      # Protected business API
├── FieldSales.Api.Tests/                # Bearer authorization tests
├── FieldSales.AppHost/                  # .NET Aspire AppHost orchestrator
├── FieldSales.ServiceDefaults/          # Aspire service defaults (OTel, health checks, resilience)
├── docs/                                # Template adoption record and reviews
├── plan_docs/                           # Field Sales stories, UX docs and delivery console
├── scripts/                             # Utility scripts (e.g. migration bundle generation)
├── aspire.config.json                   # Aspire tooling entry point (names the AppHost project)
├── Directory.Packages.props             # Central Package Management (CPM)
├── global.json                          # .NET SDK configuration
└── FieldSales.slnx                      # Solution definition
```

---

## Database Contexts

The solution separates identity, API configuration, grants, staff sessions, and catalogue data across five Entity Framework Core `DbContext` instances:

1. **`ApplicationDbContext`** (Database: `IdentityDb`):
   - ASP.NET Core Identity (Users, Roles, UserClaims, UserRoles, Logins, Tokens).
   - Administrative audit log entries (`AuditLogEntry`).
   - Bound secret reveal metadata (`SecretRevealRecord`).
   - ASP.NET Core Data Protection key repository (`DataProtectionKeys`).
2. **`ConfigurationDbContext`** (Database: `IdentityConfigDb`):
   - Duende IdentityServer configuration store (Clients, Identity Resources, API Resources, API Scopes).
3. **`PersistedGrantDbContext`** (Database: `IdentityOperationalDb`):
   - Duende IdentityServer operational store (Authorization codes, refresh tokens, reference tokens, user consent, signing keys).
4. **`StaffWebDbContext`** (Database: `StaffWebDb`):
   - Protected server-side staff tickets and the website's Data Protection keys.
5. **`CatalogueDbContext`** (Database: `CatalogueDb`):
   - Head Office categories with parent links and sibling-unique names.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (feature band `10.0.100` or later, per `global.json`)
- [Docker Desktop](https://www.docker.com/) or a compatible container runtime — required both for the .NET Aspire SQL Server container and for the SQL Server integration tests
- [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) (`pwsh`) — required to run `scripts/Create-MigrationBundles.ps1`. Pre-installed on GitHub Actions' `ubuntu-latest` runners; install locally with `dotnet tool install --global PowerShell` if `pwsh` is not already on your `PATH`.
- Node.js (v18+) *optional, only needed for the front-end component tests in `FieldSales.Identity`*

---

## Creating a New Instance from This Template

This repository was created from the IdentityServerWithAdminTemplate GitHub template and has
already been re-branded to `FieldSales`, so the template's one-shot rename script has been
removed. The adopted upstream revision and its verification are recorded in
[docs/template-adoption.md](docs/template-adoption.md).

---

## Getting Started

### 1. Configure User Secrets

Before launching via Aspire, configure the required development secrets for `AppHost`:

```pwsh
cd FieldSales.AppHost

dotnet user-secrets set "Parameters:sql-password"            "YourStrong@SA!Password"
dotnet user-secrets set "Parameters:staff-web-client-secret" "a-random-secret-for-the-staff-web-client"
dotnet user-secrets set "Parameters:seed-sysadmin-password"  "SysAdminPass123!"
dotnet user-secrets set "Parameters:seed-test-user-password" "TestUserPass123!"
```

None of these have a built-in default. AppHost fails fast naming the missing key rather than
falling back to a committed credential.

### 2. Run the Solution (.NET Aspire)

Run the AppHost project to spin up SQL Server and all dependencies:

```pwsh
dotnet run --project FieldSales.AppHost
```

Aspire will output the URL for the **Aspire Dashboard**, from which you can monitor logs, traces, metrics, and inspect running endpoints.

That is the whole first run. In Development the host applies all pending migrations itself before
serving, so a fresh clone needs no migration-bundle step and no manual port discovery — see
[Database Migrations](#database-migrations) for the deployment path used outside Development.

### 3. Default Ports & Endpoints

| Service | Port / URL | Description |
|---|---|---|
| **IdentityServer Host** | `https://localhost:7201` | OIDC discovery endpoint (`/.well-known/openid-configuration`) & Admin Console (`/Admin`) |
| **Staff website** | `https://localhost:7203` | I-01 staff entry and permitted areas |
| **Staff API** | `https://localhost:7204` | Protected `/staff/session` endpoint |
| **Aspire Dashboard** | Dynamic (see console output) | Telemetry, logs, and distributed application management |

### 4. Development Seed Data

In **Development only**, the host seeds its initial identity configuration and test accounts so
the console is usable immediately. No seeding of any kind occurs in other environments.

The `fieldsales-staff-web` confidential OIDC client is registered for `https://localhost:7203`,
with the `fieldsales.api` scope and `fieldsales-api` resource. The registration permits `roles`
and `offline_access` for server-held refresh tokens. The website and API start with AppHost.
The three business-area pages currently contain protected landing content; area choice,
last-used routing and switching are WI-002 Increment 4.

The former `razorclient` and `blazorclient` registrations are no longer seeded. They can remain
in a persistent Development database created before WI-002; a SysAdmin can review and remove
those unused records through **Admin → Clients**. Existing registrations are not deleted at startup.

A development administrator and a standard test user are also seeded:

| Account | Email | Password source | Role |
|---|---|---|---|
| System Administrator | `Seed:SysAdminEmail` (`admin@sales.local` in `appsettings.Development.json`) | `Seed:SysAdminPassword`, from `Parameters:seed-sysadmin-password` | `SysAdmin` |
| Standard Test User | `testuser@sales.local` | `Seed:TestUserPassword`, from `Parameters:seed-test-user-password` | *(none)* |

To provision staff, a SysAdmin creates the `Field Salesperson`, `Sales Manager` and
`Head Office User` roles through **Admin → Roles**, then creates staff accounts through
**Admin → Users** and assigns their held roles. `SysAdmin` is only for identity administration;
it does not grant a staff website area. **Suspend** on a user account sets an indefinite lockout,
which rejects its next password sign-in. A SysAdmin can unlock the account or reset its password
through the existing user administration pages. Staff accounts and business roles are not
automatically seeded in any environment.

---

## Creating the First Administrator

Outside Development, **no administrator is created at startup**. This is deliberate: seeding an
administrator on every boot means an account you delete or demote through the console reappears
the next time the process restarts, silently undoing a revocation.

Instead, provision the first administrator with an explicit one-time command. It connects
directly to `IdentityDb`, so the schema must already exist (migration bundles applied) and the
connection string must be supplied — outside Aspire nothing injects it for you:

```pwsh
dotnet user-secrets set "AdminBootstrap:Email"    "admin@your-company.example"
dotnet user-secrets set "AdminBootstrap:Password" "<a strong password>"

$env:ConnectionStrings__IdentityDb = "Server=...;Database=IdentityDb;User ID=...;Password=...;TrustServerCertificate=True"
dotnet run --project FieldSales.Identity/src/FieldSales.Identity -- --bootstrap-admin
```

Credentials come from configuration — user secrets, environment variables, or your platform's
secret store. Without a reachable `IdentityDb` the command fails with
`ConnectionString is missing ... 'ConnectionStrings:IdentityDb'` before it does any work.

The command:

- creates the `SysAdmin` role if it does not yet exist;
- creates the user and assigns the role;
- **refuses to modify an account that already exists** — it logs a warning and exits without
  touching passwords or role assignments, so it can never be used to re-elevate a demoted user;
- exits the process when finished rather than continuing into normal web startup.

It falls back to `Seed:SysAdminEmail` / `Seed:SysAdminPassword` when the `AdminBootstrap:*` keys
are not set.

---

## Database Migrations

Each `DbContext` has dedicated migrations:
- `Migrations/Application` (`ApplicationDbContext`)
- `Migrations/Configuration` (`ConfigurationDbContext`)
- `Migrations/Operational` (`PersistedGrantDbContext`)
- `FieldSales.Web/Data/Migrations` (`StaffWebDbContext`)
- `FieldSales.Api/Catalogue/Migrations` (`CatalogueDbContext`)

### First Run with .NET Aspire

**In Development, nothing here is required.** The hosts apply pending migrations for all five
contexts on startup, so a fresh clone reaches a running application with a single
`dotnet run --project FieldSales.AppHost`. Re-running is a no-op once the schema is current.

**Outside Development the host never creates or migrates schemas.** It verifies migration state and
refuses to start when anything is pending, naming the bundle to run. Schema changes stay a
deployment step there, applied through the reviewed bundles below.

The rest of this section is therefore for **Staging, Production, and any environment where you want
to rehearse the deployment path** rather than for ordinary local development.

From the repository root, build the bundles:

```pwsh
dotnet tool restore
./scripts/Create-MigrationBundles.ps1
```

With AppHost still running, use its active SQL Server container and the AppHost SQL password to run
the bundles in order. This snippet discovers Aspire's dynamically assigned host port and does not
print the password:

```pwsh
$sqlContainer = docker ps --filter "name=sqlserver" --format "{{.Names}}" | Select-Object -First 1
if (-not $sqlContainer) { throw "No running AppHost SQL Server container was found." }

$sqlPort = (docker port $sqlContainer 1433/tcp | Select-Object -First 1) -replace '^.*:', ''
$secretLine = dotnet user-secrets list --project FieldSales.AppHost/FieldSales.AppHost.csproj |
    Where-Object { $_ -match '^Parameters:sql-password\s*=\s*(.+)$' } |
    Select-Object -First 1
if (-not $secretLine) { throw "The AppHost SQL password is not configured." }
$sqlPassword = [regex]::Match($secretLine, '^Parameters:sql-password\s*=\s*(.+)$').Groups[1].Value

function Invoke-MigrationBundle([string] $bundle, [string] $database) {
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection['Data Source'] = "127.0.0.1,$sqlPort"
    $connection['Initial Catalog'] = $database
    $connection['User ID'] = 'sa'
    $connection['Password'] = $sqlPassword
    $connection['TrustServerCertificate'] = 'True'
    & "./artifacts/migrations/$bundle.exe" --connection $connection.ConnectionString
    if ($LASTEXITCODE -ne 0) { throw "$bundle migration bundle failed." }
}

Invoke-MigrationBundle identity IdentityDb
Invoke-MigrationBundle configuration IdentityConfigDb
Invoke-MigrationBundle operational IdentityOperationalDb
Invoke-MigrationBundle 'staff-web' StaffWebDb
Invoke-MigrationBundle catalogue CatalogueDb
```

After all five commands report `Done.`, restart the `identityserver`, `staff-api`, and `staff-web` resources in the Aspire
Dashboard (or restart AppHost). The bundles are idempotent, so it is safe to run them again when
deploying a new migration.

### Generating Migration Bundles for Production

Use the provided PowerShell script to build self-contained EF Core migration executables for CI/CD deployments:

```pwsh
./scripts/Create-MigrationBundles.ps1 -OutputDir "artifacts/migrations"
```

---

## Testing

```pwsh
# Whole solution
dotnet test FieldSales.slnx

# The admin services, page models, and integration suites
dotnet test FieldSales.Identity.Admin.Tests/FieldSales.Identity.Admin.Tests.csproj
```

**Docker must be running.** A portion of the suite provisions real SQL Server containers via
Testcontainers to cover migration adoption, concurrency, and cross-instance behaviour that an
in-memory provider cannot represent. Without a container runtime those tests fail to connect
rather than skipping.

Front-end component tests for the admin console pages are run separately with Node:

```pwsh
cd FieldSales.Identity/src/FieldSales.Identity
npm install
npm run test:admin-ui
```

---

## Configuration Reference

Key configuration sections in `FieldSales.Identity`:

```json
{
  "AdminConsole": {
    "DefaultPageSize": 10
  },
  "Clients": {
    "StaffWebUri": "https://localhost:7203",
    "StaffWebSecret": "<secret>"
  },
  "Seed": {
    "SysAdminEmail": "admin@sales.local",
    "SysAdminPassword": "<password>",
    "TestUserPassword": "<password>"
  },
  "AdminBootstrap": {
    "Email": "<administrator email>",
    "Password": "<password>"
  },
  "DataProtection": {
    "CertificatePath": "path/to/cert.pfx",
    "CertificatePassword": "<cert-password>"
  },
  "IdentityServer": {
    "SigningCertificatePath": "path/to/signing-cert.pfx",
    "SigningCertificatePassword": "<cert-password>"
  }
}
```

The `Clients` and `Seed` sections are consumed only by the Development seeder. `AdminBootstrap`
is read only by `--bootstrap-admin`. The two certificate sections are required in every
environment except Development and Testing.

---

## Production Checklist

- [ ] `IdentityServer:SigningCertificatePath` and password configured — the host will not start without them.
- [ ] `DataProtection:CertificatePath` and password configured for the identity host and staff website, so both key rings are encrypted at rest.
- [ ] All five migration bundles applied to their databases.
- [ ] First administrator created with `--bootstrap-admin`, and the bootstrap credentials removed from configuration afterwards.
- [ ] `fieldsales-staff-web` client, `fieldsales.api` scope and `fieldsales-api` resource registered through **Admin → Clients / API Scopes / APIs**. Development seeding does not run in production.
- [ ] The three staff business roles created and staff accounts assigned through **Admin → Roles / Users**.
- [ ] A Duende IdentityServer license configured if you exceed the free tier — see below.

---

## License

This template's own code is licensed under the [MIT License](LICENSE).

> [!IMPORTANT]
> **Duende IdentityServer is a commercial product**, separately licensed by Duende Software and not
> covered by this repository's MIT license. It is free for development and testing, and for
> qualifying companies and open-source projects, but production use otherwise requires a paid
> license. Review the [Duende licensing terms](https://duendesoftware.com/products/identityserver)
> before deploying, and configure a license key per Duende's instructions once you have one.
