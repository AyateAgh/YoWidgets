$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
# Product-owned copy must stay English. ArtworkPolicy contains a source-name
# matcher in Cyrillic; that is input recognition, not application copy.
$files=@(Get-ChildItem -LiteralPath (Join-Path $root 'src/YoWidgets') -File | Where-Object { $_.Extension -in '.cs','.xaml' })
$files+=Get-Item -LiteralPath (Join-Path $root 'src/YoWidgets.Core/WeatherFallback.cs'),(Join-Path $root 'scripts/uninstall.ps1')
$violations=@($files | Select-String -Pattern '[\p{IsCyrillic}]')
if($violations.Count) { $violations | ForEach-Object { Write-Output ($_.Path+':'+$_.LineNumber+': '+$_.Line.Trim()) }; throw 'Non-English product copy found' }
Write-Output ('PASS English product copy: '+$files.Count+' source files; media and notification content excluded')
$production=Get-ChildItem -LiteralPath (Join-Path $root 'src/YoWidgets') -File -Filter '*.cs'
$stale=@($production | Select-String -Pattern 'IDesktopWallpaper|WallpaperProvider|GetWallpaper\(|MaterialPackage|Glass tint|real-time background')
if($stale.Count) { throw 'Removed global material/wallpaper path was reintroduced' }
$shader=Get-Content -LiteralPath (Join-Path $root 'shaders/Glass.hlsl') -Raw
if($shader -match 'tex2D\(|sampler2D|Texture2D') { throw 'The optical overlay must not sample wallpaper or screen textures' }
Write-Output 'PASS both packs have no production wallpaper source or global material switch'
