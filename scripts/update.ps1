param([switch]$NoLaunch)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$payload=Join-Path $projectRoot 'artifacts/install-payload'
$installRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/YoWidgets'))
$marker=Join-Path $installRoot 'installation.json'
# Existing user data is preserved. Never adopt an unowned folder or follow junctions.
foreach($path in @($installRoot,(Join-Path $env:LOCALAPPDATA 'YoWidgets'))) {
 $cursor=$path
 while($cursor) { if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse path rejected' }; $cursor=[IO.Path]::GetDirectoryName($cursor) }
}
$m=Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
if($m.Product -ne 'YoWidgets.Native.v1' -or $m.InstallRoot -ne $installRoot -or $m.DataRoot -ne (Join-Path $env:LOCALAPPDATA 'YoWidgets')) { throw 'Ownership mismatch' }
$o=Get-Content -LiteralPath (Join-Path $m.DataRoot 'owner.json') -Raw | ConvertFrom-Json
if($o.InstallationId -ne $m.Id -or $o.Product -ne $m.Product) { throw 'Data ownership mismatch' }
if(Get-Process YoWidgets -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $installRoot 'YoWidgets.exe') }) { throw 'Close installed YoWidgets before update' }
$newFiles=@(Get-ChildItem -LiteralPath $payload -File)
if(-not ($newFiles.Name -contains 'YoWidgets.exe')) { throw 'Build missing' }
foreach($file in $newFiles) {
 $dest=Join-Path $installRoot $file.Name
 if((Test-Path -LiteralPath $dest) -and ($file.Name -notin $m.Files)) { throw ('Unowned file collision: '+$file.Name) }
 if((Test-Path -LiteralPath $dest) -and ((Get-Item -LiteralPath $dest -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse file rejected' }
}
# Record the union before copies so interrupted updates remain cleanable.
$previous=@($m.Files)
$m.Files=@($previous+$newFiles.Name | Select-Object -Unique)
$m | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($marker+'.tmp') -Encoding UTF8
Move-Item -LiteralPath ($marker+'.tmp') -Destination $marker -Force
foreach($file in $newFiles) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $installRoot $file.Name) -Force }
foreach($relative in $previous) {
 if($relative -in $newFiles.Name) { continue }
 if([IO.Path]::GetFileName($relative) -ne $relative) { throw 'Unsafe old file name' }
 $old=Join-Path $installRoot $relative
 if(Test-Path -LiteralPath $old) { if((Get-Item -LiteralPath $old -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse old file rejected' }; Remove-Item -LiteralPath $old -Force }
}
$m.Files=@($newFiles.Name)
$m | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($marker+'.tmp') -Encoding UTF8
Move-Item -LiteralPath ($marker+'.tmp') -Destination $marker -Force
Set-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/YoWidgets' -Name DisplayVersion -Value '0.6.7'
Write-Output 'Updated to 0.6.7; user settings and source files preserved.'
if(-not $NoLaunch) { Start-Process -FilePath (Join-Path $installRoot 'YoWidgets.exe') -WindowStyle Hidden }
