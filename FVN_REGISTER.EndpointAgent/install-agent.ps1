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

if ([string]::IsNullOrWhiteSpace($ApiKeyProtected) -and -not [Environment]::UserInteractive) {
    throw 'Non-interactive installation phải cung cấp ApiKeyProtected.'
}

$exe = Join-Path $InstallPath 'FVN_REGISTER.EndpointAgent.exe'
if (-not (Test-Path $exe)) {
    throw "Không tìm thấy Agent: $exe"
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null

if ([string]::IsNullOrWhiteSpace($ApiKeyProtected)) {
    $secureApiKey = Read-Host 'Nhập API key của Endpoint (không được ghi vào command history)' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
    try {
        $plainApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        $ApiKeyProtected = ($plainApiKey | & $exe --protect-secret-stdin | Select-Object -Last 1).Trim()
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
        $plainApiKey = $null
    }
    if ([string]::IsNullOrWhiteSpace($ApiKeyProtected)) {
        throw 'Không tạo được ApiKeyProtected bằng DPAPI LocalMachine.'
    }
}

$config = @{
    FVNEndpointAgent = @{
        ApiBaseUrl = $ApiBaseUrl
        DeviceKey = $DeviceKey
        ApiKeyProtected = $ApiKeyProtected
        IntervalMinutes = 30
        CredentialRotationLeadDays = 30
    }
} | ConvertTo-Json -Depth 4

$config | Set-Content -Path (Join-Path $InstallPath 'appsettings.json') -Encoding UTF8

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
