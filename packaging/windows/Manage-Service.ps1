param([Parameter(Mandatory)][ValidateSet('Stop','Install','Uninstall')][string]$Action,[string]$BinaryPath)
$ErrorActionPreference='Stop'
$serviceName='SentinelZoneCryptoGuard'
$dataPath=Join-Path $env:ProgramData 'SentinelZone\CryptoGuard'
$runKey='HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
function Invoke-Sc([string[]]$Arguments) {
    # Windows PowerShell 5.1 strips embedded quotes with native array splatting.
    # Construct a Windows argv command line explicitly so service paths stay quoted.
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=Join-Path $env:SystemRoot 'System32\sc.exe'
    $info.WorkingDirectory=$env:SystemRoot
    $info.UseShellExecute=$false
    $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true
    $info.RedirectStandardError=$true
    $info.Arguments=($Arguments | ForEach-Object {'"'+($_ -replace '(\\*)"','$1$1\"' -replace '(\\+)$','$1$1')+'"'}) -join ' '
    $process=[Diagnostics.Process]::Start($info)
    try{
        $output=$process.StandardOutput.ReadToEndAsync()
        $errorOutput=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(30000)){$process.Kill();throw 'Service command timed out'}
        $null=$output.GetAwaiter().GetResult();$null=$errorOutput.GetAwaiter().GetResult()
        if($process.ExitCode -ne 0){throw ('Service configuration failed ('+$process.ExitCode+')')}
    }finally{$process.Dispose()}
}
function Stop-Agent {
    $service=Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if($service -and $service.Status -ne 'Stopped'){
        $service.Stop();$service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30));$service.Dispose()
    }
}
function Stop-InstalledIdleHelpers {
    $nativeRoot=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetEnvironmentVariable('ProgramW6432')) 'SentinelZone\CryptoGuard\versions'))+'\'
    foreach($helper in Get-CimInstance Win32_Process -Filter "Name='CryptoGuardAgent.exe'"){
        if(-not $helper.ExecutablePath -or -not $helper.ExecutablePath.StartsWith($nativeRoot,[StringComparison]::OrdinalIgnoreCase) -or
           $helper.CommandLine -notmatch '(?i)\s+idle-helper\s+--watch(?:\s|$)'){continue}
        $process=Get-Process -Id $helper.ProcessId -ErrorAction SilentlyContinue
        if(-not $process){continue}
        try{
            # Recheck the image and creation time before stopping this exact helper.
            if($process.Path -eq $helper.ExecutablePath -and [math]::Abs(($process.StartTime.ToUniversalTime()-$helper.CreationDate.ToUniversalTime()).TotalMilliseconds) -lt 10){
                $process.Kill()
                if(-not $process.WaitForExit(5000)){throw 'Installed idle helper did not exit'}
            }
        }finally{$process.Dispose()}
    }
}
try {
    if($Action -eq 'Stop'){Stop-Agent;exit 0}
    if($Action -eq 'Uninstall'){
        Stop-Agent
        Stop-InstalledIdleHelpers
        if(Get-Service -Name $serviceName -ErrorAction SilentlyContinue){Invoke-Sc -Arguments @('delete',$serviceName)}
        Remove-ItemProperty -LiteralPath $runKey -Name 'SentinelZoneCryptoGuardIdle' -ErrorAction SilentlyContinue
        # State is deliberately retained. The uninstaller states this explicitly.
        exit 0
    }
    $BinaryPath=[IO.Path]::GetFullPath($BinaryPath)
    $nativeProgramFiles=[Environment]::GetEnvironmentVariable('ProgramW6432')
    if(-not $nativeProgramFiles){throw 'Native x64 Program Files path is unavailable'}
    $programRoot=[IO.Path]::GetFullPath((Join-Path $nativeProgramFiles 'SentinelZone\CryptoGuard'))+[IO.Path]::DirectorySeparatorChar
    if(-not $BinaryPath.StartsWith($programRoot,[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $BinaryPath)){throw 'Invalid installed binary location'}
    New-Item -ItemType Directory -Force -Path $dataPath | Out-Null
    $acl=New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true,$false)
    foreach($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@('S-1-5-19','Modify'))){
        $sid=New-Object System.Security.Principal.SecurityIdentifier($entry[0])
        $rule=New-Object System.Security.AccessControl.FileSystemAccessRule($sid,$entry[1],'ContainerInherit,ObjectInherit','None','Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $dataPath -AclObject $acl
    $service=Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    $previous=$null
    $serviceKey="HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"
    $previousEnvironment=$null
    $wasRunning=$service -and $service.Status -eq 'Running'
    if($service){
        $previousSettings=Get-ItemProperty -LiteralPath $serviceKey
        if($previousSettings.ObjectName -ne 'NT AUTHORITY\LocalService'){throw 'Unexpected existing service identity'}
        $previous=$previousSettings.ImagePath
        $previousEnvironment=$previousSettings.Environment
    }
    $previousRun=(Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).SentinelZoneCryptoGuardIdle
    $expectedVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($BinaryPath).ProductVersion.Split('+')[0]
    $modified=$false
    $image='"'+$BinaryPath+'" service --data "'+$dataPath+'"'
    $health=Join-Path $dataPath 'health.json'
    $rollbackRoot=Join-Path (Split-Path -Parent $dataPath) 'CryptoGuard-rollback'
    $backupPath=$null
    try {
        Stop-Agent
        Stop-InstalledIdleHelpers
        if($previous){
            # Snapshot state before a schema migration. Retain it after success for explicit rollback.
            $backupPath=Join-Path $rollbackRoot ('before-'+[Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $backupPath | Out-Null
            Set-Acl -LiteralPath $rollbackRoot -AclObject $acl
            Set-Acl -LiteralPath $backupPath -AclObject $acl
            foreach($item in Get-ChildItem -LiteralPath $dataPath -Force){
                if($item.Name -in @('bundle','agent.lock','writer.lock') -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){continue}
                Copy-Item -LiteralPath $item.FullName -Destination $backupPath -Recurse -Force
            }
            @{previous_binary=$previous;state_snapshot=$backupPath;created_at=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupPath 'rollback-manifest.json') -Encoding UTF8
        }
        if($service){Invoke-Sc -Arguments @('config',$serviceName,'binPath=',$image)}
        else{Invoke-Sc -Arguments @('create',$serviceName,'binPath=',$image,'start=','delayed-auto','obj=','NT AUTHORITY\LocalService','DisplayName=','SentinelZone CryptoGuard')}
        $modified=$true
        $extractPath=Join-Path $dataPath 'bundle'
        New-Item -ItemType Directory -Force -Path $extractPath | Out-Null
        $environment=@($previousEnvironment | Where-Object {$_ -and $_ -notlike 'DOTNET_BUNDLE_EXTRACT_BASE_DIR=*'})+@('DOTNET_BUNDLE_EXTRACT_BASE_DIR='+$extractPath)
        New-ItemProperty -LiteralPath $serviceKey -Name Environment -PropertyType MultiString -Value $environment -Force | Out-Null
        if(-not $service){
            Invoke-Sc -Arguments @('failure',$serviceName,'reset=','86400','actions=','restart/5000/restart/15000/restart/60000')
            Invoke-Sc -Arguments @('failureflag',$serviceName,'1')
        }
        $started=[DateTime]::UtcNow
        Start-Service -Name $serviceName
        $deadline=[DateTime]::UtcNow.AddSeconds(60)
        $healthy=$false
        do{
            Start-Sleep -Milliseconds 500
            if((Test-Path -LiteralPath $health) -and (Get-Item -LiteralPath $health).LastWriteTimeUtc -gt $started){
                $report=Get-Content -LiteralPath $health -Raw | ConvertFrom-Json
                $memory=if($report.schema_version -eq '1.2.0'){$report.snapshot.host.memory_total_bytes.value}else{$report.data.host.total_memory_bytes}
                $errors=if($report.schema_version -eq '1.2.0'){$report.health.collector_errors}else{$report.data.health.collector_errors}
                if($report.schema_version -in @('1.0','1.1','1.2.0') -and $report.agent_version -eq $expectedVersion -and
                   $memory -gt 0 -and $errors -notcontains 'risk_outbox_pending; detection_paused' -and
                   (Get-Service $serviceName).Status -eq 'Running'){$healthy=$true;break}
            }
        }while([DateTime]::UtcNow -lt $deadline)
        if(-not $healthy){throw 'Fresh service health check failed'}
        $helper=Join-Path (Split-Path -Parent $BinaryPath) 'Start-IdleHelper.ps1'
        $startup='"'+$env:SystemRoot+'\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -WindowStyle Hidden -File "'+$helper+'"'
        New-ItemProperty -LiteralPath $runKey -Name 'SentinelZoneCryptoGuardIdle' -PropertyType String -Value $startup -Force | Out-Null
    }catch{
        $failure=$_
        if($modified){
            Stop-Agent
            if($backupPath){
                # Preserve the failed attempt too; never discard telemetry produced during health validation.
                $failedPath=[IO.Path]::GetFullPath((Join-Path $rollbackRoot ('failed-'+[Guid]::NewGuid().ToString('N'))))
                $expectedRoot=[IO.Path]::GetFullPath($rollbackRoot)+[IO.Path]::DirectorySeparatorChar
                if(-not $failedPath.StartsWith($expectedRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFullPath($dataPath) -ne [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'SentinelZone\CryptoGuard'))){throw 'Invalid state rollback boundary'}
                Move-Item -LiteralPath $dataPath -Destination $failedPath
                New-Item -ItemType Directory -Path $dataPath | Out-Null
                foreach($item in Get-ChildItem -LiteralPath $backupPath -Force){if($item.Name -ne 'rollback-manifest.json'){Copy-Item -LiteralPath $item.FullName -Destination $dataPath -Recurse -Force}}
                Set-Acl -LiteralPath $dataPath -AclObject $acl
            }
            if($previous){
                Invoke-Sc -Arguments @('config',$serviceName,'binPath=',$previous)
                if($null -ne $previousEnvironment){New-ItemProperty -LiteralPath $serviceKey -Name Environment -PropertyType MultiString -Value $previousEnvironment -Force | Out-Null}
                else{Remove-ItemProperty -LiteralPath $serviceKey -Name Environment -ErrorAction SilentlyContinue}
            }else{Invoke-Sc -Arguments @('delete',$serviceName)}
        }
        if($previousRun){New-ItemProperty -LiteralPath $runKey -Name 'SentinelZoneCryptoGuardIdle' -PropertyType String -Value $previousRun -Force | Out-Null}
        else{Remove-ItemProperty -LiteralPath $runKey -Name 'SentinelZoneCryptoGuardIdle' -ErrorAction SilentlyContinue}
        if($previous -and $wasRunning){Start-Service -Name $serviceName}
        throw $failure
    }
    exit 0
}catch{Write-Error ('CryptoGuard service operation failed: '+$_.Exception.GetType().Name);exit 1}
