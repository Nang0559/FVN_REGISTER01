param(
    [Parameter(Mandatory=$true)] [string]$ApiBaseUrl,
    [Parameter(Mandatory=$true)] [string]$AccessToken,
    [Parameter(Mandatory=$true)] [string]$CsvPath,
    [Parameter(Mandatory=$true)] [string]$DeploymentCode,
    [string]$PackageVersion = '',
    [string]$OutputDirectory = '.\lanscope-bootstrap',
    [int]$BatchSize = 500
)
$ErrorActionPreference='Stop'
$uri=[Uri]$ApiBaseUrl
if(-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https'){throw 'ApiBaseUrl phải là HTTPS.'}
if(-not (Test-Path $CsvPath)){throw "Không tìm thấy CSV: $CsvPath"}
if($BatchSize -lt 1 -or $BatchSize -gt 5000){throw 'BatchSize phải từ 1 đến 5000.'}
$headers=@{Authorization="Bearer $AccessToken"}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
function Get-Field($row,[string[]]$names) {
    foreach($n in $names){$p=$row.PSObject.Properties[$n];if($p -and -not [string]::IsNullOrWhiteSpace([string]$p.Value)){return [string]$p.Value}}
    return $null
}
function Post-Json($url,$body){Invoke-RestMethod -Method Post -Uri $url -Headers $headers -ContentType 'application/json' -Body ($body|ConvertTo-Json -Depth 10)}
$rows=@(Import-Csv -LiteralPath $CsvPath)
if($rows.Count -lt 1){throw 'CSV không có dữ liệu.'}
$deployment=Post-Json "$($ApiBaseUrl.TrimEnd('/'))/api/security/endpoints/lanscope/deployments" @{deploymentCode=$DeploymentCode;packageVersion=$PackageVersion}
$deploymentId=[int]$deployment.data.deploymentId
$manifest=New-Object System.Collections.Generic.List[object]
for($offset=0;$offset -lt $rows.Count;$offset+=$BatchSize){
    $slice=$rows | Select-Object -Skip $offset -First $BatchSize
    $targets=@()
    foreach($row in $slice){
        $targetId=Get-Field $row @('TargetId','ClientId','ComputerName','Client ID','ClientId')
        if([string]::IsNullOrWhiteSpace($targetId)){throw "Dòng CSV $($offset+1): thiếu TargetId/ClientId/ComputerName."}
        $targets+=@{targetId=$targetId;client=@{
            clientId=Get-Field $row @('ClientId','Client ID')
            computerName=Get-Field $row @('ComputerName','Computer Name')
            ip=Get-Field $row @('IP','Ip','IPAddress','IP Address')
            mac=Get-Field $row @('MAC','Mac','MAC Address')
            serialNumber=Get-Field $row @('SerialNumber','Serial Number')
            windowsUser=Get-Field $row @('WindowsUser','Windows User','User')
            domain=Get-Field $row @('Domain')
            ou=Get-Field $row @('OU','OrganizationalUnit','Organizational Unit')
            group=Get-Field $row @('Group','LanscopeGroup')
            os=Get-Field $row @('OS','Os')
            manufacturer=Get-Field $row @('Manufacturer')
            model=Get-Field $row @('Model')
        }}
    }
    $response=Post-Json "$($ApiBaseUrl.TrimEnd('/'))/api/security/endpoints/lanscope/deployments/$deploymentId/targets" $targets
    foreach($item in $response.data){
        $path=Join-Path $OutputDirectory $item.bootstrapFileName
        Set-Content -LiteralPath $path -Value $item.bootstrapJson -Encoding UTF8
        $manifest.Add([pscustomobject]@{TargetId=$item.targetId;TargetKey=$item.targetKey;BootstrapFile=$path})
    }
}
$manifestPath=Join-Path $OutputDirectory 'manifest.csv'
$manifest|Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding UTF8
Write-Host "DeploymentId=$deploymentId"
Write-Host "Bootstrap files=$($manifest.Count)"
Write-Host "Manifest=$manifestPath"
