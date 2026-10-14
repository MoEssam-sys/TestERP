param(
    [string]$ApiBaseUrl = "http://localhost:5253"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$adminUserId = "596222e7-d712-4cf8-af96-3d3916b70e7f"
$companyId = 1
$testTag = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$branchId = [Guid]::NewGuid()
$warehouseId = [Guid]::NewGuid()
$customerId = [Guid]::NewGuid()
$categoryId = [Guid]::NewGuid()
$productOneId = [Guid]::NewGuid()
$productTwoId = [Guid]::NewGuid()
$createdInvoiceId = [Guid]::Empty
$planId = 0
$subscriptionId = 0L
$originalActive = $false
$allSecrets = dotnet user-secrets list --project One.API/One.API.csproj

function Get-SecretValue([string]$key) {
    $secretLine = $allSecrets |
        Where-Object { $_ -like "$key = *" } |
        Select-Object -First 1
    if (-not $secretLine) {
        throw "Missing required local secret key: $key"
    }

    return $secretLine.Substring(($key + " = ").Length)
}

function ConvertTo-Base64Url([byte[]]$bytes) {
    return [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

function New-TestToken {
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $header = @{ alg = "HS256"; typ = "JWT" } | ConvertTo-Json -Compress
    $payload = @{
        sub = $adminUserId
        companyId = "$companyId"
        role = "CompanyAdmin"
        jti = [Guid]::NewGuid().ToString()
        iss = "https://localhost:7211"
        aud = "http://localhost:4200"
        nbf = $now - 5
        exp = $now + 900
    } | ConvertTo-Json -Compress
    $unsigned = (ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($header))) + "." +
        (ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($payload)))
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($jwtSecret))
    try {
        $signature = ConvertTo-Base64Url ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($unsigned)))
    }
    finally {
        $hmac.Dispose()
    }

    return "$unsigned.$signature"
}

function Invoke-TestApi([string]$method, [string]$path, $body = $null) {
    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($method),
        "$ApiBaseUrl$path")
    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
        "Bearer",
        $script:testToken)
    if ($null -ne $body) {
        $request.Content = [System.Net.Http.StringContent]::new(
            ($body | ConvertTo-Json -Depth 12 -Compress),
            [Text.Encoding]::UTF8,
            "application/json")
    }

    try {
        $response = $script:httpClient.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $json = if ([string]::IsNullOrWhiteSpace($content)) { $null } else { $content | ConvertFrom-Json }
        return [pscustomobject]@{
            Status = [int]$response.StatusCode
            Json = $json
            Raw = $content
        }
    }
    finally {
        $request.Dispose()
    }
}

function Assert-Status($response, [int]$expected, [string]$name) {
    if ($response.Status -ne $expected) {
        throw "$name returned $($response.Status), expected $expected. $($response.Raw)"
    }
}

$mainDbConnection = Get-SecretValue "ConnectionStrings:MainConnection"
$userDbConnection = Get-SecretValue "ConnectionStrings:UserConnection"
$jwtSecret = Get-SecretValue "JWT:Secret"
$httpClient = [System.Net.Http.HttpClient]::new()

try {
    $originalActiveQuery = "SELECT IsActive FROM AspNetUsers WHERE Id='$adminUserId';"
    $originalActive = [bool](Invoke-Sqlcmd -ConnectionString $userDbConnection -Query $originalActiveQuery -ErrorAction Stop).IsActive
    $plan = Invoke-Sqlcmd -ConnectionString $userDbConnection -Query @"
UPDATE AspNetUsers SET IsActive=1, CompanyId=$companyId WHERE Id='$adminUserId';
INSERT INTO SubscriptionPlans
    (Code,Name,Description,Price,Currency,DurationInDays,IsActive,CreatedAtUtc,CreatedByUserId)
OUTPUT INSERTED.Id AS PlanId
VALUES
    (N'REPO-$testTag',N'Repository hardening test',NULL,0,N'EGP',1,1,SYSUTCDATETIME(),'$adminUserId');
"@ -ErrorAction Stop
    $planId = [int]$plan.PlanId
    $subscription = Invoke-Sqlcmd -ConnectionString $userDbConnection -Query @"
INSERT INTO CompanySubscriptions
    (CompanyId,SubscriptionPlanId,StartsAtUtc,EndsAtUtc,Status,PriceSnapshot,
     CurrencySnapshot,DurationInDaysSnapshot,CreatedAtUtc,CreatedByUserId)
OUTPUT INSERTED.Id AS SubscriptionId
VALUES
    ($companyId,$planId,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),
     1,0,N'EGP',1,SYSUTCDATETIME(),'$adminUserId');
"@ -ErrorAction Stop
    $subscriptionId = [long]$subscription.SubscriptionId
    Invoke-Sqlcmd -ConnectionString $userDbConnection -Query @"
INSERT INTO CompanyEntitlements
    (CompanySubscriptionId,AppPageId,PermissionActionId,CompanyId,IsCustomized,CreatedAtUtc)
SELECT $subscriptionId, pa.AppPageId, pa.PermissionActionId, $companyId, 1, SYSUTCDATETIME()
FROM AppPageActions pa
JOIN AppPages p ON p.Id=pa.AppPageId
WHERE p.[Key] IN (N'Sales.Brands',N'Sales.Invoices');
IF EXISTS (SELECT 1 FROM CompanyPermissionVersions WHERE CompanyId=$companyId)
    UPDATE CompanyPermissionVersions
    SET Version=Version+1, UpdatedAtUtc=SYSUTCDATETIME()
    WHERE CompanyId=$companyId;
ELSE
    INSERT INTO CompanyPermissionVersions(CompanyId,Version,UpdatedAtUtc)
    VALUES($companyId,1,SYSUTCDATETIME());
"@ -ErrorAction Stop | Out-Null
    Invoke-Sqlcmd -ConnectionString $mainDbConnection -Query @"
EXEC sys.sp_set_session_context @key=N'CompanyId', @value=$companyId;
INSERT INTO Branches(CompanyId,Id,Code,Name,CreatedAt,CreatedBy,IsActive)
VALUES($companyId,'$branchId',N'RB-$testTag',N'Repository test branch',SYSUTCDATETIME(),'$adminUserId',1);
INSERT INTO Warehouses(CompanyId,Id,Code,Name,BranchId,CreatedAt,CreatedBy,IsActive)
VALUES($companyId,'$warehouseId',N'RW-$testTag',N'Repository test warehouse','$branchId',SYSUTCDATETIME(),'$adminUserId',1);
INSERT INTO Customers(CompanyId,Id,Code,Name,CreatedAt,CreatedBy,IsActive)
VALUES($companyId,'$customerId',N'RC-$testTag',N'Repository test customer',SYSUTCDATETIME(),'$adminUserId',1);
INSERT INTO Categories(CompanyId,Id,Code,Name,CreatedAt,CreatedBy,IsActive)
VALUES($companyId,'$categoryId',N'RG-$testTag',N'Repository test category',SYSUTCDATETIME(),'$adminUserId',1);
INSERT INTO Products(CompanyId,Id,Code,Name,CategoryId,CreatedAt,CreatedBy,IsActive)
VALUES
    ($companyId,'$productOneId',N'RP1-$testTag',N'Repository product one','$categoryId',SYSUTCDATETIME(),'$adminUserId',1),
    ($companyId,'$productTwoId',N'RP2-$testTag',N'Repository product two','$categoryId',SYSUTCDATETIME(),'$adminUserId',1);
"@ -ErrorAction Stop | Out-Null

    $testToken = New-TestToken
    $create = Invoke-TestApi "POST" "/api/sales/brands/create" @{
        code = "TB$testTag"
        name = "Concurrency original"
        isActive = $true
    }
    Assert-Status $create 201 "Create brand"
    $createdBrandId = [Guid]$create.Json.data.id
    $originalRowVersion = [string]$create.Json.data.rowVersion
    if ([string]::IsNullOrWhiteSpace($originalRowVersion)) {
        throw "Create brand did not return RowVersion."
    }

    $update = Invoke-TestApi "PUT" "/api/sales/brands/update" @{
        id = $createdBrandId
        name = "Concurrency updated"
        isActive = $true
        rowVersion = $originalRowVersion
    }
    Assert-Status $update 200 "Update brand"
    $staleUpdate = Invoke-TestApi "PUT" "/api/sales/brands/update" @{
        id = $createdBrandId
        name = "Stale overwrite"
        isActive = $true
        rowVersion = $originalRowVersion
    }
    Assert-Status $staleUpdate 409 "Stale brand update"
    $refresh = Invoke-TestApi "GET" "/api/sales/brands/$createdBrandId"
    Assert-Status $refresh 200 "Reload brand"
    $delete = Invoke-TestApi "DELETE" "/api/sales/brands/delete" @{
        id = $createdBrandId
        rowVersion = [string]$refresh.Json.data.rowVersion
    }
    Assert-Status $delete 200 "Soft delete brand"
    $deleted = Invoke-TestApi "GET" "/api/sales/brands/$createdBrandId"
    Assert-Status $deleted 200 "Read soft-deleted brand"
    if ($deleted.Json.data.isActive -ne $false) {
        throw "Deleted brand was physically removed or remained active."
    }
    $oversizedPage = Invoke-TestApi "POST" "/api/sales/brands/filter" @{
        pageNumber = 1
        pageSize = 101
    }
    Assert-Status $oversizedPage 400 "Oversized page"

    $invoiceNumber = "INV-$testTag"
    $invoiceBody = @{
        number = $invoiceNumber
        branchId = $branchId
        warehouseId = $warehouseId
        customerId = $customerId
        date = [DateTime]::UtcNow.ToString("O")
        lines = @(
            @{ productId=$productOneId; quantity=2; unitPrice=10.25; discount=1.00; tax=2.00 },
            @{ productId=$productTwoId; quantity=1; unitPrice=5.50; discount=0.00; tax=0.50 }
        )
    }
    $invoice = Invoke-TestApi "POST" "/api/sales/invoices" $invoiceBody
    Assert-Status $invoice 201 "Create financial invoice"
    $createdInvoiceId = [Guid]$invoice.Json.data.id
    if ([decimal]$invoice.Json.data.netAmount -ne 27.50) {
        throw "Server invoice total was $($invoice.Json.data.netAmount), expected 27.50."
    }
    $duplicate = Invoke-TestApi "POST" "/api/sales/invoices" $invoiceBody
    Assert-Status $duplicate 409 "Duplicate invoice"
    $dbCheck = Invoke-Sqlcmd -ConnectionString $mainDbConnection -Query @"
EXEC sys.sp_set_session_context @key=N'CompanyId', @value=$companyId;
SELECT
    (SELECT COUNT(*) FROM SalesInvoices WHERE CompanyId=$companyId AND Id='$createdInvoiceId') HeaderCount,
    (SELECT COUNT(*) FROM SalesInvoiceDetails WHERE CompanyId=$companyId AND InvoiceId='$createdInvoiceId') LineCount,
    (SELECT NetAmount FROM SalesInvoices WHERE CompanyId=$companyId AND Id='$createdInvoiceId') NetAmount;
"@ -ErrorAction Stop
    if ($dbCheck.HeaderCount -ne 1 -or $dbCheck.LineCount -ne 2 -or [decimal]$dbCheck.NetAmount -ne 27.50) {
        throw "Financial transaction verification failed."
    }

    [pscustomobject]@{
        Create = 201
        CurrentUpdate = 200
        StaleUpdate = 409
        SoftDelete = 200
        OversizedPage = 400
        InvoiceCreate = 201
        DuplicateInvoice = 409
        InvoiceHeaders = $dbCheck.HeaderCount
        InvoiceLines = $dbCheck.LineCount
        NetAmount = $dbCheck.NetAmount
    }
}
finally {
    try {
        Invoke-Sqlcmd -ConnectionString $mainDbConnection -Query @"
EXEC sys.sp_set_session_context @key=N'CompanyId', @value=$companyId;
DELETE FROM SalesInvoiceDetails WHERE CompanyId=$companyId AND InvoiceId='$createdInvoiceId';
DELETE FROM SalesInvoices WHERE CompanyId=$companyId AND (Id='$createdInvoiceId' OR Number=N'INV-$testTag');
DELETE FROM Brands WHERE CompanyId=$companyId AND Code=N'TB$testTag';
DELETE FROM Products WHERE CompanyId=$companyId AND Id IN ('$productOneId','$productTwoId');
DELETE FROM Categories WHERE CompanyId=$companyId AND Id='$categoryId';
DELETE FROM Warehouses WHERE CompanyId=$companyId AND Id='$warehouseId';
DELETE FROM Customers WHERE CompanyId=$companyId AND Id='$customerId';
DELETE FROM Branches WHERE CompanyId=$companyId AND Id='$branchId';
"@ -ErrorAction SilentlyContinue | Out-Null
    }
    catch {}

    try {
        if ($subscriptionId -gt 0) {
            $subscriptionCleanup = "DELETE FROM CompanyEntitlements WHERE CompanySubscriptionId=$subscriptionId; DELETE FROM CompanySubscriptions WHERE Id=$subscriptionId;"
            Invoke-Sqlcmd -ConnectionString $userDbConnection -Query $subscriptionCleanup -ErrorAction SilentlyContinue | Out-Null
        }
        if ($planId -gt 0) {
            $planCleanup = "DELETE FROM SubscriptionPlans WHERE Id=$planId;"
            Invoke-Sqlcmd -ConnectionString $userDbConnection -Query $planCleanup -ErrorAction SilentlyContinue | Out-Null
        }
        $activeValue = if ($originalActive) { 1 } else { 0 }
        $userCleanup = "UPDATE AspNetUsers SET IsActive=$activeValue WHERE Id='$adminUserId'; UPDATE CompanyPermissionVersions SET Version=Version+1, UpdatedAtUtc=SYSUTCDATETIME() WHERE CompanyId=$companyId;"
        Invoke-Sqlcmd -ConnectionString $userDbConnection -Query $userCleanup -ErrorAction SilentlyContinue | Out-Null
    }
    catch {}

    $httpClient.Dispose()
}
