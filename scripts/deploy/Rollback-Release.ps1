[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'updater.config.json'),
    [string]$Component,
    [string]$Version,
    [switch]$List
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Updater.Common.ps1')
Initialize-Updater -ConfigPath $ConfigPath
if($List -or -not$Component){foreach($name in Get-ComponentNames){$c=Get-Component $name;$cur=(Get-SitePath -IisPath $c.iisPath).TrimEnd('\');Write-Host "== $name (active: $cur)";Get-ChildItem -LiteralPath (Join-Path $script:Root "releases\$name") -Directory|Sort-Object CreationTime -Descending|ForEach-Object{Write-Host ("{0} {1:yyyy-MM-dd HH:mm} {2}" -f $(if($_.FullName.TrimEnd('\') -ieq $cur){'*'}else{' '}),$_.CreationTime,$_.Name)}};return}
if(-not$Version){throw '-Version is required.'};$c=Get-Component $Component;if($null-eq$c){throw "Unknown component '$Component'."};$target=Join-Path $script:Root "releases\$Component\$Version";if(-not(Test-Path -LiteralPath $target)){throw "Release not found: $target"}
$old=Get-SitePath -IisPath $c.iisPath;$pool=Get-SitePool -IisPath $c.iisPath;Set-SitePath -IisPath $c.iisPath -Path $target;Restart-Pool -Pool $pool;if(-not(Wait-Healthy -Url $c.healthUrl -TimeoutSec ([int]$script:Cfg.healthTimeoutSec))){Set-SitePath -IisPath $c.iisPath -Path $old;Restart-Pool -Pool $pool;throw "Health check failed for $Component/$Version; reverted."};Write-UpdLog "Switched $Component to $Version."
