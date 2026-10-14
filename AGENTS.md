# Repository Guidelines

## Project Structure & Module Organization

`One.sln` targets .NET 10. `One.Contracts/` owns transport results, DTOs, enums, validation attributes, and lightweight client models; current linked source files retain their `One.Domain.*` namespaces for compatibility. `One.Domain/` owns entities and server domain rules. `One.Application/` owns repositories and services; `One.Infrastructure/` contains Dapper, EF Core, and integrations; `One.API/` hosts HTTP endpoints. Routable Razor pages, layouts, client services, and translations belong in `One.Shared/`. `One.MAUI/` and `One.Web/` are thin platform hosts. `OneSourceGen/` contains the Roslyn generator.

## Build, Test, and Development Commands

Run from the repository root:

```powershell
dotnet restore One.sln
dotnet build One.sln -c Release --warnaserror
dotnet test One.sln -c Release --collect:"XPlat Code Coverage" --warnaserror
dotnet run --project One.API/One.API.csproj --launch-profile https
dotnet run --project One.Web/One.Web.csproj --launch-profile https
dotnet build One.MAUI/One.MAUI.ERP.csproj -f net10.0-windows10.0.19041.0
dotnet format One.sln --verify-no-changes
powershell -ExecutionPolicy Bypass -File build/Test-RepositorySecrets.ps1
powershell -ExecutionPolicy Bypass -File Database/Tests/RunIsolatedDatabaseVerification.ps1 -SqlPassword <local-test-password>
powershell -ExecutionPolicy Bypass -File Database/Tests/RunRepositoryHardeningIntegration.ps1
powershell -ExecutionPolicy Bypass -File Database/Deploy/ApplyUsersMigrations.ps1
```

In Visual Studio, select the `Web.API` solution launch profile to start the API and WebAssembly host together.

## New Master Screen Workflow

Repository code is the source of truth. Inspect the current Branches backend and Brands frontend first; if their pattern changes, follow it and update this guide.

1. Confirm fields, relationships, uniqueness, authorization, and delete/deactivate behavior.
2. Add a versioned table script under `Database/Scripts/`. Every business table requires `CompanyId`, tenant-scoped unique indexes, composite tenant foreign keys, and registration in the row-level security policy. Business tables use Dapper through `MainConnection`; use EF migrations only for Identity/company schema changes.
3. Add `One.Domain/Entities/<Area>/<Entity>.cs`, inheriting `BaseEntity<Guid>`. Simple master data must have `[ReferenceData]` and `IsActive`; transactional entities must not. Never accept `CompanyId` from DTOs. Apply field attributes deliberately and keep `IsActive` out of `[UpdateField]`.
4. Add separate create, update, active, and filter DTO source files under `One.Domain/DTOs/<Area>/`; they compile into `One.Contracts` during the compatibility transition. Update, active, and delete requests must carry `RowVersion`. Filters inherit `BaseFilter` and define `[MatchOn]`/`[MatchDictionary]`.
5. Add `One.Application/Repos/<Area>/<Entity>Repo.cs`, inheriting `BaseRepository<TEntity, Guid>`, accepting `ITenantContext`, and overriding `AllowedQueryColumns` with the entity's explicit sort/filter allowlist. Generic repositories are discovered and contract-checked automatically. Custom SQL must retain tenant predicates.
6. Add a controller under `One.API/Controllers/<Area>/`. Inherit `BaseApiController`, use `IDynamicMappingService`, and put `[RequirePermission(pageKey, actionKey)]` on every endpoint.
7. Add matching keys to both translation files, then build `One.Shared/Components/<Area>/<Entity>Master.razor`. Add `[PagePermission(pageKey)]` to the route and wrap create, update, delete, activate, export, and approve controls in `PermissionView`. Follow Brands for localized CRUD, cancellation, debounced search, paging, confirmation, responsive RTL layout, and shared HTTP results.
8. Add the localized navigation route and update the permission catalog seed with one stable page key and its allowed actions. Verify SQL, API status codes, validation, CRUD, search, translations, desktop, Android, and WebAssembly behavior.

## Permission Catalog

The global catalog lives in the Users database as `AppModule`, `AppPage`, `PermissionAction`, and `AppPageAction`. Stable keys come from `PermissionCatalogKeys`; never use translated text as an identifier. `PermissionCatalogSeeder` is the repeatable source of truth and must include only real routable pages. Every `NameResourceKey` must exist in both translation files, and every catalog route must match a Razor `@page` route.

## Permission Enforcement

`PermissionService` calculates effective access. Super administrators have an explicit bypass; company administrators inherit the active company entitlements; regular users receive only their explicit grants intersected with those entitlements. Never protect a business endpoint with roles alone. Use `RequirePermissionAttribute` on the server, `PagePermissionAttribute` for routes, and `PermissionView` for operation controls. `NavMenu.razor` is generated from effective navigation; do not add static business links. Permission, user status, company status, entitlement, and subscription changes must bump `CompanyPermissionVersion` through the application services so existing tokens observe changes. Keep `IsActive` out of `[UpdateField]`; status changes use the dedicated Activate permission and endpoint.

## Plans & Subscriptions

Plans and company subscriptions live in the Users database. Change them only through `ISubscriptionPlanService` and `ICompanySubscriptionService` so validation, transactions, concurrency checks, snapshots, and audit records remain consistent. Plan permissions reference stable catalog keys; system-administration pages are never assignable. Creating a subscription copies plan permissions into `CompanyEntitlement`, and later plan edits must not alter existing companies implicitly. Provision a company only through `ICompanyProvisioningService`; company, subscription, entitlements, and first administrator must commit or roll back together. Reuse `PermissionSelector.razor` in plan and company screens instead of duplicating permission-selection UI. Do not expose these operations through a generic repository.

## Repository & Transaction Boundaries

Use `BaseRepository` only for `[ReferenceData]` entities. It enforces tenant scope, `RowVersion` conflicts, stable bounded queries, audited status changes, and logical deletion. Do not use dynamic column updates, hard deletion, or generic bulk methods. Reload after update/status/delete to obtain the new `RowVersion`; a stale version returns HTTP 409. Optional strings use `[UpdateField(skipIfNull: false)]` when clients must clear them.

Financial aggregates use dedicated application services. `SalesInvoiceApplicationService` validates tenant-owned references, calculates totals on the server, and writes invoice header and lines inside one explicit transaction. Extend this pattern for inventory movements, payments, and accounting entries; never save their rows independently through `BaseRepository`.

## Restaurant Administration & Diagnostics

Restaurants are tenant companies: continue using `CompanyId` for isolation and do not create a parallel restaurant-owned model. Super Admin restaurant pages live under `One.Shared/Components/SysAdmin/`. Creation must use `ICompanyProvisioningService` so the restaurant, first administrator, subscription, and selected catalog permissions remain atomic. Add future assignable modules and pages through `PermissionCatalogSeeder`; they will then appear in the restaurant permission editor.

Server calls to `ILogger` at `Error` or `Critical` are persisted by the centralized provider. Handled UI exceptions must use `MessagesCenter.ExceptionHandle` or `IClientLogger`; `AppErrorBoundary` captures unhandled component failures. Never swallow unexpected exceptions or expose stack traces to end users. Diagnostic records retain the reference, route/page, function, source filename, line when symbols are available, tenant, user, local and UTC timestamps, and correlation ID. Only Super Admin may query `api/Logs/admin` or view `/sysadmin/error-logs`.

## Shared UI & API Client

`One.Shared` must reference `One.Contracts`, never `One.Domain`, Identity, or EF Core. Put every routable component and all user-facing behavior in `One.Shared`; platform hosts provide only storage, API address, logging, and native handlers. Register shared services with `AddOneShared()` and the single transport with `AddOneApiClient(...)`.

Components inject `IOneApiClient` and consume `Result<T>`; do not inject `HttpClient`, create named clients, or parse response JSON in Razor files. Automatic retries are restricted to GET, HEAD, and OPTIONS. POST, PUT, DELETE, and other writes execute once unless an operation has an explicit idempotency design. Use `IClientSession` as the only token and authentication-state source; never create page-specific token keys or store passwords. Large grids use server paging, dropdowns use bounded queries, and debounced searches must cancel the previous request.

## Authentication & Operations

Password login uses Identity lockout and returns one generic failure message. Every access token belongs to a stored `UserSession`; refresh tokens are hashed, rotated once, and revoked on replay, logout, user deactivation, or company deactivation. Never issue a token before checking both user and company status. OTP codes use cryptographic generation, HMAC hashing, a resend cooldown, a short lifetime, bounded attempts, and atomic consumption. Never log codes, tokens, passwords, request bodies, or email bodies.

Company-user login must send the selected `CompanyCode`; the server verifies it against the user's active company before checking the password. Global accounts must use an empty company scope. Authentication pages use `AuthLayout.razor` from `One.Shared` so WebAssembly and MAUI share the same responsive, RTL-safe design. Keep onboarding, employee login, SuperAdmin password/OTP login, forgot-password, and reset-password routes public in `Routes.razor`.

Password recovery always returns a generic request response, uses an Identity token with a bounded lifetime, scopes company users by company code, and revokes every old session after success. Configure the public callback with `Authentication__PasswordReset__ClientBaseUrl`; production must use HTTPS. Never hard-code or seed an administrator plaintext password. Local administrator credentials belong in User Secrets or are set through an explicit protected bootstrap process.

The API must fail startup when required connection, JWT, OTP, CORS, or production mail configuration is missing. Trust forwarded headers only from `ReverseProxy:KnownProxies`. Keep rate limiting on the trusted remote address. `/health/live` checks the process and `/health/ready` checks both databases. Startup must never apply EF migrations; set `ONEERP_USERS_CONNECTION` for the deployment process and run `Database/Deploy/ApplyUsersMigrations.ps1` before starting the API. Client database logs and server files use configured retention limits; keep batches and field lengths bounded.

## Localization

Translations live in `One.Shared/Resources/ar.yml` and `en-US.yml`. Keep identical `PascalCase` keys and placeholders. Razor uses generated `IKeysAccessor` members or `ITranslator`; never hard-code user-facing text. `LocalizeScope.razor` refreshes language changes, and `MainLayout.razor` controls culture and RTL state.

## Local Secrets & Configuration

Keep API development secrets outside the repository with .NET User Secrets owned by `One.API`; the Windows store is `%APPDATA%\Microsoft\UserSecrets\OneERP.Api\secrets.json`. Use `One.API/appsettings.Secrets.example.json` only as a key-name template. Tracked appsettings files contain safe defaults and empty secret values. `One.Web`, `One.MAUI`, and `One.Shared` must never contain database, SMTP, signing, or OTP hashing secrets. Deployment uses environment variables such as `ConnectionStrings__MainConnection`, `JWT__Secret`, and `Security__OtpPepper`. JWT and OTP secrets must each contain at least 32 bytes. Never print secret values in logs or examples.

## Improvement Plan

`SYSTEM_IMPROVEMENT_PLAN.md` is the completed remediation record. Keep its decisions in force and revise this guide whenever repository, service, localization, authorization, or page-creation conventions change.

## Quality Gates

Tests are split by responsibility: `One.Application.Tests/` covers business rules, tenant isolation, repository limits, and authentication behavior; `One.API.Tests/` covers authorization, rate limiting, error handling, and health endpoints through `WebApplicationFactory`; `One.Shared.Tests/` covers session state, permission rendering, and translation parity with bUnit. New behavior must include a focused regression test named `Method_Scenario_ExpectedResult`.

Every pull request runs `.github/workflows/ci.yml`: formatting, secret scanning, dependency audit, warnings-as-errors builds, coverage-enabled tests, API/WebAssembly publish, isolated SQL tenant/concurrency verification, and MAUI Windows/Android builds. Do not merge with a failed gate. Database verification must use a disposable `OneERP_Test_*` database, never a development database.

## Style, Tests, and Contributions

Follow `.editorconfig`: four spaces, CRLF, nullable references, braces, and `using` directives outside namespaces. Use `PascalCase` for public symbols, `I` prefixes for interfaces, and `camelCase` for locals. Add xUnit projects as `One.<Layer>.Tests` and name tests `Method_Scenario_ExpectedResult`. Use focused imperative commits such as `feat: add cities master`. Pull requests need an impact summary, linked issue, verification notes, UI screenshots, and migration warnings. Never commit credentials or tokens.
