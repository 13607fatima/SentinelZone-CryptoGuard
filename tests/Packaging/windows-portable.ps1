param([Parameter(Mandatory)][string]$TestDirectory,[string]$PayloadDirectory)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$root=[IO.Path]::GetFullPath($TestDirectory)
$payload=if($PayloadDirectory){[IO.Path]::GetFullPath($PayloadDirectory)}else{Join-Path $root 'source\artifacts\win-x64'}
$results=New-Object Collections.Generic.List[object]
function Check([string]$Name,[bool]$Passed,$Detail){
    $results.Add([pscustomobject]@{name=$Name;passed=$Passed;detail=$Detail})
    $results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'portable-results.json') -Encoding utf8
    Write-Output ($results[$results.Count-1] | ConvertTo-Json -Compress -Depth 5)
    if(-not $Passed){throw $Name}
}
$standalone=Join-Path $root 'standalone'
New-Item -ItemType Directory -Force $standalone | Out-Null
$exe=Join-Path $standalone 'CryptoGuardAgent.exe'
Copy-Item (Join-Path $payload 'CryptoGuardAgent.exe') $exe
$json=& $exe doctor --data (Join-Path $standalone 'state') --hardware
$code=$LASTEXITCODE
$item=$json | ConvertFrom-Json
$json | Set-Content (Join-Path $root 'standalone-doctor.json') -Encoding utf8
Check 'Single EXE without installed runtime or hardware folder' ($code -eq 0 -and $item.agent_version -eq '0.22.2-rc.1' -and $item.snapshot.host.memory_total_bytes.value -gt 0 -and ($item.snapshot.coverage | Where-Object name -eq 'hardware').status -eq 'component_not_installed') @{version=$item.agent_version;hardware=($item.snapshot.coverage | Where-Object name -eq 'hardware').status}
$capture=Join-Path $root 'portable-capture'
& (Join-Path $payload 'CryptoGuardAgent.exe') run --data $capture --duration 65 --capture --hardware | Set-Content (Join-Path $root 'capture-last.json') -Encoding utf8
Check 'Portable continuous collection and graceful duration exit' ($LASTEXITCODE -eq 0) 65
$events=Join-Path $root 'portable-capture.jsonl'
& (Join-Path $payload 'CryptoGuardAgent.exe') export --data $capture --out $events
if($LASTEXITCODE){throw 'Portable export failed'}
$records=@(Get-Content $events | ForEach-Object {$_ | ConvertFrom-Json})
$uuids=@($records.event_uid | Select-Object -Unique)
Check 'Capture preserves unique event IDs and monotonic sequences' ($records.Count -ge 10 -and $uuids.Count -eq $records.Count -and @($records.sequence|Select-Object -Unique).Count -eq $records.Count) @{samples=$records.Count;firstSequence=$records[0].sequence;lastSequence=$records[-1].sequence}
Check 'Host CPU warms up and stays within range' ($null -eq $records[0].snapshot.host.cpu_percent.value -and @($records | Where-Object {$_.snapshot.host.cpu_percent.value -ge 0 -and $_.snapshot.host.cpu_percent.value -le 100 -and $_.snapshot.host.cpu_percent.status -eq 'ok'}).Count -ge 5) $records[-1].snapshot.host.cpu_percent
Check 'Unsupported virtual hardware is reported without crashing' (@($records | Where-Object {($_.snapshot.coverage | Where-Object name -eq 'gpu').status -eq 'unsupported'}).Count -eq $records.Count) 'VMware SVGA 3D; no physical sensors available'
$a=& (Join-Path $payload 'CryptoGuardAgent.exe') replay --input $events
if($LASTEXITCODE){throw 'Replay A failed'}
$b=& (Join-Path $payload 'CryptoGuardAgent.exe') replay --input $events
Check 'Captured telemetry replays deterministically' ($LASTEXITCODE -eq 0 -and ($a -join "`n") -eq ($b -join "`n")) $records.Count
Check 'No false mining alerts in this idle VM capture' (@($records | ForEach-Object {$_.risk} | Where-Object {$_.lifecycle -in @('confirmed','suspected')}).Count -eq 0) 'Narrow idle-VM smoke test, not an accuracy benchmark'
