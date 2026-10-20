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

    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder.ConnectionString = $connectionString
    if (-not $builder.ContainsKey("Data Source") -and -not $builder.ContainsKey("Server") -and -not $builder.ContainsKey("Address") -and -not $builder.ContainsKey("Network Address")) {
        throw "Connection string must include Server or Data Source."
    }
    if (-not $builder.ContainsKey("Initial Catalog") -and -not $builder.ContainsKey("Database")) {
        throw "Connection string must specify Database=FVN_REGISTER."
    }
    $dbName = if ($builder.ContainsKey("Initial Catalog")) { [string]$builder["Initial Catalog"] } else { [string]$builder["Database"] }
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

$apiKey = New-RandomSecret -ByteCount 48
$contextSigningKey = New-RandomSecret -ByteCount 64
try {
    foreach ($project in $projects.Values) { Ensure-UserSecretsId -Project $project }

    foreach ($project in @($projects.AiApi, $projects.Worker)) {
        Invoke-DotNet -Arguments @("user-secrets", "set", "ConnectionStrings:FvnAi", $connectionString, "--project", $project) -FailureMessage "Could not save AI database connection string."
        Invoke-DotNet -Arguments @("user-secrets", "set", "AIPlatform:ApiKey", $apiKey, "--project", $project) -FailureMessage "Could not save AI API key."
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
