param(
    [Parameter(Mandatory = $true)]
    [string]$SqlPassword,
    [string]$SqlServer = "localhost,1433",
    [string]$SqlUser = "sa",
    [string]$SqlCmdPath = "sqlcmd",
    [string]$DatabaseName = ("OneERP_Test_" + [Guid]::NewGuid().ToString("N"))
)

$ErrorActionPreference = "Stop"

if ($DatabaseName -notmatch '^OneERP_Test_[A-Za-z0-9_]+$') {
    throw "The isolated database name is invalid."
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$initialization = Join-Path $PSScriptRoot "InitializeIsolatedMainDatabase.sql"
$scripts = @(
    $initialization,
    (Join-Path $root "Database/Scripts/20260809_001_AddTenantIsolation.sql"),
    (Join-Path $root "Database/Scripts/20260809_002_RemoveLegacyOrphanBrand.sql"),
    (Join-Path $root "Database/Scripts/20260809_003_MakeTenantColumnsRequired.sql"),
    (Join-Path $root "Database/Scripts/20260809_004_HardenReferenceDataAndInvoices.sql"),
    (Join-Path $PSScriptRoot "VerifyTenantIsolation.sql"),
    (Join-Path $PSScriptRoot "VerifyRepositoryHardening.sql")
)

foreach ($script in $scripts) {
    if (-not (Test-Path -LiteralPath $script -PathType Leaf)) {
        throw "Required database verification script was not found: $script"
    }
}

$previousPassword = $env:SQLCMDPASSWORD
$env:SQLCMDPASSWORD = $SqlPassword
$databaseCreated = $false
try {
    # Cleanup is safe for this validated, randomly generated name even if creation fails midway.
    $databaseCreated = $true
    & $SqlCmdPath -S $SqlServer -U $SqlUser -C -b -r 1 -d master `
        -v "DatabaseName=$DatabaseName" -i $initialization
    if ($LASTEXITCODE -ne 0) {
        throw "Isolated database initialization failed."
    }
    foreach ($script in $scripts | Select-Object -Skip 1) {
        & $SqlCmdPath -S $SqlServer -U $SqlUser -C -b -r 1 `
            -d $DatabaseName -i $script
        if ($LASTEXITCODE -ne 0) {
            throw "Database verification failed in $([IO.Path]::GetFileName($script))."
        }
    }

    Write-Host "Isolated database verification passed."
}
finally {
    if ($databaseCreated) {
        $dropDatabase = @"
IF DB_ID(N'$DatabaseName') IS NOT NULL
BEGIN
    ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$DatabaseName];
END;
"@
        & $SqlCmdPath -S $SqlServer -U $SqlUser -C -b -r 1 -d master -Q $dropDatabase
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "The isolated database could not be removed: $DatabaseName"
        }
    }

    $env:SQLCMDPASSWORD = $previousPassword
}
