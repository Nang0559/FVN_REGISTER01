[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateLength(1, 64)]
    [string]$TenantCode,

    [Parameter(Mandatory = $true)]
    [ValidateLength(1, 128)]
    [string]$ActorId,

    [string]$ApiBaseUrl = "http://localhost:53716",

    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "FVN_REGISTER.AI.Api\FVN_REGISTER.AI.Api.csproj"
if (-not (Test-Path -LiteralPath $apiProject)) {
    throw "AI API project was not found: $apiProject"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK (dotnet) was not found."
}

# Read secrets into memory only. Never print this command's captured output.
$secretLines = @(& dotnet user-secrets list --project $apiProject 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Could not read AI API User Secrets. Configure them with scripts/Setup-AI-DevelopmentSecrets.ps1 first."
}
$apiKey = $null
$signingKey = $null
foreach ($line in $secretLines) {
    $value = [string]$line
    if ($value -match '^AIPlatform:ApiKey\s*=\s*(.*)$') { $apiKey = $Matches[1] }
    if ($value -match '^AIPlatform:ContextSigningKey\s*=\s*(.*)$') { $signingKey = $Matches[1] }
}
$secretLines = $null
if ([string]::IsNullOrWhiteSpace($apiKey) -or [string]::IsNullOrWhiteSpace($signingKey)) {
    throw "AIPlatform:ApiKey or AIPlatform:ContextSigningKey is missing from AI API User Secrets."
}
if ($signingKey.Length -lt 32) {
    throw "AIPlatform:ContextSigningKey must contain at least 32 characters."
}

$baseUri = $null
if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$baseUri) -or $baseUri.Scheme -notin @("http", "https")) {
    throw "ApiBaseUrl must be an absolute HTTP or HTTPS URL."
}
$ApiBaseUrl = $ApiBaseUrl.TrimEnd("/")
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds([Math]::Max(30, $TimeoutSeconds))

function Get-ResponseText {
    param([System.Net.Http.HttpResponseMessage]$Response)
    return $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
}

function New-SignedRequest {
    param(
        [string]$Method,
        [string]$Path,
        [byte[]]$BodyBytes,
        [string]$ContentType
    )

    if ($null -eq $BodyBytes) { $BodyBytes = [byte[]]@() }
    $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString([Globalization.CultureInfo]::InvariantCulture)
    $nonce = [Guid]::NewGuid().ToString("N")
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bodyHash = [BitConverter]::ToString($sha.ComputeHash($BodyBytes)).Replace("-", "")
    }
    finally {
        $sha.Dispose()
    }

    $canonical = [string]::Join([char]10, @(
        $Method.ToUpperInvariant()
        $Path
        $TenantCode.Trim()
        $ActorId.Trim()
        $timestamp
        $nonce
        $bodyHash
    ))

    $keyBytes = [Text.Encoding]::UTF8.GetBytes($signingKey)
    $hmac = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
    try {
        $signature = [BitConverter]::ToString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace("-", "")
    }
    finally {
        $hmac.Dispose()
        [Array]::Clear($keyBytes, 0, $keyBytes.Length)
    }

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), "$ApiBaseUrl$Path")
    $request.Headers.Add("X-AI-API-Key", $apiKey)
    $request.Headers.Add("X-Tenant-Code", $TenantCode.Trim())
    $request.Headers.Add("X-Actor-Id", $ActorId.Trim())
    $request.Headers.Add("X-AI-Timestamp", $timestamp)
    $request.Headers.Add("X-AI-Nonce", $nonce)
    $request.Headers.Add("X-AI-Signature", $signature)

    if ($Method -ne "GET") {
        $request.Content = [System.Net.Http.ByteArrayContent]::new($BodyBytes)
        if (-not [string]::IsNullOrWhiteSpace($ContentType)) {
            $null = $request.Content.Headers.TryAddWithoutValidation("Content-Type", $ContentType)
        }
    }
    return $request
}

try {
    Write-Host "1/4 - Checking AI API readiness..." -ForegroundColor Cyan
    $readyResponse = $client.GetAsync("$ApiBaseUrl/health/ready").GetAwaiter().GetResult()
    $readyText = Get-ResponseText $readyResponse
    if (-not $readyResponse.IsSuccessStatusCode) {
        Write-Host "Readiness response (HTTP $([int]$readyResponse.StatusCode)):" -ForegroundColor Yellow
        Write-Host $readyText
        throw "AI Platform is not ready. Resolve database, Ollama model, embedding model and malware-scanner readiness before uploading a test document."
    }
    $ready = $readyText | ConvertFrom-Json
    Write-Host "Database=$($ready.database); provider=$($ready.provider); chatModel=$($ready.chatModelAvailable); embeddingModel=$($ready.embeddingModelAvailable); malwareScanner=$($ready.malwareScannerConfigured)" -ForegroundColor Green
    $readyResponse.Dispose()

    Write-Host "2/4 - Creating and signing a UTF-8 text document..." -ForegroundColor Cyan
    $nl = [Environment]::NewLine
    $sample = "FVN_REGISTER AI end-to-end test" + $nl +
        "Document type: leave request" + $nl +
        "Employee code: E2E-TEST" + $nl +
        "Request dates: 20/10/2026 to 21/10/2026" + $nl +
        "This is synthetic test data; do not write it to HR records."
    $fileBytes = [Text.Encoding]::UTF8.GetBytes($sample)
    $boundary = "----------------FVNRegisterAI" + [Guid]::NewGuid().ToString("N")
    $multipart = [System.Net.Http.MultipartFormDataContent]::new($boundary)
    $fileContent = [System.Net.Http.ByteArrayContent]::new($fileBytes)
    $null = $fileContent.Headers.TryAddWithoutValidation("Content-Type", "text/plain; charset=utf-8")
    $multipart.Add($fileContent, "file", "fvn-register-ai-e2e.txt")
    $bodyStream = [System.IO.MemoryStream]::new()
    try {
        $multipart.CopyToAsync($bodyStream).GetAwaiter().GetResult()
        $uploadBytes = $bodyStream.ToArray()
        $multipartContentType = $multipart.Headers.ContentType.ToString()
    }
    finally {
        $bodyStream.Dispose()
        $multipart.Dispose()
    }

    $uploadPath = "/api/ai/v1/documents"
    $uploadRequest = New-SignedRequest -Method "POST" -Path $uploadPath -BodyBytes $uploadBytes -ContentType $multipartContentType
    try {
        $uploadResponse = $client.SendAsync($uploadRequest).GetAwaiter().GetResult()
        $uploadText = Get-ResponseText $uploadResponse
        if (-not $uploadResponse.IsSuccessStatusCode) {
            throw "Document upload failed (HTTP $([int]$uploadResponse.StatusCode)): $uploadText"
        }
        $receipt = $uploadText | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace([string]$receipt.documentId)) {
            throw "Upload response did not contain documentId: $uploadText"
        }
        $documentId = [string]$receipt.documentId
        Write-Host "Accepted documentId=$documentId; jobId=$($receipt.jobId); duplicate=$($receipt.duplicate)" -ForegroundColor Green
        $uploadResponse.Dispose()
    }
    finally {
        $uploadRequest.Dispose()
        [Array]::Clear($uploadBytes, 0, $uploadBytes.Length)
        [Array]::Clear($fileBytes, 0, $fileBytes.Length)
    }

    Write-Host "3/4 - Polling the SQL-backed ingestion job..." -ForegroundColor Cyan
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds([Math]::Max(60, $TimeoutSeconds))
    $status = $null
    do {
        Start-Sleep -Seconds 3
        $statusPath = "/api/ai/v1/documents/$documentId"
        $statusRequest = New-SignedRequest -Method "GET" -Path $statusPath -BodyBytes ([byte[]]@()) -ContentType ""
        try {
            $statusResponse = $client.SendAsync($statusRequest).GetAwaiter().GetResult()
            $statusText = Get-ResponseText $statusResponse
            if (-not $statusResponse.IsSuccessStatusCode) {
                throw "Document status request failed (HTTP $([int]$statusResponse.StatusCode)): $statusText"
            }
            $status = $statusText | ConvertFrom-Json
            $statusResponse.Dispose()
        }
        finally {
            $statusRequest.Dispose()
        }

        Write-Host "Document=$($status.status); Job=$($status.jobStatus); Failure=$($status.failureCode)"
        if ($status.jobStatus -eq "Completed") { break }
        if ($status.jobStatus -eq "DeadLetter") {
            throw "Ingestion job reached DeadLetter. FailureCode=$($status.failureCode)"
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    if ($null -eq $status -or $status.jobStatus -ne "Completed") {
        throw "Timed out waiting for ingestion completion. Last job status: $($status.jobStatus)"
    }

    Write-Host "4/4 - End-to-end ingestion completed." -ForegroundColor Cyan
    Write-Host "DocumentId: $documentId"
    Write-Host "Document status: $($status.status)"
    Write-Host "Job status: $($status.jobStatus)"
    Write-Host "Next: inspect the extraction endpoint and review candidate; this test does not approve or write business records." -ForegroundColor Green
}
finally {
    $client.Dispose()
    $apiKey = $null
    $signingKey = $null
}
