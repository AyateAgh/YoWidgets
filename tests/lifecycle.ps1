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
Write-Output 'RESULT 4/4 lifecycle cases passed'
