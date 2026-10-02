<#[CmdletBinding()]param(
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory)][string]$ApiIisPath,
    [Parameter(Mandatory)][string]$ApiHealthUrl,
    [string]$WebIisPath,
    [string]$WebHealthUrl,
    [Parameter(Mandatory)][string]$PublicKeyFile,
    [string]$SqlServer = 'localhost',
    [string]$SqlDatabase = 'FVN_REGISTER',
    [string[]]$ApiSharedDirs = @('logs','uploads','App_Data'),
    [string[]]$WebSharedDirs = @('logs'),
    [int]$KeepReleases = 5,
    [System.Management.Automation.PSCredential]$RunAs,
    [switch]$Force
)
$ErrorActionPreference='Stop'
$p=[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent();if(-not$p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run as Administrator.'}
if(-not(Test-Path -LiteralPath $PublicKeyFile)){throw "Public key not found: $PublicKeyFile"}
Import-Module WebAdministration
. (Join-Path $PSScriptRoot 'Updater.Common.ps1')
$components=[ordered]@{api=@{iisPath=$ApiIisPath;healthUrl=$ApiHealthUrl;sharedDirs=$ApiSharedDirs}}
if($WebIisPath){if(-not$WebHealthUrl){throw '-WebHealthUrl is required with -WebIisPath.'};$components['web']=@{iisPath=$WebIisPath;healthUrl=$WebHealthUrl;sharedDirs=$WebSharedDirs}}
if(-not$Force -and (Read-Host "Type YES to install the online updater under $Root") -ne 'YES'){return}
$dirs=@('updater','keys','patch-inbox','patch-status','patch-processed','patch-rejected','work');foreach($k in $components.Keys){$dirs+="releases\$k";$dirs+="shared\$k"};foreach($d in $dirs){New-Item -ItemType Directory -Path (Join-Path $Root $d) -Force|Out-Null}
foreach($f in 'Updater.Common.ps1','Apply-Patch.ps1','Rollback-Release.ps1'){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $f) -Destination (Join-Path $Root "updater\$f") -Force};Copy-Item -LiteralPath $PublicKeyFile -Destination (Join-Path $Root 'keys\public.xml') -Force
$cfg=[ordered]@{root=$Root;keepReleases=$KeepReleases;healthTimeoutSec=90;maxExtractMb=1500;preserveFiles=@('appsettings.Production.json','web.config');sql=[ordered]@{server=$SqlServer;database=$SqlDatabase};components=$components};Save-Json -Object $cfg -Path (Join-Path $Root 'updater\updater.config.json') -Depth 8
$sysAdmin=@('/grant:r','*S-1-5-18:(OI)(CI)F','*S-1-5-32-544:(OI)(CI)F');function Lock-Dir{param([string]$Path,[string[]]$Extra=@());& icacls.exe $Path /inheritance:r @sysAdmin @Extra|Out-Null;if($LASTEXITCODE-ne0){throw "icacls failed on $Path"}}
Lock-Dir (Join-Path $Root 'updater');Lock-Dir (Join-Path $Root 'work')
$pools=@{};foreach($k in $components.Keys){$pools[$k]=(Get-Item -Path $components[$k].iisPath).applicationPool};$apiId="IIS AppPool\$($pools['api'])";Lock-Dir (Join-Path $Root 'patch-inbox') @('/grant:r',"${apiId}:(OI)(CI)M");Lock-Dir (Join-Path $Root 'patch-status') @('/grant:r',"${apiId}:(OI)(CI)RX");Lock-Dir (Join-Path $Root 'keys') @('/grant:r',"${apiId}:(OI)(CI)RX")
foreach($k in $components.Keys){$id="IIS AppPool\$($pools[$k])";& icacls.exe (Join-Path $Root "releases\$k") /grant:r "${id}:(OI)(CI)RX"|Out-Null;& icacls.exe (Join-Path $Root "shared\$k") /grant:r "${id}:(OI)(CI)M"|Out-Null}
Initialize-Updater -ConfigPath (Join-Path $Root 'updater\updater.config.json')
foreach($k in $components.Keys){$c=Get-Component $k;$cur=Get-SitePath -IisPath $c.iisPath;$dest=Join-Path $Root "releases\$k\initial";if(Test-Path $dest){continue};$skip=@();foreach($d in $c.sharedDirs){$skip+=Join-Path $cur $d};New-Item -ItemType Directory -Path $dest -Force|Out-Null;$xd=@();if($skip.Count-gt0){$xd=@('/XD')+$skip};Invoke-Robocopy -Source $cur -Destination $dest -Extra $xd;foreach($d in $c.sharedDirs){$src=Join-Path $cur $d;$shared=Join-Path $Root "shared\$k\$d";if(Test-Path $src){New-Item -ItemType Directory -Path $shared -Force|Out-Null;Invoke-Robocopy -Source $src -Destination $shared};New-Junction -Link (Join-Path $dest $d) -Target $shared};$pool=Get-SitePool -IisPath $c.iisPath;Set-SitePath -IisPath $c.iisPath -Path $dest;Restart-Pool -Pool $pool;if(-not(Wait-Healthy -Url $c.healthUrl -TimeoutSec 90)){Set-SitePath -IisPath $c.iisPath -Path $cur;Restart-Pool -Pool $pool;throw "[$k] health check failed after migration; reverted."}}
$script=Join-Path $Root 'updater\Apply-Patch.ps1';$action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`"";$trigger=New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration (New-TimeSpan -Days 3650);$settings=New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 30) -StartWhenAvailable
if($RunAs){Register-ScheduledTask -TaskName 'FVN_REGISTER_Updater' -Action $action -Trigger $trigger -Settings $settings -User $RunAs.UserName -Password $RunAs.GetNetworkCredential().Password -RunLevel Highest -Force|Out-Null}else{Register-ScheduledTask -TaskName 'FVN_REGISTER_Updater' -Action $action -Trigger $trigger -Settings $settings -User 'SYSTEM' -RunLevel Highest -Force|Out-Null}
Write-Host "Updater installed. Configure SystemUpdate in appsettings.Production.json: Enabled=true, InboxPath=$Root\patch-inbox, StatusPath=$Root\patch-status, PublicKeyPath=$Root\keys\public.xml" -ForegroundColor Green
