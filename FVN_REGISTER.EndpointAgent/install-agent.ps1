param(
    [Parameter(Mandatory = $true)] [string]$InstallPath,
    [Parameter(Mandatory = $true)] [string]$ApiBaseUrl,
    [Parameter(Mandatory = $true)] [string]$DeviceKey,
    [Parameter(Mandatory = $false)] [string]$ApiKeyProtected
)

$ErrorActionPreference = 'Stop'

$uri = [Uri]$ApiBaseUrl
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https') {
    throw 'ApiBaseUrl phải là HTTPS.'
}

$exe = Join-Path $InstallPath 'FVN_REGISTER.EndpointAgent.exe'
if (-not (Test-Path $exe)) {
    throw "Không tìm thấy Agent: $exe"
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null

if ([string]::IsNullOrWhiteSpace($ApiKeyProtected)) {
    if (-not [Environment]::UserInteractive) {
        throw 'Non-interactive installation phải cung cấp ApiKeyProtected.'
    }

    $secureApiKey = Read-Host 'Nhập API key của Endpoint (không truyền API key plaintext trên command line)' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
    try {
        $plainApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        $protectedOutput = $plainApiKey | & $exe --protect-secret-stdin
        if ($LASTEXITCODE -ne 0) {
            throw "Agent không tạo được ApiKeyProtected (exit code $LASTEXITCODE)."
        }
        $ApiKeyProtected = ($protectedOutput | Select-Object -Last 1).ToString().Trim()
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
        $plainApiKey = $null
    }

    if ([string]::IsNullOrWhiteSpace($ApiKeyProtected)) {
        throw 'Không tạo được ApiKeyProtected bằng DPAPI LocalMachine.'
    }
}

$configPath = Join-Path $InstallPath 'appsettings.json'
$config = @{
    FVNEndpointAgent = @{
        ApiBaseUrl = $ApiBaseUrl
        DeviceKey = $DeviceKey
        ApiKeyProtected = $ApiKeyProtected
        IntervalMinutes = 30
        CredentialRotationLeadDays = 30
    }
} | ConvertTo-Json -Depth 4

# Write beside the live file and replace it in one filesystem operation.
# The live configuration is never truncated before the replacement is ready.
$tempPath = "$configPath.$([Guid]::NewGuid().ToString('N')).tmp"
try {
    [System.IO.File]::WriteAllText($tempPath, $config, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tempPath -Destination $configPath -Force
}
finally {
    if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
}

$serviceName = 'FVNRegisterEndpointAgent'
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $serviceName binPath= "`"$exe`"" start= auto DisplayName= "FVN Register Endpoint Agent" | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/none/0 | Out-Null
Start-Service -Name $serviceName

Get-Service -Name $serviceName | Select-Object Name, Status, StartType
