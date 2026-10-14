$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$trackedFiles = git -C $repositoryRoot ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate tracked repository files."
}

$failures = [System.Collections.Generic.List[string]]::new()
$sensitiveJsonPaths = @(
    "ConnectionStrings:UserConnection",
    "ConnectionStrings:MainConnection",
    "JWT:Secret",
    "Security:OtpPepper",
    "Email:SmtpPass"
)

function Read-JsonValue($node, [string[]]$segments) {
    $current = $node
    foreach ($segment in $segments) {
        $property = $current.PSObject.Properties[$segment]
        if ($null -eq $property) {
            return $null
        }

        $current = $property.Value
    }

    return $current
}

foreach ($relativePath in $trackedFiles) {
    $fullPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        continue
    }

    $extension = [IO.Path]::GetExtension($relativePath)
    if ($extension -notin @(".cs", ".csproj", ".json", ".md", ".ps1", ".sql", ".yml", ".yaml")) {
        continue
    }

    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($content -match '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----') {
        $failures.Add("Private key material: $relativePath")
    }
    if ($content -match '(?<![A-Za-z0-9])AKIA[0-9A-Z]{16}(?![A-Za-z0-9])') {
        $failures.Add("AWS access key pattern: $relativePath")
    }
    if ($content -match '(?<![A-Za-z0-9])gh[pousr]_[A-Za-z0-9]{36,255}(?![A-Za-z0-9])') {
        $failures.Add("GitHub token pattern: $relativePath")
    }

    if ($extension -ne ".json" -or [IO.Path]::GetFileName($relativePath) -notlike "appsettings*.json") {
        continue
    }
    if ([IO.Path]::GetFileName($relativePath) -eq "appsettings.Secrets.example.json") {
        continue
    }

    try {
        $json = $content | ConvertFrom-Json
    }
    catch {
        $failures.Add("Invalid application settings JSON: $relativePath")
        continue
    }

    foreach ($settingPath in $sensitiveJsonPaths) {
        $value = Read-JsonValue $json ($settingPath -split ':')
        if ($value -is [string] -and
            -not [string]::IsNullOrWhiteSpace($value) -and
            -not $value.StartsWith('${', [StringComparison]::Ordinal)) {
            $failures.Add("Tracked sensitive setting '$settingPath': $relativePath")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | Sort-Object -Unique | ForEach-Object { Write-Error $_ }
    throw "Repository secret scan failed."
}

Write-Host "Repository secret scan passed."
