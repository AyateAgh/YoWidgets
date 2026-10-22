param([switch]$InstallDependencies)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
 & node scripts/generate-theme.mjs
 if($LASTEXITCODE -ne 0){throw 'Theme generation failed'}
 if($InstallDependencies -or !(Test-Path -LiteralPath 'ui/node_modules')){ & npm ci --prefix ui --no-audit --no-fund; if($LASTEXITCODE -ne 0){throw 'UI dependencies failed'} }
 & node scripts/ui-licenses.mjs
 if($LASTEXITCODE -ne 0){throw 'UI license collection failed'}
 & npm run build --prefix ui
 if($LASTEXITCODE -ne 0){throw 'UI compilation failed'}
 Copy-Item -LiteralPath 'ui/THIRD-PARTY-LICENSES.txt' -Destination 'ui/dist/THIRD-PARTY-LICENSES.txt' -Force
 $output=Join-Path $projectRoot 'artifacts'
 New-Item -ItemType Directory -Path $output -Force | Out-Null
 $bundle=Join-Path $output 'premium-ui.zip'
 Compress-Archive -Path (Join-Path $projectRoot 'ui/dist/*') -DestinationPath $bundle -Force
 Write-Output "Interface bundle: $bundle"
} finally {Pop-Location}
