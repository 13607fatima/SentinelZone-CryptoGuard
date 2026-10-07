param([Parameter(Mandatory)][string]$Iscc)
$ErrorActionPreference='Stop'
$pfx=Join-Path $env:RUNNER_TEMP 'cryptoguard-release.pfx'
$certificate=$null
try {
 [IO.File]::WriteAllBytes($pfx,[Convert]::FromBase64String($env:SIGNING_PFX_BASE64))
 $password=ConvertTo-SecureString $env:SIGNING_PFX_PASSWORD -AsPlainText -Force
 $certificate=Import-PfxCertificate -FilePath $pfx -CertStoreLocation Cert:\CurrentUser\My -Password $password
 $signTool=(Get-ChildItem 'C:/Program Files (x86)/Windows Kits/10/bin/*/x64/signtool.exe'|Sort-Object FullName|Select-Object -Last 1).FullName
 & "$PSScriptRoot/Sign-Release.ps1" -CertificateThumbprint $certificate.Thumbprint -TimestampUrl $env:SIGNING_TIMESTAMP_URL -SignTool $signTool -Iscc $Iscc
} finally {
 if($certificate){Remove-Item -LiteralPath ('Cert:\CurrentUser\My\'+$certificate.Thumbprint)}
 if(Test-Path -LiteralPath $pfx){Remove-Item -LiteralPath $pfx}
}
