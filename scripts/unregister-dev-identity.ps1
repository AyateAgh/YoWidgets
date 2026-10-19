$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$journal=Join-Path $root '.tools/dev-identity.json'
if (-not (Test-Path -LiteralPath $journal)) { Write-Output 'No owned development registration.'; exit 0 }
$m=Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json
if ($m.Product -ne 'YoWidgets.Native.v1') { throw 'Unknown journal owner' }
$package=Get-AppxPackage -Name 'YoWidgets.Native' | Where-Object { $_.PackageFullName -eq $m.PackageFullName }
if ($package) { Remove-AppxPackage -Package $package.PackageFullName }
if (Get-AppxPackage -Name 'YoWidgets.Native' | Where-Object { $_.PackageFullName -eq $m.PackageFullName }) { throw 'Registration remains' }
Remove-Item -LiteralPath $journal
Write-Output 'Owned development identity removed. Build files and source remain.'
