param([string]$TestDestination,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$source=Join-Path $projectRoot 'artifacts/release'
if (-not (Test-Path -LiteralPath (Join-Path $source 'YoWidgets.exe'))) { throw 'Run scripts/build.ps1 first.' }
$installRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/YoWidgets'))
$dataRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'YoWidgets'))
if ($TestDestination) {
 $testBoundary=[IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/lifecycle')).TrimEnd('\')+'\'
 $installRoot=[IO.Path]::GetFullPath($TestDestination)
 if (-not $installRoot.StartsWith($testBoundary,[StringComparison]::OrdinalIgnoreCase)) { throw 'Test installation must be below artifacts/lifecycle.' }
 $dataRoot=Join-Path $installRoot 'data'
}
if (Test-Path -LiteralPath $installRoot) { throw 'Installation folder already exists. Uninstall the previous owned installation first.' }
if (Test-Path -LiteralPath $dataRoot) { throw 'Data folder already exists; refusing to adopt or overwrite existing data.' }
if (-not $TestDestination -and (Test-Path -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/YoWidgets')) { throw 'Uninstall registry key already exists.' }
$parent=Split-Path -Parent $installRoot
if (Test-Path -LiteralPath $parent) { if ((Get-Item -LiteralPath $parent).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse installation parent rejected' } }
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
$manifest=[ordered]@{Product='YoWidgets.Native.v1';Id=[Guid]::NewGuid().ToString('N');InstallRoot=$installRoot;DataRoot=$dataRoot;SourceRoot=$projectRoot;Test=[bool]$TestDestination;Files=@((Get-ChildItem -LiteralPath $source -File).Name);CreatedDataRoot=$false;PackageFullName=$null;Stage='prepared'}
$marker=Join-Path $installRoot 'installation.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $marker -Encoding UTF8
foreach($item in Get-ChildItem -LiteralPath $source -File) {
 Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $installRoot $item.Name)
 $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $marker -Encoding UTF8
}
if (-not (Test-Path -LiteralPath $dataRoot)) { $manifest.CreatedDataRoot=$true; $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $marker -Encoding UTF8; New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null }
$dataOwner=Join-Path $dataRoot 'owner.json'
if (Test-Path -LiteralPath $dataOwner) { throw 'Data belongs to another installation. Nothing overwritten.' }
@{Product='YoWidgets.Native.v1';InstallationId=$manifest.Id} | ConvertTo-Json | Set-Content -LiteralPath $dataOwner -Encoding UTF8
$manifest.Stage='applied'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $marker -Encoding UTF8
if (-not $TestDestination) {
 $key='HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/YoWidgets'
 if (Test-Path -LiteralPath $key) { throw 'Uninstall registry key already exists.' }
 New-Item -Path $key -Force | Out-Null
 New-ItemProperty -LiteralPath $key -Name DisplayName -Value YoWidgets -PropertyType String | Out-Null
 New-ItemProperty -LiteralPath $key -Name DisplayVersion -Value '0.1.0' -PropertyType String | Out-Null
 New-ItemProperty -LiteralPath $key -Name InstallLocation -Value $installRoot -PropertyType String | Out-Null
 New-ItemProperty -LiteralPath $key -Name UninstallString -Value ('powershell.exe -NoProfile -File "'+(Join-Path $installRoot 'uninstall.ps1')+'"') -PropertyType String | Out-Null
}
Write-Output "Installed per-user: $installRoot"
if (-not $NoLaunch) { Start-Process -FilePath (Join-Path $installRoot 'YoWidgets.exe') -WindowStyle Hidden }
