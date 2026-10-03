param(
    [Parameter(Mandatory = $true)] [string]$InstallPath,
    [Parameter(Mandatory = $true)] [string]$ApiBaseUrl,
    [Parameter(Mandatory = $true)] [string]$BootstrapFile
)

$ErrorActionPreference = 'Stop'

$uri = [Uri]$ApiBaseUrl
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https') { throw 'ApiBaseUrl phải là HTTPS.' }

$sourceExe = Join-Path $PSScriptRoot 'FVN_REGISTER.EndpointAgent.exe'
if (-not (Test-Path $sourceExe)) { throw "Không tìm thấy Agent: $sourceExe" }
if (-not (Test-Path $BootstrapFile)) { throw "Không tìm thấy bootstrap file: $BootstrapFile" }

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
$targetExe = Join-Path $InstallPath 'FVN_REGISTER.EndpointAgent.exe'
Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force

$targetBootstrap = Join-Path $InstallPath 'lanscope-bootstrap.json'
Copy-Item -LiteralPath $BootstrapFile -Destination $targetBootstrap -Force

$configPath = Join-Path $InstallPath 'appsettings.json'
$config = @{
    FVNEndpointAgent = @{
        ApiBaseUrl = $ApiBaseUrl
        DeviceKey = ''
        ApiKeyProtected = ''
        BootstrapPath = 'lanscope-bootstrap.json'
        IntervalMinutes = 30
        CredentialRotationLeadDays = 30
    }
} | ConvertTo-Json -Depth 4

$tempPath = $configPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [System.IO.File]::WriteAllText($tempPath, $config, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tempPath -Destination $configPath -Force
}
finally {
    if (Test-Path $tempPath) { Remove-Item $tempPath -Force -ErrorAction SilentlyContinue }
}

$serviceName = 'FVNRegisterEndpointAgent'
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $serviceName binPath= ('"' + $targetExe + '"') start= auto DisplayName= 'FVN Register Endpoint Agent' | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/none/0 | Out-Null
sc.exe config $serviceName obj= LocalSystem | Out-Null
Start-Service -Name $serviceName

$svc = Get-Service -Name $serviceName
if ($svc.Status -ne 'Running') { throw ('Endpoint Agent service failed to start. Status=' + $svc.Status) }
exit 0
