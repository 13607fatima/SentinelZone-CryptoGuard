param([Parameter(Mandatory)][string]$NugetCache,[Parameter(Mandatory)][string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ReleaseDirectory=[IO.Path]::GetFullPath($ReleaseDirectory)
New-Item -ItemType Directory -Force $ReleaseDirectory | Out-Null
$packages=@();$seen=@{};$relationships=@()
foreach($lock in Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter packages*.lock.json){
    $data=Get-Content -LiteralPath $lock.FullName -Raw | ConvertFrom-Json
    foreach($targetProperty in $data.dependencies.PSObject.Properties){
        foreach($packageProperty in $targetProperty.Value.PSObject.Properties){
            $name=$packageProperty.Name
            $p=$packageProperty.Value
            if($p.type -eq 'Project'){continue}
            $key=$name.ToLowerInvariant()+'/'+$p.resolved
            if($seen.ContainsKey($key)){continue};$seen[$key]=$true
            $id='SPDXRef-Package-'+($name+'-'+$p.resolved -replace '[^A-Za-z0-9.-]','-')
            $license='NOASSERTION';$nuspec=Join-Path $NugetCache ($key+'/'+$name.ToLowerInvariant()+'.nuspec')
            if(Test-Path -LiteralPath $nuspec){[xml]$xml=Get-Content -LiteralPath $nuspec -Raw;$node=$xml.SelectSingleNode('//*[local-name()="license"]');if($node -and $node.type -eq 'expression'){$license=$node.InnerText}}
            $checksum=if($p.contentHash){@(@{algorithm='SHA512';checksumValue=[BitConverter]::ToString([Convert]::FromBase64String($p.contentHash)).Replace('-','').ToLowerInvariant()})}else{@()}
            $packages+=@{SPDXID=$id;name=$name;versionInfo=$p.resolved;downloadLocation=('https://api.nuget.org/v3-flatcontainer/'+$key+'/'+$name.ToLowerInvariant()+'.'+$p.resolved+'.nupkg');filesAnalyzed=$false;licenseConcluded='NOASSERTION';licenseDeclared=$license;copyrightText='NOASSERTION';checksums=@($checksum | Where-Object {$null -ne $_});comment='Resolved build dependency inventory. Some platform-specific dependencies are not in the Windows payload; actual payload files are listed separately.'}
            $relationships+=@{spdxElementId='SPDXRef-Application';relationshipType='DEPENDS_ON';relatedSpdxElement=$id}
        }
    }
}
$packages+=@{SPDXID='SPDXRef-Application';name='SentinelZone CryptoGuard';versionInfo='0.22.2-rc.1';downloadLocation='NOASSERTION';filesAnalyzed=$false;licenseConcluded='NOASSERTION';licenseDeclared='NOASSERTION';copyrightText='NOASSERTION'}
# Runtime packs are distributed even though SDK restore can omit them from application lock files.
foreach($name in @('microsoft.netcore.app.runtime.win-x64')){
    $version='10.0.12';$dir=Join-Path $NugetCache "$name/$version"
    if(Test-Path -LiteralPath $dir){
        $id='SPDXRef-Runtime-'+$name
        $packages+=@{SPDXID=$id;name=$name;versionInfo=$version;downloadLocation="https://api.nuget.org/v3-flatcontainer/$name/$version/$name.$version.nupkg";filesAnalyzed=$false;licenseConcluded='NOASSERTION';licenseDeclared='MIT';copyrightText='NOASSERTION'}
        $relationships+=@{spdxElementId='SPDXRef-Application';relationshipType='DEPENDS_ON';relatedSpdxElement=$id}
    }
}
$files=@();$index=0;$payload=Join-Path $root 'artifacts/win-x64'
foreach($file in Get-ChildItem -LiteralPath $payload -Recurse -File){
    $id='SPDXRef-File-'+(++$index)
    $files+=@{SPDXID=$id;fileName=('./portable/'+$file.FullName.Substring($payload.TrimEnd('\').Length+1).Replace('\','/'));checksums=@(@{algorithm='SHA256';checksumValue=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()});licenseConcluded='NOASSERTION';copyrightText='NOASSERTION'}
    $relationships+=@{spdxElementId='SPDXRef-Application';relationshipType='CONTAINS';relatedSpdxElement=$id}
}
$relationships+=@{spdxElementId='SPDXRef-DOCUMENT';relationshipType='DESCRIBES';relatedSpdxElement='SPDXRef-Application'}
$sbom=@{spdxVersion='SPDX-2.3';dataLicense='CC0-1.0';SPDXID='SPDXRef-DOCUMENT';name='SentinelZone-CryptoGuard-Windows-alpha';documentNamespace=('https://spdx.org/spdxdocs/CryptoGuard-'+[guid]::NewGuid());creationInfo=@{created=[DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ');creators=@('Tool: CryptoGuard-local-inventory-1')};packages=$packages;files=$files;relationships=$relationships}
$sbom|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'sbom.spdx.json') -Encoding utf8
$artifacts=@(Get-ChildItem -LiteralPath $ReleaseDirectory -File | Where-Object Name -NotIn @('SHA256SUMS','release-manifest.json') | ForEach-Object {@{name=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
$manifest=@{product='SentinelZone CryptoGuard';version='0.22.2-rc.1';status='unsigned-local-evaluation';createdUtc=[DateTime]::UtcNow.ToString('o');rid='win-x64';sdk='10.0.401';runtime='10.0.12';selfContained=$true;hardwareLibrary='LibreHardwareMonitorLib 0.9.6';signing='not_signed';provenance='local build; no hosted CI attestation';acceptance='See TEST-REPORT and ROADMAP-MATRIX. Production acceptance is incomplete.';artifacts=$artifacts}
$manifest|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'release-manifest.json') -Encoding utf8
Get-ChildItem -LiteralPath $ReleaseDirectory -File | Where-Object Name -ne 'SHA256SUMS' | Sort-Object Name | ForEach-Object {((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_.Name)} | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS') -Encoding ascii
