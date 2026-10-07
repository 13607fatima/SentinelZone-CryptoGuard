param([Parameter(Mandatory)][string]$TestDirectory,[string]$SourceDirectory,[switch]$ExistingInstall)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$root=[IO.Path]::GetFullPath($TestDirectory)
$app=Join-Path $env:ProgramFiles 'SentinelZone\CryptoGuard'
$data=Join-Path $env:ProgramData 'SentinelZone\CryptoGuard'
$source=if($SourceDirectory){[IO.Path]::GetFullPath($SourceDirectory)}else{Join-Path $root 'source'}
$setup=Join-Path $source 'artifacts\installer\SentinelZone-CryptoGuard-Setup-0.22.2-rc.1-win-x64.exe'
$exe=Join-Path $app 'versions\0.22.2-rc.1\CryptoGuardAgent.exe'
$results=New-Object Collections.Generic.List[object]
function Record([string]$Name,[bool]$Passed,$Detail){
    $results.Add([pscustomobject]@{name=$Name;passed=$Passed;detail=$Detail})
    $results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'lifecycle-results.json') -Encoding utf8
    Write-Output ((@{name=$Name;passed=$Passed;detail=$Detail}|ConvertTo-Json -Compress -Depth 5))
    if(-not $Passed){throw ('Lifecycle check failed: '+$Name)}
}
function Launch([string]$File,[string[]]$Arguments){
    $p=Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -Wait -PassThru
    $code=$p.ExitCode;$p.Dispose();return $code
}
function Wait-Health([string]$Version,[datetime]$After){
    $deadline=[datetime]::UtcNow.AddSeconds(70)
    do{
        if(Test-Path (Join-Path $data 'health.json')){
            $h=Get-Content (Join-Path $data 'health.json') -Raw | ConvertFrom-Json
            if($h.agent_version -eq $Version -and [datetimeoffset]::Parse($h.observed_at).UtcDateTime -gt $After -and (Get-Service SentinelZoneCryptoGuard).Status -eq 'Running'){return $h}
        }
        Start-Sleep -Milliseconds 500
    }while([datetime]::UtcNow -lt $deadline)
    throw 'Fresh health timeout'
}
$identityBefore=Get-Content (Join-Path $data 'identity.json') -Raw | ConvertFrom-Json
$oldTime=[datetime]::UtcNow
if(-not $ExistingInstall){
    $code=Launch $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $root 'rc1-upgrade.log')+'"'))
    Record 'Installer upgrade exit code' ($code -eq 0) $code
}
$health=Wait-Health '0.22.2-rc.1' $oldTime
$previousSequence=if($identityBefore.PSObject.Properties.Name -contains 'next_sequence'){$identityBefore.next_sequence}else{$identityBefore.sequence}
Record 'Service preserves identity and sequence' ($health.agent_id -eq $identityBefore.agent_id -and $health.sequence -ge $previousSequence) @{version=$health.agent_version;sequence=$health.sequence}
$settings=Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\SentinelZoneCryptoGuard'
Record 'Quoted service path and LocalService identity' ($settings.ImagePath.StartsWith('"'+$exe+'"') -and $settings.ObjectName -eq 'NT AUTHORITY\LocalService') @{image=$settings.ImagePath;account=$settings.ObjectName}
Record 'Automatic delayed startup' ($settings.Start -eq 2 -and $settings.DelayedAutoStart -eq 1) 'Registry start=2, delayed=1'
$acl=Get-Acl -LiteralPath $data
$sids=@($acl.Access | ForEach-Object {$_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value})
Record 'Private ProgramData ACL' ($acl.AreAccessRulesProtected -and $sids.Count -eq 3 -and 'S-1-5-19' -in $sids -and 'S-1-5-18' -in $sids -and 'S-1-5-32-544' -in $sids) $sids
$export=Join-Path $root 'service-export.jsonl'
& $exe export --data $data --out $export
Record 'Export while service is running' ($LASTEXITCODE -eq 0 -and (Get-Item $export).Length -gt 0 -and (Get-Service SentinelZoneCryptoGuard).Status -eq 'Running') (Get-Item $export).Length
$pidBefore=(Get-CimInstance Win32_Service -Filter "Name='SentinelZoneCryptoGuard'").ProcessId
$code=Launch $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $root 'same-version.log')+'"'))
$pidAfter=(Get-CimInstance Win32_Service -Filter "Name='SentinelZoneCryptoGuard'").ProcessId
Record 'Same-version install safely rejected without stopping service' ($code -ne 0 -and $pidBefore -eq $pidAfter) @{exit=$code;pidUnchanged=($pidBefore -eq $pidAfter)}
$time=[datetime]::UtcNow
Restart-Service SentinelZoneCryptoGuard
$health=Wait-Health '0.22.2-rc.1' $time
Record 'Service restart retains identity' ($health.agent_id -eq $identityBefore.agent_id) $health.agent_version

# Test an invalid candidate only inside the product directory.
$bad=Join-Path $app 'versions\test-invalid-candidate'
New-Item -ItemType Directory -Force $bad | Out-Null
$badExe=Join-Path $bad 'CryptoGuardAgent.exe'
Copy-Item (Join-Path $env:SystemRoot 'System32\cmd.exe') $badExe
$beforeSettings=Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\SentinelZoneCryptoGuard'
$beforeRun=(Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run').SentinelZoneCryptoGuardIdle
$code=Launch (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',('"'+(Join-Path $app 'Manage-Service.ps1')+'"'),'-Action','Install','-BinaryPath',('"'+$badExe+'"'))
$afterSettings=Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\SentinelZoneCryptoGuard'
$afterRun=(Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run').SentinelZoneCryptoGuardIdle
Record 'Failed upgrade restores running previous service and helper' ($code -ne 0 -and $afterSettings.ImagePath -eq $beforeSettings.ImagePath -and (@($afterSettings.Environment) -join '|') -eq (@($beforeSettings.Environment) -join '|') -and $afterRun -eq $beforeRun -and (Get-Service SentinelZoneCryptoGuard).Status -eq 'Running') @{exit=$code;previousPathRestored=($afterSettings.ImagePath -eq $beforeSettings.ImagePath)}
if([IO.Path]::GetFullPath($bad) -ne [IO.Path]::GetFullPath((Join-Path $app 'versions\test-invalid-candidate'))){throw 'Invalid test cleanup boundary'}
Remove-Item -LiteralPath $badExe
Remove-Item -LiteralPath $bad

$healthBefore=Get-Content (Join-Path $data 'health.json') -Raw | ConvertFrom-Json
$samples=New-Object Collections.Generic.List[object]
$cpuPrevious=@{};$stamp=[Diagnostics.Stopwatch]::StartNew();$previousTime=$stamp.Elapsed.TotalSeconds
$logical=(Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
for($i=0;$i -lt 37;$i++){
    $cpu=0.0;$memory=0L;$alive=@{}
    foreach($p in Get-Process CryptoGuardAgent,CryptoGuardHardwareHost -ErrorAction SilentlyContinue){
        if($p.Path -notlike ($app+'\*')){continue}
        $key=$p.Id.ToString()+':'+$p.StartTime.Ticks.ToString();$alive[$key]=$p.TotalProcessorTime.TotalSeconds
        if($cpuPrevious.ContainsKey($key)){$cpu+=[math]::Max(0,$p.TotalProcessorTime.TotalSeconds-$cpuPrevious[$key])}else{$cpu+=$p.TotalProcessorTime.TotalSeconds}
        $memory+=$p.WorkingSet64
    }
    $time=$stamp.Elapsed.TotalSeconds
    if($i -gt 0){$samples.Add([pscustomobject]@{elapsed=$time;cpuPercentHost=100*$cpu/($time-$previousTime)/$logical;memoryMiB=$memory/1MB})}
    $cpuPrevious=$alive;$previousTime=$time
    if($i -lt 36){Start-Sleep -Seconds 5}
}
$samples | ConvertTo-Json | Set-Content (Join-Path $root 'resource-samples.json') -Encoding utf8
$sorted=@($samples.cpuPercentHost | Sort-Object)
Record 'Three-minute service resource measurement completed' ($samples.Count -eq 36 -and (Get-Service SentinelZoneCryptoGuard).Status -eq 'Running') @{meanCpu=($samples.cpuPercentHost|Measure-Object -Average).Average;p95Cpu=$sorted[[int][math]::Ceiling($sorted.Count*.95)-1];maxMemoryMiB=($samples.memoryMiB|Measure-Object -Maximum).Maximum;seconds=$stamp.Elapsed.TotalSeconds;note='Sampled live children; very short children may fall between samples.'}

$healthAfter=Get-Content (Join-Path $data 'health.json') -Raw | ConvertFrom-Json
@{durationSeconds=$stamp.Elapsed.TotalSeconds;meanCpu=($samples.cpuPercentHost|Measure-Object -Average).Average;p95Cpu=$sorted[[int][math]::Ceiling($sorted.Count*.95)-1];maxMemoryMiB=($samples.memoryMiB|Measure-Object -Maximum).Maximum;droppedEventsDelta=($healthAfter.health.dropped_events-$healthBefore.health.dropped_events);spoolGrowthBytes=($healthAfter.health.spool_bytes-$healthBefore.health.spool_bytes);collectorErrors=$healthAfter.health.collector_errors;scope='Installed main service and live helpers, sampled every five seconds; very short children may be missed';soak72h='UNVERIFIED'} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'windows-resource.json') -Encoding UTF8
$prior=Get-Content (Join-Path $data 'identity.json') -Raw | ConvertFrom-Json
$recordsBefore=@(Get-ChildItem (Join-Path $data 'spool') -Filter '*.event').Count
$code=Launch (Join-Path $app 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $root 'uninstall.log')+'"'))
$after=Get-Content (Join-Path $data 'identity.json') -Raw | ConvertFrom-Json
Record 'Uninstall removes service and preserves local identity and records' ($code -eq 0 -and -not (Get-Service SentinelZoneCryptoGuard -ErrorAction SilentlyContinue) -and $after.agent_id -eq $prior.agent_id -and @(Get-ChildItem (Join-Path $data 'spool') -Filter '*.event').Count -ge $recordsBefore) @{exit=$code;retainedRecords=@(Get-ChildItem (Join-Path $data 'spool') -Filter '*.event').Count}
$time=[datetime]::UtcNow
$code=Launch $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $root 'reinstall.log')+'"'))
$health=Wait-Health '0.22.2-rc.1' $time
Record 'Fresh service installation from retained state' ($code -eq 0 -and $health.agent_id -eq $prior.agent_id) @{exit=$code;version=$health.agent_version}
Write-Output 'Lifecycle suite completed; corrected service is left running.'
