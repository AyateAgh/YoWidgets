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
$single=Join-Path $projectRoot 'artifacts/single-exe'
& $sdk publish (Join-Path $projectRoot 'src/YoWidgets/YoWidgets.csproj') -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:IncludeAllContentForSelfExtract=true /p:DebugType=embedded -o $single --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$published=@(Get-ChildItem -LiteralPath $single -Force)
if ($published.Count -ne 1 -or $published[0].Name -ne 'YoWidgets.exe') { throw 'Single-file output contains unexpected extra files.' }
$output=Join-Path $projectRoot 'artifacts/install-payload'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $single 'YoWidgets.exe') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'),(Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/verification.md') -Destination (Join-Path $output 'VERIFICATION.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/cleanup-contract.md') -Destination (Join-Path $output 'CLEANUP-CONTRACT.md') -Force
$readmePath=Join-Path $output 'README.md'
$readme=(Get-Content -LiteralPath $readmePath -Raw).Replace('docs/verification.md','VERIFICATION.md').Replace('docs/cleanup-contract.md','CLEANUP-CONTRACT.md')
Set-Content -LiteralPath $readmePath -Value $readme -Encoding UTF8
Write-Output "Single executable: $single/YoWidgets.exe"
