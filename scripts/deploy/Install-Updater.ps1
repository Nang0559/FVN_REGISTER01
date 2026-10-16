[CmdletBinding()]
param(
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
    [int]$KeepReleases = 5
)
$ErrorActionPreference='Stop'
if(-not(Test-Path -LiteralPath $PublicKeyFile)){throw "Public key not found: $PublicKeyFile"}
. (Join-Path $PSScriptRoot 'Updater.Common.ps1')
$components=[ordered]@{api=@{iisPath=$ApiIisPath;healthUrl=$ApiHealthUrl;sharedDirs=$ApiSharedDirs}}
if($WebIisPath){if(-not$WebHealthUrl){throw '-WebHealthUrl is required with -WebIisPath.'};$components['web']=@{iisPath=$WebIisPath;healthUrl=$WebHealthUrl;sharedDirs=$WebSharedDirs}}
$dirs=@('updater','keys','patch-inbox','patch-status','patch-processed','patch-rejected','work');foreach($k in $components.Keys){$dirs+="releases\$k";$dirs+="shared\$k"};foreach($d in $dirs){New-Item -ItemType Directory -Path (Join-Path $Root $d) -Force|Out-Null}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Updater.Common.ps1') -Destination (Join-Path $Root 'updater\Updater.Common.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Apply-Patch.ps1') -Destination (Join-Path $Root 'updater\Apply-Patch.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Rollback-Release.ps1') -Destination (Join-Path $Root 'updater\Rollback-Release.ps1') -Force
Copy-Item -LiteralPath $PublicKeyFile -Destination (Join-Path $Root 'keys\public.xml') -Force
$cfg=[ordered]@{root=$Root;keepReleases=$KeepReleases;healthTimeoutSec=90;maxExtractMb=1500;preserveFiles=@('appsettings.Production.json','web.config');sql=[ordered]@{server=$SqlServer;database=$SqlDatabase};components=$components}
Save-Json -Object $cfg -Path (Join-Path $Root 'updater\updater.config.json') -Depth 8
Write-Host "Updater files prepared under $Root." -ForegroundColor Green
Write-Host "Next: register Apply-Patch.ps1 as a privileged Windows Scheduled Task running every minute, grant the IIS API app-pool Modify access to patch-inbox and Read access to patch-status/keys, and configure SystemUpdate in appsettings.Production.json." -ForegroundColor Yellow
