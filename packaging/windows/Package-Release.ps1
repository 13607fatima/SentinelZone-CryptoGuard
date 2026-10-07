param([Parameter(Mandatory)][string]$NugetCache,[Parameter(Mandatory)][string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ReleaseDirectory=[IO.Path]::GetFullPath($ReleaseDirectory)
if(Test-Path -LiteralPath $ReleaseDirectory){
    if(Get-ChildItem -LiteralPath $ReleaseDirectory -Force | Select-Object -First 1){throw 'Use an empty release directory to avoid stale artifacts'}
}else{New-Item -ItemType Directory -Path $ReleaseDirectory -Force | Out-Null}
[xml]$properties=Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$version=[string]$properties.Project.PropertyGroup.Version
$payload=Join-Path $root 'artifacts\win-x64'
$installer=Join-Path $root ('artifacts\installer\SentinelZone-CryptoGuard-Setup-'+$version+'-win-x64.exe')
if(-not (Test-Path $installer) -or -not (Test-Path (Join-Path $payload 'CryptoGuardAgent.exe'))){throw 'Run Publish.ps1 with -Iscc first'}
Copy-Item -LiteralPath $installer -Destination $ReleaseDirectory
Copy-Item -LiteralPath (Join-Path $payload 'CryptoGuardAgent.exe') -Destination (Join-Path $ReleaseDirectory ('SentinelZone-CryptoGuard-'+$version+'-win-x64.exe'))
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($payload,(Join-Path $ReleaseDirectory ('SentinelZone-CryptoGuard-Portable-'+$version+'-win-x64.zip')),[IO.Compression.CompressionLevel]::Optimal,$false)
$sourcePath=Join-Path $ReleaseDirectory ('SentinelZone-CryptoGuard-Source-'+$version+'.zip')
$archive=[IO.Compression.ZipFile]::Open($sourcePath,[IO.Compression.ZipArchiveMode]::Create)
try{
    # Explicit source roots keep local caches, test state, SSH material and release
    # output out of source archives even when build tools use a shared workspace.
    foreach($file in Get-ChildItem -LiteralPath $root -File -Force){
        if($file.Name -notmatch '^(\.gitignore|\.gitattributes|Directory.Build.props|global.json|NuGet.Config|.*\.md|.*\.slnx|THIRD-PARTY-NOTICES.txt)$'){continue}
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$file.Name) | Out-Null
    }
    foreach($directory in @('.github','src','tests','packaging','schemas','contracts','rules','research','scripts','debian','docs','licenses')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $root $directory) -File -Recurse -Force){
            $relative=$file.FullName.Substring($root.Length+1)
            if(($relative -split '[\\/]') | Where-Object {$_ -in @('bin','obj','artifacts','__pycache__','.venv')}){continue}
            if($file.Extension -in @('.pfx','.snk','.key','.pub')){throw 'Unexpected key material in source tree'}
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative.Replace('\','/')) | Out-Null
        }
    }
}finally{$archive.Dispose()}
& (Join-Path $PSScriptRoot 'Inventory.ps1') -NugetCache $NugetCache -ReleaseDirectory $ReleaseDirectory
Write-Output ('Release artifacts and checksums: '+$ReleaseDirectory)
