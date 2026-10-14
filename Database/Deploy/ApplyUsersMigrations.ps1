$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:ONEERP_USERS_CONNECTION)) {
    throw 'Set ONEERP_USERS_CONNECTION for this process before applying migrations.'
}

dotnet ef database update `
    --project One.Infrastructure/One.Infrastructure.csproj `
    --startup-project One.API/One.API.csproj `
    --context UsersDbContext

if ($LASTEXITCODE -ne 0) {
    throw "Users database migration failed with exit code $LASTEXITCODE."
}
