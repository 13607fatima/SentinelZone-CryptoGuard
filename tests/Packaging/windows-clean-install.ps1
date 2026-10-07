param([Parameter(Mandatory)][string]$SourceDirectory,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
if(Test-Path -LiteralPath $OutputDirectory){throw 'New result directory required'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$app=Join-Path $env:ProgramFiles 'SentinelZone\CryptoGuard'
$data=Join-Path $env:ProgramData 'SentinelZone\CryptoGuard'
$parent=[IO.Path]::GetFullPath((Join-Path $env:ProgramData 'SentinelZone'))
$backup=Join-Path $parent ('CryptoGuard-clean-test-backup-'+[guid]::NewGuid().ToString('N'))
$clean=Join-Path $parent ('CryptoGuard-clean-test-state-'+[guid]::NewGuid().ToString('N'))
foreach($target in @($data,$backup,$clean)){if(-not [IO.Path]::GetFullPath($target).StartsWith($parent+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'State boundary'}}
$setup=Join-Path $SourceDirectory 'artifacts/installer/SentinelZone-CryptoGuard-Setup-0.22.2-rc.1-win-x64.exe'
$exe=Join-Path $app 'versions/0.22.2-rc.1/CryptoGuardAgent.exe'
$old=Get-Content (Join-Path $data 'identity.json') -Raw | ConvertFrom-Json
$results=New-Object Collections.Generic.List[object]
function Launch([string]$f,[string[]]$a){$p=Start-Process -FilePath $f -ArgumentList $a -WindowStyle Hidden -Wait -PassThru;$v=$p.ExitCode;$p.Dispose();if($v){throw "Installer exit $v"}}
function Record([string]$name,[bool]$ok,$detail){$results.Add(@{name=$name;passed=$ok;detail=$detail});$results|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputDirectory 'clean-install-results.json') -Encoding UTF8;if(-not $ok){throw $name}}
function FreshHealth(){for($i=0;$i -lt 40;$i++){if(Test-Path (Join-Path $data 'health.json')){$h=Get-Content (Join-Path $data 'health.json') -Raw|ConvertFrom-Json;if($h.agent_version -eq '0.22.2-rc.1' -and (Get-Service SentinelZoneCryptoGuard).Status -eq 'Running'){return $h}};Start-Sleep -Seconds 2};throw 'Health timeout'}
try{
 Launch (Join-Path $app 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
 Move-Item -LiteralPath $data -Destination $backup
 Launch $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
 $h=FreshHealth
 Record 'Windows clean install creates new endpoint identity' ($h.agent_id -ne $old.agent_id) @{version=$h.agent_version}
 $expected=(Get-FileHash (Join-Path $SourceDirectory 'artifacts/win-x64/CryptoGuardAgent.exe')).Hash
 Record 'Installed binary matches tested payload' ((Get-FileHash $exe).Hash -eq $expected) @{agent_sha256=$expected.ToLowerInvariant();setup_sha256=(Get-FileHash $setup).Hash.ToLowerInvariant()}
 Launch (Join-Path $app 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
 Move-Item -LiteralPath $data -Destination $clean
 Move-Item -LiteralPath $backup -Destination $data
 Launch $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
 $h=FreshHealth
 Record 'Original endpoint identity restored after clean installation test' ($h.agent_id -eq $old.agent_id) @{version=$h.agent_version}
}finally{
 if(Test-Path -LiteralPath $backup){
  if(Get-Service SentinelZoneCryptoGuard -ErrorAction SilentlyContinue){Stop-Service SentinelZoneCryptoGuard -Force}
  if(Test-Path -LiteralPath $data){Move-Item -LiteralPath $data -Destination ($clean+'-failed')}
  Move-Item -LiteralPath $backup -Destination $data
 }
}
