$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Development-only setup for AI API, AI Worker and FVN_REGISTER.API.
# Secrets are stored with .NET User Secrets, not in appsettings.json.

$root = Split-Path -Parent $PSScriptRoot
$projects = [ordered]@{
    AiApi = Join-Path $root "FVN_REGISTER.AI.Api\FVN_REGISTER.AI.Api.csproj"
    Worker = Join-Path $root "FVN_REGISTER.AI.Worker\FVN_REGISTER.AI.Worker.csproj"
    MainApi = Join-Path $root "FVN_REGISTER.API\FVN_REGISTER.API.csproj"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw ".NET SDK (dotnet) not found." }
foreach ($item in $projects.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $item.Value)) { throw "Project not found: $($item.Value)" }
}

function Invoke-DotNet {
    param([string[]]$Arguments, [string]$FailureMessage)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$FailureMessage (exit code $LASTEXITCODE)" }
}
function Ensure-UserSecretsId {
    param([string]$Project)
    $text = Get-Content -LiteralPath $Project -Raw
    if ($text -notmatch '<UserSecretsId>\s*[^<]+\s*</UserSecretsId>') {
        Write-Host "Initializing UserSecretsId for $(Split-Path -Leaf $Project)..."
        Invoke-DotNet -Arguments @("user-secrets", "init", "--project", $Project) -FailureMessage "Could not initialize UserSecretsId."
    }
}
function New-RandomSecret {
    param([int]$ByteCount = 48)
    $bytes = New-Object byte[] $ByteCount
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes); return [Convert]::ToBase64String($bytes) }
    finally { $rng.Dispose(); [Array]::Clear($bytes, 0, $bytes.Length) }
}

Write-Host ""
Write-Host "=== FVN_REGISTER AI Platform - Development secrets ==="
Write-Host "Enter the connection string for database FVN_REGISTER."
$secureConnectionString = Read-Host "SQL Server connection string" -AsSecureString
$ptr = [IntPtr]::Zero
$connectionString = $null
try {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureConnectionString)
    $connectionString = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    if ([string]::IsNullOrWhiteSpace($connectionString)) { throw "Connection string cannot be empty." }

    # Accept pasted connection strings with or without surrounding quotes.
    # Read-Host does not need quotes, but users often paste them from documentation.
    $connectionString = $connectionString.Trim()
    if ($connectionString.Length -ge 2) {
        $first = $connectionString[0]
        $last = $connectionString[$connectionString.Length - 1]
        if (($first -eq '"' -and $last -eq '"') -or ($first -eq "'" -and $last -eq "'")) {
            $connectionString = $connectionString.Substring(1, $connectionString.Length - 2).Trim()
        }
    }

    # Validate keywords against the raw string. DbConnectionStringBuilder.Keys can
    # be provider/runtime-normalized differently across PowerShell/.NET versions.
    $hasServer = $connectionString -match '(?i)(?:^|;)\s*(?:data\s*source|server|address|network\s*address)\s*='
    if (-not $hasServer) {
        throw "Connection string must include Server or Data Source. Check that the pasted text starts with 'Data Source=' or 'Server=' and has no extra characters."
    }

    $databaseMatch = [regex]::Match(
        $connectionString,
        '(?i)(?:^|;)\s*(?:initial\s+catalog|database)\s*=\s*(?:"([^"]*)"|''([^'']*)''|([^;]*))'
    )
    if (-not $databaseMatch.Success) {
        throw "Connection string must specify Database=FVN_REGISTER."
    }
    $dbName = $databaseMatch.Groups[1].Value
    if (-not $dbName) { $dbName = $databaseMatch.Groups[2].Value }
    if (-not $dbName) { $dbName = $databaseMatch.Groups[3].Value.Trim() }
    if ($dbName -and $dbName -ne "FVN_REGISTER") {
        $confirm = Read-Host "Database is '$dbName', not FVN_REGISTER. Continue? (y/N)"
        if ($confirm -notin @("y", "Y", "yes", "YES")) { throw "Cancelled: database is not FVN_REGISTER." }
    }

    $baseUrl = Read-Host "AI API Base URL [https://localhost:53715]"
    if ([string]::IsNullOrWhiteSpace($baseUrl)) { $baseUrl = "https://localhost:53715" }
    $parsed = $null
    if (-not [Uri]::TryCreate($baseUrl, [UriKind]::Absolute, [ref]$parsed) -or $parsed.Scheme -notin @("http", "https")) {
        throw "Base URL must be an absolute HTTP/HTTPS URL."
    }
}
finally {
    if ($ptr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    $secureConnectionString = $null
}

# Original uploaded documents. AI.Api and AI.Worker MUST use the same absolute path.
$defaultStorageRoot = Join-Path $root "OriginalDocuments"
$storageRoot = Read-Host "Original document storage root [$defaultStorageRoot]"
if ([string]::IsNullOrWhiteSpace($storageRoot)) { $storageRoot = $defaultStorageRoot }
$storageRoot = $storageRoot.Trim().Trim('"').Trim("'")
if (-not [System.IO.Path]::IsPathRooted($storageRoot)) { throw "Storage root must be an absolute path: $storageRoot" }
$storageRoot = [System.IO.Path]::GetFullPath($storageRoot)
New-Item -ItemType Directory -Force -Path $storageRoot | Out-Null

$apiKey = New-RandomSecret -ByteCount 48
$contextSigningKey = New-RandomSecret -ByteCount 64
try {
    foreach ($project in $projects.Values) { Ensure-UserSecretsId -Project $project }

    foreach ($project in @($projects.AiApi, $projects.Worker)) {
        Invoke-DotNet -Arguments @("user-secrets", "set", "ConnectionStrings:FvnAi", $connectionString, "--project", $project) -FailureMessage "Could not save AI database connection string."
        Invoke-DotNet -Arguments @("user-secrets", "set", "AIPlatform:ApiKey", $apiKey, "--project", $project) -FailureMessage "Could not save AI API key."
        Invoke-DotNet -Arguments @("user-secrets", "set", "AIPlatform:StorageRoot", $storageRoot, "--project", $project) -FailureMessage "Could not save AI storage root."
        Invoke-DotNet -Arguments @("user-secrets", "set", "AIPlatform:ContextSigningKey", $contextSigningKey, "--project", $project) -FailureMessage "Could not save AI context signing key."
    }

    Invoke-DotNet -Arguments @("user-secrets", "set", "AIPlatform:BaseUrl", $baseUrl.TrimEnd('/'), "--project", $projects.MainApi) -FailureMessage "Could not save main API BaseUrl."
    Invoke-DotNet -Arguments @("user-secrets", "set", "AIPlatform:ApiKey", $apiKey, "--project", $projects.MainApi) -FailureMessage "Could not save main API key."
    Invoke-DotNet -Arguments @("user-secrets", "set", "AIPlatform:ContextSigningKey", $contextSigningKey, "--project", $projects.MainApi) -FailureMessage "Could not save main API signing key."

    Write-Host ""
    Write-Host "SUCCESS: configured User Secrets for AI API, AI Worker and FVN_REGISTER.API."
    Write-Host "The shared API key and context signing key are synchronized. Secret values were not printed."
    Write-Host "Next: build the solution; run AI API and check /health and /health/ready; run AI Worker to process queued jobs."
    Write-Host "Note: this script does not grant SQL permissions or configure an external AI provider/Ollama."
}
finally {
    $connectionString = $null
    $apiKey = $null
    $contextSigningKey = $null
}
