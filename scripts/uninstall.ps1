param([int]$ParentId=0,[string]$TestRoot,[switch]$ShowResult)
$ErrorActionPreference='Stop'
function Invoke-OwnedUninstall {
$installRoot=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
if ($TestRoot) { $installRoot=[IO.Path]::GetFullPath($TestRoot).TrimEnd('\') }
$marker=Join-Path $installRoot 'installation.json'
if (-not (Test-Path -LiteralPath $marker)) { Write-Output 'Nothing to remove: installation manifest absent.'; return }
$m=Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
if ($m.Product -ne 'YoWidgets.Native.v1' -or [string]::IsNullOrWhiteSpace($m.Id) -or $m.InstallRoot -ne $installRoot) { throw 'Installation ownership mismatch' }
$normal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/YoWidgets')).TrimEnd('\')
if ($m.Test) {
 if (-not $TestRoot) { throw 'Test cleanup must run through the source script with -TestRoot.' }; $scriptProjectRoot=Split-Path -Parent $PSScriptRoot; $testBoundary=[IO.Path]::GetFullPath((Join-Path $scriptProjectRoot 'artifacts/lifecycle')).TrimEnd('\')+'\'
 if (-not $installRoot.StartsWith($testBoundary,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid test root' }
 if ($m.DataRoot -ne (Join-Path $installRoot 'data')) { throw 'Invalid test data root' }
} else {
 if ($installRoot -ne $normal -or $m.DataRoot -ne [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'YoWidgets'))) { throw 'Unexpected install/data location' }
}
function Assert-NoReparse([string]$path) {
 if (-not (Test-Path -LiteralPath $path)) { return }
 $entry=Get-Item -LiteralPath $path -Force
 if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing reparse path: $path" }
 if ($entry.PSIsContainer) { foreach($child in Get-ChildItem -LiteralPath $path -Force) { Assert-NoReparse $child.FullName } }
}
Assert-NoReparse $installRoot
if ($ParentId -gt 0) {
 $parent=Get-Process -Id $ParentId -ErrorAction SilentlyContinue
 if ($parent) {
  if ($parent.Path -ne (Join-Path $installRoot 'YoWidgets.exe')) { throw 'Uninstall parent ownership mismatch' }
  if (-not $parent.WaitForExit(15000)) { throw 'YoWidgets is still running. Close it before removal.' }
 }
}
$running=Get-Process YoWidgets -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $installRoot 'YoWidgets.exe') }
if ($running) { throw 'Close the installed YoWidgets before removal.' }
if ($m.PackageFullName) {
 $package=Get-AppxPackage | Where-Object { $_.PackageFullName -eq $m.PackageFullName -and $_.Name -eq 'YoWidgets.Native' }
 if ($package) { Remove-AppxPackage -Package $package.PackageFullName }
 if (Get-AppxPackage | Where-Object { $_.PackageFullName -eq $m.PackageFullName }) { throw 'Package identity remains registered' }
}
$run='HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
$runValues=Get-ItemProperty -LiteralPath $run -ErrorAction SilentlyContinue
$value=$runValues.YoWidgets
if ($value -eq ('"'+(Join-Path $installRoot 'YoWidgets.exe')+'"')) { Remove-ItemProperty -LiteralPath $run -Name YoWidgets }
if (Test-Path -LiteralPath $m.DataRoot) {
 Assert-NoReparse $m.DataRoot
 $owner=Join-Path $m.DataRoot 'owner.json'
 if (-not (Test-Path -LiteralPath $owner)) { throw 'Data ownership record missing; leaving data intact.' }
 $o=Get-Content -LiteralPath $owner -Raw | ConvertFrom-Json
 if ($o.Product -ne 'YoWidgets.Native.v1' -or $o.InstallationId -ne $m.Id) { throw 'Data belongs to another installation' }
 # Root was validated against the exact per-user location or dedicated test boundary above.
 Remove-Item -LiteralPath $m.DataRoot -Recurse -Force
}
$residuals=@()
foreach($relative in $m.Files) {
 if ([IO.Path]::GetFileName($relative) -ne $relative) { throw 'Unsafe file name in manifest' }
 $file=Join-Path $installRoot $relative
 if (Test-Path -LiteralPath $file) { try { Remove-Item -LiteralPath $file -Force } catch { $residuals+=$file } }
}
$extra=@(Get-ChildItem -LiteralPath $installRoot -Force | Where-Object { $_.Name -ne 'installation.json' })
if ($extra.Count -gt 0 -or $residuals.Count -gt 0) { throw ('Partial removal; remaining entries: '+(($extra.FullName+$residuals)-join ', ')) }
if (-not $m.Test) {
 $key='HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/YoWidgets'
 $uninstallValues=Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
 if ($uninstallValues.InstallLocation -eq $installRoot) { Remove-Item -LiteralPath $key -Recurse }
}
Remove-Item -LiteralPath $marker -Force
# Only an already verified empty root is removed, without recursive deletion.
Remove-Item -LiteralPath $installRoot
Write-Output 'YoWidgets removed. Files, data and registrations verified absent.'

}
function Show-CleanupResult([string]$message) {
 Add-Type -AssemblyName PresentationFramework
 $window=New-Object System.Windows.Window
 $window.Title='YoWidgets — удаление'
 $window.Width=560; $window.Height=260; $window.WindowStartupLocation='CenterScreen'
 $window.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#11151D')
 $panel=New-Object System.Windows.Controls.StackPanel
 $panel.Margin=24
 $text=New-Object System.Windows.Controls.TextBlock
 $text.Text=$message; $text.TextWrapping='Wrap'; $text.FontSize=15; $text.Foreground=[Windows.Media.Brushes]::White
 $panel.Children.Add($text) | Out-Null
 $button=New-Object System.Windows.Controls.Button
 $button.Content='Закрыть'; $button.Padding=10; $button.Margin='0,20,0,0'
 $button.Add_Click({ $window.Close() })
 $panel.Children.Add($button) | Out-Null
 $window.Content=$panel
 $window.ShowDialog() | Out-Null
}
try {
 Invoke-OwnedUninstall
 if ($ShowResult) { Show-CleanupResult 'YoWidgets удалён. Файлы, данные и регистрации проверены. Исходные фото и другие программы сохранены.' }
} catch {
 if ($ShowResult) { Show-CleanupResult ('Удаление не завершено: '+$_.Exception.Message); exit 1 }
 throw
}
