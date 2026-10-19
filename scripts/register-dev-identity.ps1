param([Parameter(Mandatory)][string]$ExternalLocation)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$external=[IO.Path]::GetFullPath($ExternalLocation)
$allowed=[IO.Path]::GetFullPath((Join-Path $root 'src/YoWidgets/bin')).TrimEnd('\')+'\'
if (-not $external.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Dev identity is limited to this project build folder.' }
if (-not (Test-Path -LiteralPath (Join-Path $external 'YoWidgets.exe'))) { throw 'Build executable missing' }
$existing=@(Get-AppxPackage -Name 'YoWidgets.Native')
if ($existing.Count -gt 0) { throw 'Existing YoWidgets identity found. Use its paired unregister script first.' }
# Requires developer registration prerequisites already enabled by the user.
# Never enable Developer Mode, trust certificates or change execution policy here.
$assets=Join-Path $external 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'assets/Logo.png') -Destination $assets -Force
Add-AppxPackage -Register -Path (Join-Path $root 'packaging/AppxManifest.xml') -ExternalLocation $external
$package=Get-AppxPackage -Name 'YoWidgets.Native'
if (-not $package) { throw 'Registration did not produce a package identity' }
@{Product='YoWidgets.Native.v1';PackageFullName=$package.PackageFullName;ExternalLocation=$external} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root '.tools/dev-identity.json') -Encoding UTF8
Write-Output 'Identity registered. Restart the exact EXE and request notification permission in Settings.'
