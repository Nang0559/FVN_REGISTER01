param(
    [Parameter(Mandatory = $true)] [string]$InstallPath,
    [Parameter(Mandatory = $true)] [string]$ApiBaseUrl,
    [Parameter(Mandatory = $true)] [string]$BootstrapFile,
    [int]$EnrollmentWaitMinutes = 15
)

$ErrorActionPreference = 'Stop'
$serviceName = 'FVNRegisterEndpointAgent'
$uri = [Uri]$ApiBaseUrl
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https') { throw 'ApiBaseUrl phải là HTTPS.' }
if (-not (Test-Path -LiteralPath $BootstrapFile -PathType Leaf)) { throw "Không tìm thấy bootstrap file: $BootstrapFile" }

$sourceExe = Join-Path $PSScriptRoot 'FVN_REGISTER.EndpointAgent.exe'
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw "Không tìm thấy Agent: $sourceExe" }

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
& icacls.exe $BootstrapFile /inheritance:r /grant:r 'SYSTEM:(F)' 'Administrators:(F)' | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Không thể đặt ACL an toàn cho bootstrap source: $BootstrapFile" }
$targetExe = Join-Path $InstallPath 'FVN_REGISTER.EndpointAgent.exe'
$targetBootstrap = Join-Path $InstallPath 'lanscope-bootstrap.json'
$configPath = Join-Path $InstallPath 'appsettings.json'

$existingConfig = $null
if (Test-Path -LiteralPath $configPath) {
    try { $existingConfig = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json } catch { $existingConfig = $null }
}
$existingAgent = if ($existingConfig) { $existingConfig.FVNEndpointAgent } else { $null }
$deviceKey = if ($existingAgent -and $existingAgent.DeviceKey) { [string]$existingAgent.DeviceKey } else { '' }
$apiKeyProtected = if ($existingAgent -and $existingAgent.ApiKeyProtected) { [string]$existingAgent.ApiKeyProtected } else { '' }
$isAlreadyEnrolled = [bool]($deviceKey -and $apiKeyProtected)
$bootstrapPath = if ($isAlreadyEnrolled) { '' } else { 'lanscope-bootstrap.json' }
$lanscopeClientId = if ($existingAgent -and $existingAgent.LanscopeClientId) { [string]$existingAgent.LanscopeClientId } else { '' }
$lanscopeOu = if ($existingAgent -and $existingAgent.LanscopeOrganizationalUnit) { [string]$existingAgent.LanscopeOrganizationalUnit } else { '' }
$lanscopeGroup = if ($existingAgent -and $existingAgent.LanscopeGroup) { [string]$existingAgent.LanscopeGroup } else { '' }

# Stop first so the running process cannot lock the executable during upgrade.
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force -ErrorAction Stop
        $deadline = (Get-Date).AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 500
            $existing.Refresh()
        } while ($existing.Status -ne 'Stopped' -and (Get-Date) -lt $deadline)
        if ($existing.Status -ne 'Stopped') { throw 'Không thể dừng FVNRegisterEndpointAgent trước khi nâng cấp.' }
    }
}

# Copy only after the old process has stopped.
Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force
if (-not $isAlreadyEnrolled) { Copy-Item -LiteralPath $BootstrapFile -Destination $targetBootstrap -Force }

$config = @{
    FVNEndpointAgent = @{
        ApiBaseUrl = $ApiBaseUrl
        DeviceKey = $deviceKey
        ApiKeyProtected = $apiKeyProtected
        BootstrapPath = $bootstrapPath
        LanscopeClientId = $lanscopeClientId
        LanscopeOrganizationalUnit = $lanscopeOu
        LanscopeGroup = $lanscopeGroup
        IntervalMinutes = if ($existingAgent -and $existingAgent.IntervalMinutes) { [int]$existingAgent.IntervalMinutes } else { 30 }
        CredentialRotationLeadDays = if ($existingAgent -and $existingAgent.CredentialRotationLeadDays) { [int]$existingAgent.CredentialRotationLeadDays } else { 30 }
    }
} | ConvertTo-Json -Depth 4

$tempPath = $configPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [System.IO.File]::WriteAllText($tempPath, $config, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tempPath -Destination $configPath -Force
}
finally {
    if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
}

# The service needs access, ordinary local users do not.
& icacls.exe $InstallPath /inheritance:r /grant:r 'SYSTEM:(OI)(CI)(F)' 'Administrators:(OI)(CI)(F)' 'Users:(OI)(CI)(RX)' | Out-Null
& icacls.exe $targetBootstrap /inheritance:r /grant:r 'SYSTEM:(F)' 'Administrators:(F)' | Out-Null
& icacls.exe $configPath /inheritance:r /grant:r 'SYSTEM:(F)' 'Administrators:(F)' | Out-Null

if (-not $existing) {
    & sc.exe create $serviceName binPath= ('"' + $targetExe + '"') start= auto DisplayName= 'FVN Register Endpoint Agent' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc create thất bại. ExitCode=$LASTEXITCODE" }
}
else {
    & sc.exe config $serviceName binPath= ('"' + $targetExe + '"') start= auto obj= LocalSystem | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc config thất bại. ExitCode=$LASTEXITCODE" }
}

& sc.exe config $serviceName obj= LocalSystem | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc config LocalSystem thất bại. ExitCode=$LASTEXITCODE" }
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/none/0 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc failure thất bại. ExitCode=$LASTEXITCODE" }
& sc.exe failureflag $serviceName 1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc failureflag thất bại. ExitCode=$LASTEXITCODE" }

Start-Service -Name $serviceName

# SCM "Running" only proves process start. For fresh LANSCOPE rollout wait for enrollment.
if (-not $isAlreadyEnrolled) {
    $deadline = (Get-Date).AddMinutes([Math]::Max(1, $EnrollmentWaitMinutes))
    $enrolled = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 10
        try {
            $current = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
            $agent = $current.FVNEndpointAgent
            if ($agent.DeviceKey -and $agent.ApiKeyProtected -and [string]::IsNullOrWhiteSpace([string]$agent.BootstrapPath)) {
                $enrolled = $true
                break
            }
        } catch { }
    }
    if (-not $enrolled) {
        $svc = Get-Service -Name $serviceName
        throw "Endpoint Agent chưa enroll thành công sau $EnrollmentWaitMinutes phút. Service=$($svc.Status). Reissue bootstrap token nếu token đã hết hạn."
    }
    Remove-Item -LiteralPath $targetBootstrap -Force -ErrorAction Stop
    Remove-Item -LiteralPath $BootstrapFile -Force -ErrorAction Stop
}

$svc = Get-Service -Name $serviceName
if ($svc.Status -ne 'Running') { throw ('Endpoint Agent service failed to start. Status=' + $svc.Status) }
exit 0
