# AI Handoff & Project Status

**ATTENTION AI AGENTS:**
> **Any AI tool working on this project MUST read this file first.**
> **You MUST update this file with a summary of your changes before completing your session.**

## Project Overview
OneERP is a multi-tenant ERP solution using .NET 8 (API) and MAUI (Blazor Hybrid).
- **Architecture**: Clean Architecture (API, Application, Domain, Infrastructure, MAUI Client).
- **Authentication**: JWT, Identity, OTP for SuperAdmin.
- **Database**: SQL Server. `UsersDb` (Shared), `MainDb` (Per-Tenant).

## Latest Work Session (Date: 2026-01-02)
**Agent**: Antigravity (Google DeepMind)
**Focus**: Super Admin Bootstrap, RBAC Implementation, Database Safety.

### 1. Database Creation Logic (CRITICAL)
-   **Change**: Modified `One.API.Configuration.ApiConfig.SeedRolesAsync`.
-   **Why**: The application was attempting to `CREATE DATABASE` on startup via `MigrateAsync`, causing permission errors for users without master access.
-   **Current Logic**: Checks `CanConnectAsync()` first. If the database is missing, it **throws an exception** and stops. It does **NOT** attempt to create the database automatically.
-   **Instruction**: Do NOT revert this. Do NOT use `EnsureCreated` or unconditional `Migrate`.

### 2. Authentication & RBAC
-   **Super Admin**: Implemented OTP-based login (`SuperAdminLogin.razor`).
-   **Roles**: Seeded `SuperAdmin`, `CompanyAdmin`, `User`, `Tester`.
-   **Backend**: `AuthService` now correctly identifies "CompanyAdmin" role (fixed string mismatch).
-   **Register User**: `RegisterUser.razor` now supports:
    -   Target Company ID input (for SuperAdmin).
    -   Role selection (Context-aware).
    -   Link from `CompaniesList`.

### 3. API & Infrastructure
-   **OTP**: Added `OtpChallenge` entity and `AuthenticationController` endpoints (`/Login/Otp`, `/Login/Verify`).
-   **Company Listing**: `CompaniesList.razor` allows SuperAdmin to view companies and create their first Admin.

## Current State & Next Steps
-   **Build Status**: Solution builds successfully.
-   **Verification**: Manual verification passed for logic flow.
-   **Immediate Next Steps**:
    -   Verify the multi-tenant data isolation in `MainDb` (since we just focused on `UsersDb` and auth).
    -   Continue implementing module features (Sales, POS) respecting the RBAC rules.
    -   **Update (Time: 17:50)**: Reset UsersDb to new host `db37048`. Applied migrations successfully. Schema is fresh.
    -   **Update (Time: 18:00)**: Fixed OTP 500 error. Backend now propagates email errors. Frontend waits for API success. Added SMTP config placeholder.
    -   **Update (Time: 18:25)**: Fixed 500 error due to missing `ITranslator` dependency in API. Created and registered `ServerTranslator`. OTP flow is now fully functional.
    -   **Update (Time: 19:15)**: Implemented Super Admin Dashboard + Company Details + Backend Data Logic.
    -   **Update (Time: 19:15)**: Updated NavMenu to be Role-Aware using `AuthorizeView` and strict constants. Fixed redirection loops in MainLayout.
    -   **Update (Time: 19:15)**: Created `CompanySummaryDto` and `CompanyDetailsDto` to support the dashboard view.

### 4. Session Persistence & Auto-Login (Time: 19:40)

#### A. Fixed SuperAdmin Dashboard White Screen
-   **File**: `One.MAUI\Components\SysAdmin\SuperAdminDashboard.razor`
-   **Changes**: Added error handling with visible error UI, retry button, loading state, and debug logging.

#### B. Auto-Login Implementation
-   **DEBUG Mode**: Credentials/tokens persist for **365 days**
-   **RELEASE Mode**: Credentials/tokens persist for **1 day**
-   **Files**: `Login.razor`, `SuperAdminLogin.razor`

#### C. Company Code Flow
-   `Login.razor` - Displays company name/code, "Change Company" link
-   `CompanyOnboarding.razor` - Saves `company_name` in addition to `company_code`
-   `MainLayout.razor` - Logout clears all tokens, removed `forceLoad: true`

#### D. Storage Keys (SecureStorage)
| Key | Purpose |
|-----|---------|
| `auth:creds:v1` | User credentials + expiry |
| `superadmin:token:v1` | SuperAdmin JWT + expiry |
| `company_code` | Selected company code |
| `company_name` | Selected company name |

#### E. New Files
-   `One.MAUI\Services\JwtAuthenticationStateProvider.cs` - JWT claims for Blazor auth

#### F. Other Fixes
-   Added `AddAuthorizationCore()` + `AuthenticationStateProvider` to `MauiProgram.cs`
-   Added Polly retry policy for HTTP resilience
-   Added Polly retry policy for HTTP resilience
-   Removed all `forceLoad: true` from navigations

#### G. CRITICAL FIX: White Screen & Missing CascadingAuthenticationState (Time: 21:45)
-   **Issue**: `SuperAdminDashboard` (and other authorized pages) showed a white screen or crashed with `Authorization requires a cascading parameter of type Task<AuthenticationState>`.
-   **Cause**: The Blazor root component (`Routes.razor`) was missing the `<CascadingAuthenticationState>` wrapper around the `<Router>`. This prevented `[Authorize]` attributes and `AuthorizeView` components from accessing the authentication state.
-   **Fix**: Wrapped the `<Router>` in `Routes.razor` with `<CascadingAuthenticationState>`.
-   **Files Modified**:
    -   `One.MAUI\Components\Routes.razor`

#### H. FIX: API Null Response / Deserialization Issues (Time: 22:00)
-   **Issue**: `RegisterAdmin.razor` (and others) received `null` responses because `dynamic` deserialization failed or case sensitivity mismatched (API returns `success`, code expects `Success`).
-   **Fix**:
    -   Replaced `dynamic` with typed private classes (e.g. `RegistrationResponse`).
    -   Added `PropertyNameCaseInsensitive = true` to `JsonSerializerOptions` in `ApiService.ExecuteSimpleApi` calls.
-   **Files Modified**:
-   **Files Modified**:
    -   `RegisterAdmin.razor`, `RegisterUser.razor`
    -   `CompanyDetails.razor`, `SuperAdminDashboard.razor`, `CompaniesList.razor`

#### I. FIX: Registration Logic & Validation (Time: 22:15)
-   **Issue #1**: "Email already exists" error even for new emails.
    -   **Cause**: `AuthRepository.FindByEmailAsync` was returning `new UserData()` (empty object) instead of `null` when user wasn't found due to incorrect null coalescing (`?? new()`).
    -   **Fix**: Removed `?? new()` to return `null` correctly. Updated `IAuthRepository` signature to return `UserData?`.
-   **Issue #2**: Frontend showed generic "BadRequest 400" without the specific error message.
    -   **Cause**: `ApiService.ExecuteSimpleApi` was ignoring the response body on error.
    -   **Fix**: Added logic to read and parse the response body (JSON) to extract "message" or "title".
-   **Issue #3**: Missing server-side validation.
    -   **Fix**: Added `[Required]`, `[EmailAddress]`, and `[MinLength]` attributes to `RegisterUserDto`.
-   **Files Modified**:
    -   `One.Application\Repos\Identity\AuthRepository.cs`
    -   `One.Infrastructure\Interfaces\IAuthRepository.cs`
    -   `One.Shared\Services\ApiService.cs`
    -   `One.Domain\DTOs\Identity\RegisterUserDto.cs`

