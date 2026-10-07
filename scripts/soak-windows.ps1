param([double]$Hours=72,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if($Hours -le 0){throw 'Positive duration required'}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Use a new output directory'}
New-Item -ItemType Directory -Path $out | Out-Null
$data=Join-Path $env:ProgramData 'SentinelZone\CryptoGuard'
$app=[IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'SentinelZone\CryptoGuard'))+'\'
$logical=[int](Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
$watch=[Diagnostics.Stopwatch]::StartNew();$previous=@{};$last=0.0;$rows=New-Object Collections.Generic.List[object];$restarts=0;$lastServicePid=0;$crashes=0
try{
    while($watch.Elapsed.TotalHours -lt $Hours){
        $service=Get-CimInstance Win32_Service -Filter "Name='SentinelZoneCryptoGuard'"
        if($service.State -ne 'Running'){$crashes++}
        if($lastServicePid -ne 0 -and $service.ProcessId -ne $lastServicePid){$restarts++};$lastServicePid=$service.ProcessId
        $cpu=0.0;$rss=0L;$current=@{}
        foreach($process in Get-Process CryptoGuardAgent,CryptoGuardHardwareHost -ErrorAction SilentlyContinue){
            if(-not $process.Path -or -not $process.Path.StartsWith($app,[StringComparison]::OrdinalIgnoreCase)){continue}
            $key=$process.Id.ToString()+':'+$process.StartTime.Ticks.ToString();$value=$process.TotalProcessorTime.TotalSeconds;$current[$key]=$value
            if($previous.ContainsKey($key)){$cpu+=[math]::Max(0,$value-$previous[$key])}
            $rss+=$process.WorkingSet64
        }
        $now=$watch.Elapsed.TotalSeconds
        if($last -gt 0){
            $health=Get-Content -LiteralPath (Join-Path $data 'health.json') -Raw | ConvertFrom-Json
            $row=[pscustomobject]@{elapsed_seconds=$now;cpu_percent_host_capacity=100*$cpu/($now-$last)/$logical;rss_bytes=$rss;spool_bytes=$health.health.spool_bytes;dropped_events=$health.health.dropped_events;collector_errors=@($health.health.collector_errors).Count}
            $rows.Add($row);$row | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $out 'samples.jsonl') -Encoding UTF8
        }
        $previous=$current;$last=$now;Start-Sleep -Seconds 5
    }
}finally{
    $cpu=@($rows.cpu_percent_host_capacity | Sort-Object);$elapsedHours=$watch.Elapsed.TotalHours
    $report=@{requested_hours=$Hours;elapsed_hours=$elapsedHours;samples=$rows.Count;crashes_observed=$crashes;service_restarts=$restarts;soak72h=if($elapsedHours -ge 72 -and $crashes -eq 0){'MEASURED_REVIEW_REQUIRED'}else{'UNVERIFIED'};reference_host_verified=$false;
        cpu_mean=if($cpu.Count){($cpu|Measure-Object -Average).Average}else{$null};cpu_p95=if($cpu.Count){$cpu[[int][math]::Ceiling($cpu.Count*.95)-1]}else{$null};rss_peak_bytes=($rows.rss_bytes|Measure-Object -Maximum).Maximum;
        rss_growth_bytes=if($rows.Count){$rows[$rows.Count-1].rss_bytes-$rows[0].rss_bytes}else{$null};
        note='Live process sampling includes main service and observed helpers; short-lived children may be missed. Reference targets are not automatically certified.'}
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'report.json') -Encoding UTF8
}
