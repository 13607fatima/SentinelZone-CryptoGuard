$ErrorActionPreference='Stop'
$binary=Join-Path $PSScriptRoot 'CryptoGuardAgent.exe'
Start-Process -FilePath $binary -ArgumentList @('idle-helper','--watch') -WindowStyle Hidden
