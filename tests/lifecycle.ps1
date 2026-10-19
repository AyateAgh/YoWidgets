$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$boundary=Join-Path $root 'artifacts/lifecycle'
New-Item -ItemType Directory -Path $boundary -Force | Out-Null
$destination=Join-Path $boundary ([Guid]::NewGuid().ToString('N'))
& (Join-Path $root 'scripts/install.ps1') -TestDestination $destination -NoLaunch
if (-not (Test-Path -LiteralPath (Join-Path $destination 'YoWidgets.exe'))) { throw 'Installed exe missing' }
$foreign=Join-Path $destination 'my-own-file.txt'
Set-Content -LiteralPath $foreign -Value 'preserve this file'
$refused=$false
try { & (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $destination } catch { $refused=$true }
if (-not $refused -or -not (Test-Path -LiteralPath $foreign)) { throw 'Foreign file protection failed' }
if (-not (Test-Path -LiteralPath (Join-Path $destination 'uninstall.ps1'))) { throw 'Partial uninstall destroyed its own retry entry point' }
Write-Output 'PASS foreign file preserved and partial removal reported'
Remove-Item -LiteralPath $foreign
& (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $destination
if (Test-Path -LiteralPath $destination) { throw 'Install root remains' }
Write-Output 'PASS complete removal after partial failure'
& (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $destination
Write-Output 'PASS repeated removal is safe'
$junctionRoot=Join-Path $boundary ([Guid]::NewGuid().ToString('N'))
& (Join-Path $root 'scripts/install.ps1') -TestDestination $junctionRoot -NoLaunch
$target=Join-Path $boundary ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $target | Out-Null
$sentinel=Join-Path $target 'sentinel.txt'
Set-Content -LiteralPath $sentinel -Value 'untouched'
$link=Join-Path $junctionRoot 'foreign-junction'
New-Item -ItemType Junction -Path $link -Target $target | Out-Null
$refused=$false
try { & (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $junctionRoot } catch { $refused=$true }
if (-not $refused -or -not (Test-Path -LiteralPath $sentinel)) { throw 'Junction protection failed' }
Write-Output 'PASS junction refused without traversing target'
[IO.Directory]::Delete($link)
& (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $junctionRoot
# Only this test-created, checked sentinel and its now-empty directory are removed.
if (-not ([IO.Path]::GetFullPath($target).StartsWith([IO.Path]::GetFullPath($boundary)+'\',[StringComparison]::OrdinalIgnoreCase))) { throw 'Test boundary failure' }
Remove-Item -LiteralPath $sentinel
Remove-Item -LiteralPath $target
$dataCase=Join-Path $boundary ([Guid]::NewGuid().ToString('N'))
& (Join-Path $root 'scripts/install.ps1') -TestDestination $dataCase -NoLaunch
$dataForeign=Join-Path $dataCase 'data/my-own-note.txt'
Set-Content -LiteralPath $dataForeign -Value 'must survive'
$refused=$false
try { & (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $dataCase } catch { $refused=$true }
if (-not $refused -or -not (Test-Path -LiteralPath $dataForeign) -or -not (Test-Path -LiteralPath (Join-Path $dataCase 'uninstall.ps1'))) { throw 'Foreign data protection or retry mechanism failed' }
Remove-Item -LiteralPath $dataForeign
& (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $dataCase
Write-Output 'PASS unknown data preserved and installed retry entry point retained'
$ancestorTarget=Join-Path $boundary ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $ancestorTarget | Out-Null
$ancestorLink=Join-Path $boundary ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Junction -Path $ancestorLink -Target $ancestorTarget | Out-Null
$refused=$false
try { & (Join-Path $root 'scripts/install.ps1') -TestDestination (Join-Path $ancestorLink 'ordinary/install') -NoLaunch } catch { $refused=$true }
if (-not $refused -or (Test-Path -LiteralPath (Join-Path $ancestorTarget 'ordinary'))) { throw 'Ancestor junction containment bypass' }
[IO.Directory]::Delete($ancestorLink)
Remove-Item -LiteralPath $ancestorTarget
Write-Output 'PASS junction in ancestor chain rejected before mutation'
foreach($lockedName in @('installation.json','uninstall.ps1')) {
 $lockedCase=Join-Path $boundary ([Guid]::NewGuid().ToString('N'))
 & (Join-Path $root 'scripts/install.ps1') -TestDestination $lockedCase -NoLaunch
 $held=[IO.File]::Open((Join-Path $lockedCase $lockedName),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
 try {
  $refused=$false
  try { & (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $lockedCase } catch { $refused=$true }
  if (-not $refused -or -not (Test-Path -LiteralPath (Join-Path $lockedCase 'uninstall.ps1')) -or -not (Test-Path -LiteralPath (Join-Path $lockedCase 'installation.json'))) { throw "Terminal failure destroyed retry mechanism: $lockedName" }
 } finally { $held.Dispose() }
 & (Join-Path $root 'scripts/uninstall.ps1') -TestRoot $lockedCase
 if (Test-Path -LiteralPath $lockedCase) { throw 'Locked file retry did not finish' }
 Write-Output "PASS retry survives terminal lock on $lockedName"
}
Write-Output 'RESULT 8/8 lifecycle cases passed'
