param([switch]$SkipTests)
$ErrorActionPreference='Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { throw 'Local SDK missing. See README.md.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_CLI_HOME=Join-Path $projectRoot '.tools/dotnet-home'
if (-not $SkipTests) {
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Tests') -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
& $sdk run --project (Join-Path $projectRoot 'tools/ShaderCompiler') -c Release -- (Join-Path $projectRoot 'shaders/Glass.hlsl') (Join-Path $projectRoot 'shaders/Glass.ps')
if ($LASTEXITCODE -ne 0) { throw 'Shader compilation failed' }
$output=Join-Path $projectRoot 'artifacts/release'
& $sdk publish (Join-Path $projectRoot 'src/YoWidgets') -c Release -r win-x64 --self-contained true -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $output -Force
Write-Output "Runnable build: $output/YoWidgets.exe"
