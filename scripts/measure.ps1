param([Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$ProcessId,[ValidateRange(10,86400)][int]$Seconds=300,[string]$OutputPath)
$ErrorActionPreference='Stop'
$start=Get-Process -Id $ProcessId
$startCpu=$start.TotalProcessorTime.TotalSeconds
$started=Get-Date
$samples=@()
for($i=0;$i -lt [Math]::Ceiling($Seconds/10);$i++) {
 Start-Sleep -Seconds ([Math]::Min(10,$Seconds))
 $process=Get-Process -Id $ProcessId -ErrorAction Stop
 $samples+=[pscustomobject]@{At=(Get-Date).ToString('o');CpuSeconds=$process.TotalProcessorTime.TotalSeconds;WorkingSetMB=[Math]::Round($process.WorkingSet64/1MB,1);PrivateMB=[Math]::Round($process.PrivateMemorySize64/1MB,1);Handles=$process.HandleCount}
}
$elapsed=((Get-Date)-$started).TotalSeconds
$result=[ordered]@{ProcessId=$ProcessId;ElapsedSeconds=[Math]::Round($elapsed,1);LogicalProcessors=[Environment]::ProcessorCount;AverageCpuPercent=[Math]::Round((($process.TotalProcessorTime.TotalSeconds-$startCpu)/$elapsed/[Environment]::ProcessorCount*100),3);WorkingSetMB=[Math]::Round($process.WorkingSet64/1MB,1);PrivateMB=[Math]::Round($process.PrivateMemorySize64/1MB,1);Samples=$samples}
$json=$result | ConvertTo-Json -Depth 4
if($OutputPath) { $json | Set-Content -LiteralPath $OutputPath -Encoding UTF8 }
$json
