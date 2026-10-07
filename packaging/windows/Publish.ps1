param([string]$Dotnet='dotnet',[string]$Iscc,[switch]$SkipRestore)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=Join-Path $root 'artifacts/win-x64'
Push-Location $root
try{
    if(-not $SkipRestore){& $Dotnet restore SentinelZone.CryptoGuard.Windows.slnx --locked-mode --configfile NuGet.Config;if($LASTEXITCODE){throw 'Restore failed'}}
    & $Dotnet build SentinelZone.CryptoGuard.Windows.slnx -c Release --no-restore
    if($LASTEXITCODE){throw 'Build failed'}
    & $Dotnet tests/Windows/bin/Release/net10.0-windows/CryptoGuard.Tests.dll (Join-Path $root 'artifacts/test-state')
    if($LASTEXITCODE){throw 'Tests failed'}
    & $Dotnet tests/Contract/bin/Release/net10.0/CryptoGuard.Unified.Tests.dll --report validation/unified-windows-tests.json --fixtures contracts/replay
    if($LASTEXITCODE){throw 'Unified tests failed'}
    & $Dotnet restore tests/WindowsMigration --locked-mode --configfile NuGet.Config
    if($LASTEXITCODE){throw 'Migration locked restore failed'}
    & $Dotnet run --project tests/WindowsMigration -c Release --no-restore -- validation/migration-windows-tests.json
    if($LASTEXITCODE){throw 'Migration tests failed'}
    if(-not $SkipRestore){
        & $Dotnet restore src/CryptoGuard.Agent.Windows -r win-x64 --locked-mode -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:CryptoGuardLockProfile=win-x64-singlefile
        if($LASTEXITCODE){throw 'Single-file locked restore failed'}
    }
    & $Dotnet publish src/CryptoGuard.Agent.Windows/CryptoGuard.Agent.Windows.csproj -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:CryptoGuardLockProfile=win-x64-singlefile -o $out
    if($LASTEXITCODE){throw 'Agent publish failed'}
    if(-not $SkipRestore){
        & $Dotnet restore src/CryptoGuard.HardwareHost.Windows -r win-x64 --locked-mode -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:CryptoGuardLockProfile=win-x64-hardware
        if($LASTEXITCODE){throw 'Hardware locked restore failed'}
    }
    & $Dotnet publish src/CryptoGuard.HardwareHost.Windows/CryptoGuard.HardwareHost.Windows.csproj -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false -p:CryptoGuardLockProfile=win-x64-hardware -o (Join-Path $out 'hardware')
    if($LASTEXITCODE){throw 'Hardware publish failed'}
    Copy-Item README.md,THIRD-PARTY-NOTICES.txt -Destination $out
    Copy-Item licenses -Destination $out -Recurse -Force
    Copy-Item docs,schemas,contracts -Destination $out -Recurse -Force
    Copy-Item packaging/windows/Start-IdleHelper.ps1 -Destination $out
    if($Iscc){& $Iscc ('/DPayloadDir='+$out) ('/DOutputDir='+(Join-Path $root 'artifacts/installer')) packaging/windows/CryptoGuard.iss;if($LASTEXITCODE){throw 'Installer compile failed'}}
}finally{Pop-Location}
