param([int]$ParentId=0,[string]$TestRoot,[switch]$ShowResult)
$ErrorActionPreference='Stop'
function Assert-SafeAncestors([string]$path) {
 $cursor=[IO.Path]::GetFullPath($path)
 while ($cursor) {
  if (Test-Path -LiteralPath $cursor) { if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse ancestor rejected: $cursor" } }
  $parent=[IO.Path]::GetDirectoryName($cursor)
  if ($parent -eq $cursor) { break }
  $cursor=$parent
 }
}
function Invoke-OwnedUninstall {
$installRoot=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
if ($TestRoot) { $installRoot=[IO.Path]::GetFullPath($TestRoot).TrimEnd('\') }
Assert-SafeAncestors $installRoot
$marker=Join-Path $installRoot 'installation.json'
if (-not (Test-Path -LiteralPath $marker)) { Write-Output 'Nothing to remove: installation manifest absent.'; return }
$manifestText=Get-Content -LiteralPath $marker -Raw
$m=$manifestText | ConvertFrom-Json
$retryScript=Join-Path $installRoot 'uninstall.ps1'
$retryText=if (Test-Path -LiteralPath $retryScript) { Get-Content -LiteralPath $retryScript -Raw } else { Get-Content -LiteralPath $PSCommandPath -Raw }
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
Assert-SafeAncestors $m.DataRoot
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
# A portable engine may share the installed data root. Do not remove its live
# content merely because its executable has a different path.
$dataHasher=[Security.Cryptography.SHA256]::Create()
try{$dataHash=([BitConverter]::ToString($dataHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes(([IO.Path]::GetFullPath($m.DataRoot)).ToUpperInvariant())))).Replace('-','').Substring(0,20)}finally{$dataHasher.Dispose()}
$dataMutex=$null
try{$dataMutex=[Threading.Mutex]::OpenExisting('Local\YoWidgets-'+$dataHash)}catch [Threading.WaitHandleCannotBeOpenedException]{}
if($dataMutex){$dataMutex.Dispose();throw 'YoWidgets is using this data. Close all copies before removal.'}
if(Test-Path -LiteralPath (Join-Path $m.DataRoot 'focus-session.json')){throw 'Focus has unfinished system restoration. Reconnect audio devices and restart YoWidgets before removal.'}
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
 $bundleJournal=Join-Path $m.DataRoot 'bundle-cache.json'
 if (Test-Path -LiteralPath $bundleJournal) {
  $bundleBoundary=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) '.net/YoWidgets')).TrimEnd('\')
  $bundleEntries=@(Get-Content -LiteralPath $bundleJournal -Raw | ConvertFrom-Json)
  foreach($entry in $bundleEntries) {
   $bundleRoot=[IO.Path]::GetFullPath($entry.Path).TrimEnd('\')
   if ([IO.Path]::GetDirectoryName($bundleRoot) -ne $bundleBoundary -or [IO.Path]::GetFileName($bundleRoot) -notmatch '^[a-zA-Z0-9_.+-]{5,128}$') { throw 'Invalid bundle cache root' }
   Assert-SafeAncestors $bundleRoot
   Assert-NoReparse $bundleRoot
   foreach($relative in $entry.Files) {
    $candidate=[IO.Path]::GetFullPath((Join-Path $bundleRoot $relative))
    if ([IO.Path]::IsPathRooted($relative) -or -not $candidate.StartsWith($bundleRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid bundle cache member' }
   }
  }
  foreach($entry in $bundleEntries) {
   $bundleRoot=[IO.Path]::GetFullPath($entry.Path).TrimEnd('\')
   if (-not (Test-Path -LiteralPath $bundleRoot)) { continue }
   foreach($relative in $entry.Files) { $candidate=Join-Path $bundleRoot $relative; if(Test-Path -LiteralPath $candidate -PathType Leaf) { Remove-Item -LiteralPath $candidate -Force } }
   Get-ChildItem -LiteralPath $bundleRoot -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { if (@(Get-ChildItem -LiteralPath $_.FullName -Force).Count -eq 0) { Remove-Item -LiteralPath $_.FullName } }
   if (@(Get-ChildItem -LiteralPath $bundleRoot -Force).Count -gt 0) { throw 'Unknown/locked bundle cache data preserved; retry cleanup after resolving it.' }
   Remove-Item -LiteralPath $bundleRoot
  }
  Remove-Item -LiteralPath $bundleJournal -Force
 }
 # New assistant state is owned only when its exact root carries our marker.
 foreach($name in @('Assistant','assistant-cache','browser-profile','integrations','Cache')) {
  $ownedRoot=[IO.Path]::GetFullPath((Join-Path $m.DataRoot $name)).TrimEnd('\')
  if (-not $ownedRoot.StartsWith([IO.Path]::GetFullPath($m.DataRoot).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid assistant data location' }
  if (-not (Test-Path -LiteralPath $ownedRoot)) { continue }
  Assert-NoReparse $ownedRoot
  $ownership=Join-Path $ownedRoot '.yowidgets-owned.json'
  if (-not (Test-Path -LiteralPath $ownership -PathType Leaf)) { throw ('Unrecognized assistant data preserved: '+$ownedRoot) }
  Assert-NoReparse $ownership
  $record=Get-Content -LiteralPath $ownership -Raw | ConvertFrom-Json
  if ($record.Product -ne 'YoWidgets.Native.v1' -or $record.Path -ne $ownedRoot) { throw 'Assistant ownership mismatch; data preserved.' }
  $pending=[Collections.Generic.Queue[string]]::new();$pending.Enqueue($ownedRoot);$folders=[Collections.Generic.List[string]]::new();$members=[Collections.Generic.List[string]]::new()
  while($pending.Count -gt 0) {
   $directory=$pending.Dequeue();$folders.Add($directory)
   foreach($entry in Get-ChildItem -LiteralPath $directory -Force) {
    if (-not $entry.FullName.StartsWith($ownedRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Assistant member escaped its owned root.' }
    Assert-NoReparse $entry.FullName
    if($entry.PSIsContainer) { $pending.Enqueue($entry.FullName) } else { $members.Add($entry.FullName) }
   }
  }
  foreach($member in $members) { if($member -ne $ownership) {Remove-Item -LiteralPath $member -Force} }
  foreach($directory in ($folders | Where-Object {$_ -ne $ownedRoot} | Sort-Object Length -Descending)) {Remove-Item -LiteralPath $directory}
  Remove-Item -LiteralPath $ownership -Force
  Remove-Item -LiteralPath $ownedRoot
 }
 # Content predates a separate root marker. Its exact declarative layout is
 # validated in full before deleting any member; unexpected files are preserved.
 $contentRoot=[IO.Path]::GetFullPath((Join-Path $m.DataRoot 'Content')).TrimEnd('\')
 if(Test-Path -LiteralPath $contentRoot) {
  if(-not $contentRoot.StartsWith([IO.Path]::GetFullPath($m.DataRoot).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid content root'}
  Assert-NoReparse $contentRoot
  foreach($entry in Get-ChildItem -LiteralPath $contentRoot -Force){
   if($entry.PSIsContainer){if($entry.Name -notin @('packages','staging')){throw 'Unknown content directory preserved.'}}
   elseif($entry.Name -notin @('installed.json','installed.json.bak','installed.json.tmp')){throw 'Unknown content file preserved.'}
  }
  $members=@(Get-ChildItem -LiteralPath $contentRoot -Recurse -File -Force)
  foreach($entry in $members){
   if(-not $entry.FullName.StartsWith($contentRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Content member escaped its root'}
   $relative=$entry.FullName.Substring($contentRoot.Length+1).Replace('\','/')
   if($relative -match '^installed\.json(\.bak|\.tmp)?$'){continue}
   $package=$relative -match '^packages/[a-z0-9][a-z0-9.-]{0,79}/[0-9]+(\.[0-9]+){1,3}/.+'
   $staging=$relative -match '^staging/[a-f0-9]{32}/.+'
   $allowedExtension=$entry.Extension.ToLowerInvariant() -in @('.json','.png','.jpg','.webp','.svg') -or ($staging -and $entry.Extension.ToLowerInvariant() -eq '.zip')
   if(((-not $package)-and(-not $staging))-or($entry.Name -ne '.sha256'-and -not $allowedExtension)){throw 'Unknown package file preserved.'}
  }
  foreach($entry in $members){Remove-Item -LiteralPath $entry.FullName -Force}
  foreach($directory in (Get-ChildItem -LiteralPath $contentRoot -Recurse -Directory -Force|Sort-Object {$_.FullName.Length} -Descending)){Remove-Item -LiteralPath $directory.FullName}
  Remove-Item -LiteralPath $contentRoot
 }
 $ownedData=@('catalog-cache.json','catalog-cache.json.tmp','ui-state.json','ui-state.json.tmp','settings.json','settings.json.bak','settings.json.tmp','settings.json.corrupt','app.log','app.log.old','weather-cache.json','weather-cache.json.tmp','system-changes.json','system-changes.json.tmp','focus-session.json','focus-session.json.tmp','owned-images.json.tmp','bundle-cache.json.tmp','browser-companion.key')
 foreach($name in $ownedData) { $file=Join-Path $m.DataRoot $name; if (Test-Path -LiteralPath $file -PathType Leaf) { Remove-Item -LiteralPath $file -Force } }
 $imageJournal=Join-Path $m.DataRoot 'owned-images.json'
 $images=Join-Path $m.DataRoot 'images'
 if (Test-Path -LiteralPath $imageJournal) {
  $imageNames=@(Get-Content -LiteralPath $imageJournal -Raw | ConvertFrom-Json)
  foreach($name in $imageNames) {
   if ($name -notmatch '^[a-fA-F0-9]{32}\.(png|jpg|jpeg|bmp)$') { throw 'Invalid image ownership record' }
   $file=Join-Path $images $name
   if (Test-Path -LiteralPath $file -PathType Leaf) { Remove-Item -LiteralPath $file -Force }
  }
 }
 if ((Test-Path -LiteralPath $images) -and @(Get-ChildItem -LiteralPath $images -Force).Count -eq 0) { Remove-Item -LiteralPath $images }
 $unknownData=@(Get-ChildItem -LiteralPath $m.DataRoot -Force | Where-Object { $_.Name -notin @('owner.json','owned-images.json') })
 if ($unknownData.Count -gt 0) { throw ('Unknown data preserved; removal can be retried: '+($unknownData.Name -join ', ')) }
 if (Test-Path -LiteralPath $imageJournal) { Remove-Item -LiteralPath $imageJournal -Force }
 Remove-Item -LiteralPath $owner -Force
 Remove-Item -LiteralPath $m.DataRoot
}
$residuals=@()
foreach($relative in $m.Files) {
 if ($relative -eq 'uninstall.ps1') { continue }
 if ([IO.Path]::GetFileName($relative) -ne $relative) { throw 'Unsafe file name in manifest' }
 $file=Join-Path $installRoot $relative
 if (Test-Path -LiteralPath $file) { try { Remove-Item -LiteralPath $file -Force } catch { $residuals+=$file } }
}
$extra=@(Get-ChildItem -LiteralPath $installRoot -Force | Where-Object { $_.Name -notin @('installation.json','uninstall.ps1') })
if ($extra.Count -gt 0 -or $residuals.Count -gt 0) { throw ('Partial removal; remaining entries: '+(($extra.FullName+$residuals)-join ', ')) }
try {
 if (Test-Path -LiteralPath $retryScript) { Remove-Item -LiteralPath $retryScript -Force }
 Remove-Item -LiteralPath $marker -Force
 # Only an already verified empty root is removed, without recursive deletion.
 Remove-Item -LiteralPath $installRoot
 if (-not $m.Test) {
  $key='HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/YoWidgets'
  $uninstallValues=Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
  if ($uninstallValues.InstallLocation -eq $installRoot) { Remove-Item -LiteralPath $key -Recurse }
 }
} catch {
 # Keep the Apps entry until file finalization succeeds. Recreate only missing
 # recovery files if a lock interrupted deletion of either metadata file/root.
 New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
 if (-not (Test-Path -LiteralPath $marker)) { Set-Content -LiteralPath $marker -Value $manifestText -Encoding UTF8 }
 if (-not (Test-Path -LiteralPath $retryScript)) { Set-Content -LiteralPath $retryScript -Value $retryText -Encoding UTF8 }
 throw
}
Write-Output 'YoWidgets removed. Files, data and registrations verified absent.'

}
function Show-CleanupResult([string]$message) {
 Add-Type -AssemblyName PresentationFramework
 $window=New-Object System.Windows.Window
 $window.Title='YoWidgets — uninstall'
 $window.FontFamily=[Windows.Media.FontFamily]::new('Segoe UI Variable Text, Segoe UI')
 $window.FontWeight=[Windows.FontWeights]::Medium
 $window.Width=560; $window.Height=260; $window.WindowStartupLocation='CenterScreen'
 $window.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#11151D')
 $panel=New-Object System.Windows.Controls.StackPanel
 $panel.Margin=24
 $text=New-Object System.Windows.Controls.TextBlock
 $text.Text=$message; $text.TextWrapping='Wrap'; $text.FontSize=15; $text.Foreground=[Windows.Media.Brushes]::White
 $panel.Children.Add($text) | Out-Null
 $button=New-Object System.Windows.Controls.Button
 $button.Content='Close'; $button.Padding=10; $button.Margin='0,20,0,0'
 $button.Add_Click({ $window.Close() })
 $panel.Children.Add($button) | Out-Null
 $window.Content=$panel
 $window.ShowDialog() | Out-Null
}
try {
 Invoke-OwnedUninstall
 if ($ShowResult) { Show-CleanupResult 'YoWidgets was removed. Files, data and registrations were checked. Original photos and other apps were preserved.' }
} catch {
 if ($ShowResult) { Show-CleanupResult ('Uninstall incomplete: '+$_.Exception.Message); exit 1 }
 throw
}
