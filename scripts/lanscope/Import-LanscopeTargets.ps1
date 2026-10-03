param(
    [Parameter(Mandatory=$true)] [string]$ApiBaseUrl,
    [Parameter(Mandatory=$true)] [string]$CsvPath,
    [Parameter(Mandatory=$true)] [string]$DeploymentCode,
    [int]$DeploymentId = 0,
    [switch]$ReissuePending,
    [string]$PackageVersion = '',
    [string]$OutputDirectory = '.\lanscope-bootstrap',
    [int]$BatchSize = 500
)
$ErrorActionPreference='Stop'
$uri=[Uri]$ApiBaseUrl
if(-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https'){throw 'ApiBaseUrl phải là HTTPS.'}
if(-not (Test-Path $CsvPath)){throw "Không tìm thấy CSV: $CsvPath"}
if($BatchSize -lt 1 -or $BatchSize -gt 5000){throw 'BatchSize phải từ 1 đến 5000.'}
$AccessToken=$env:FVN_LANSCOPE_ACCESS_TOKEN
if([string]::IsNullOrWhiteSpace($AccessToken)){ $secure=Read-Host 'Nhập FVN access token' -AsSecureString; $AccessToken=[System.Net.NetworkCredential]::new('', $secure).Password }
if([string]::IsNullOrWhiteSpace($AccessToken)){throw 'Thiếu access token. Đặt FVN_LANSCOPE_ACCESS_TOKEN trong môi trường hoặc nhập prompt bảo mật; không truyền secret trên command line.'}
$headers=@{Authorization="Bearer $AccessToken"}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Get-Field($row,[string[]]$names){foreach($n in $names){$p=$row.PSObject.Properties[$n];if($p -and -not [string]::IsNullOrWhiteSpace([string]$p.Value)){return [string]$p.Value}}return $null}
function Post-Json($url,$body){Invoke-RestMethod -Method Post -Uri $url -Headers $headers -ContentType 'application/json' -Body ($body|ConvertTo-Json -Depth 10)}
function Get-Json($url){Invoke-RestMethod -Method Get -Uri $url -Headers $headers}
function Protect-BootstrapFile($path){
    & icacls $path /inheritance:r | Out-Null
    & icacls $path /grant:r "SYSTEM:(F)" "Administrators:(F)" | Out-Null
    if($LASTEXITCODE -ne 0){throw "Không thể đặt ACL an toàn cho bootstrap: $path"}
}
function Write-Bootstrap($item){
    $path=Join-Path $OutputDirectory $item.bootstrapFileName
    Set-Content -LiteralPath $path -Value $item.bootstrapJson -Encoding UTF8 -NoNewline
    Protect-BootstrapFile $path
    return $path
}

$rows=@(Import-Csv -LiteralPath $CsvPath)
if($rows.Count -lt 1){throw 'CSV không có dữ liệu.'}
$base=$ApiBaseUrl.TrimEnd('/')

if($DeploymentId -le 0){
    $deployment=Post-Json "$base/api/security/endpoints/lanscope/deployments" @{deploymentCode=$DeploymentCode;packageVersion=$PackageVersion}
    $DeploymentId=[int]$deployment.data.deploymentId
}

$existing=@{}
try{
    $current=Get-Json "$base/api/security/endpoints/lanscope/deployments/$DeploymentId/targets"
    foreach($item in @($current.data)){ $existing[$item.targetKey.ToString().Trim().ToUpperInvariant()]=$true }
}catch{ if($DeploymentId -gt 0 -and $_.Exception.Response.StatusCode.value__ -eq 404){throw "DeploymentId $DeploymentId không tồn tại."} }

$manifest=New-Object System.Collections.Generic.List[object]
for($offset=0;$offset -lt $rows.Count;$offset+=$BatchSize){
    $slice=@($rows | Select-Object -Skip $offset -First $BatchSize)
    $targets=@()
    for($j=0;$j -lt $slice.Count;$j++){
        $row=$slice[$j];$csvLine=$offset+$j+2
        $targetId=Get-Field $row @('TargetId','ClientId','ComputerName','Client ID','ClientId')
        if([string]::IsNullOrWhiteSpace($targetId)){throw "Dòng CSV $csvLine: thiếu TargetId/ClientId/ComputerName."}
        if($existing.ContainsKey($targetId.Trim().ToUpperInvariant())){continue}
        $targets+=@{targetId=$targetId;client=@{
            clientId=Get-Field $row @('ClientId','Client ID');computerName=Get-Field $row @('ComputerName','Computer Name')
            ip=Get-Field $row @('IP','Ip','IPAddress','IP Address');mac=Get-Field $row @('MAC','Mac','MAC Address')
            serialNumber=Get-Field $row @('SerialNumber','Serial Number');windowsUser=Get-Field $row @('WindowsUser','Windows User','User')
            domain=Get-Field $row @('Domain');ou=Get-Field $row @('OU','OrganizationalUnit','Organizational Unit')
            group=Get-Field $row @('Group','LanscopeGroup');os=Get-Field $row @('OS','Os');manufacturer=Get-Field $row @('Manufacturer');model=Get-Field $row @('Model')
        }}
    }
    if($targets.Count -eq 0){continue}
    $response=Post-Json "$base/api/security/endpoints/lanscope/deployments/$DeploymentId/targets" $targets
    foreach($item in @($response.data)){ $path=Write-Bootstrap $item; $manifest.Add([pscustomobject]@{TargetId=$item.targetId;TargetKey=$item.targetKey;BootstrapFile=$path}) }
}
if($ReissuePending){
    $response=Post-Json "$base/api/security/endpoints/lanscope/deployments/$DeploymentId/targets/reissue-pending" @{}
    foreach($item in @($response.data)){ $path=Write-BootstrapFile $item; $manifest.Add([pscustomobject]@{TargetId=$item.targetId;TargetKey=$item.targetKey;BootstrapFile=$path}) }
}
$manifestPath=Join-Path $OutputDirectory 'manifest.csv'
$manifest|Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding UTF8
Write-Host "DeploymentId=$DeploymentId"
Write-Host "Bootstrap files=$($manifest.Count)"
Write-Host "Manifest=$manifestPath"
