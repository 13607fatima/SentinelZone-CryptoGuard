param([Parameter(Mandatory)][string]$CertificateThumbprint,[Parameter(Mandatory)][uri]$TimestampUrl,[Parameter(Mandatory)][string]$SignTool,[Parameter(Mandatory)][string]$Iscc)
$ErrorActionPreference='Stop'
if($TimestampUrl.Scheme -ne 'https'){throw 'Use an HTTPS RFC3161 timestamp service'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$payload=Join-Path $root 'artifacts/win-x64'
foreach($binary in @((Join-Path $payload 'CryptoGuardAgent.exe'),(Join-Path $payload 'hardware/CryptoGuardHardwareHost.exe'))){
    & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl.AbsoluteUri /td SHA256 $binary
    if($LASTEXITCODE){throw 'Binary signing failed'}
    & $SignTool verify /pa /all $binary
    if($LASTEXITCODE){throw 'Binary signature verification failed'}
}
& $Iscc ('/DPayloadDir='+$payload) ('/DOutputDir='+(Join-Path $root 'artifacts/installer')) (Join-Path $PSScriptRoot 'CryptoGuard.iss')
if($LASTEXITCODE){throw 'Installer compilation failed'}
$installer=Join-Path $root 'artifacts/installer/SentinelZone-CryptoGuard-Setup-0.22.2-rc.1-win-x64.exe'
& $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl.AbsoluteUri /td SHA256 $installer
if($LASTEXITCODE){throw 'Installer signing failed'}
& $SignTool verify /pa /all $installer
if($LASTEXITCODE){throw 'Installer signature verification failed'}
Write-Output 'Signatures complete. Regenerate release inventory and checksums from these final files before distribution.'
