param([switch]$SkipTests)
$ErrorActionPreference='Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk=(Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_CLI_HOME=Join-Path $projectRoot '.tools/dotnet-home'
& node (Join-Path $PSScriptRoot 'publish-catalog.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Catalog generation failed' }
& (Join-Path $PSScriptRoot 'build-ui.ps1')
if (-not $SkipTests) {
 & npm test --prefix (Join-Path $projectRoot 'ui')
 if ($LASTEXITCODE -ne 0) { throw 'Interface tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Platform.Tests') -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Content platform tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Platform.UiTests') -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Management platform tests failed' }
 & node --test (Join-Path $projectRoot 'integrations/browser-bridge/provider.test.cjs') (Join-Path $projectRoot 'integrations/browser-bridge/content.test.cjs') (Join-Path $projectRoot 'integrations/assistant-browser-bridge/worker.test.cjs')
 if ($LASTEXITCODE -ne 0) { throw 'Browser provider tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Tests') -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Assistant transport tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --voice-check
 if ($LASTEXITCODE -ne 0) { throw 'Live voice transport tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --voice-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Live voice native UI tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --widget-ipc-check
 if ($LASTEXITCODE -ne 0) { throw 'Widget command and named-pipe IPC tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --ui-preview --agent-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Assistant native interaction tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --activity-motion-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Assistant activity and motion tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --composer-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Composer native tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --composer-polish-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Composer editor, connection and popup tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --media-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Generated media native tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --generation-settings-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Generation connection settings tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --mention-ui-check
 if ($LASTEXITCODE -ne 0) { throw 'Composer mention and popup interaction tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --control-check
 if ($LASTEXITCODE -ne 0) { throw 'Browser and control-session tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tests/YoWidgets.Assistant.Tests') -c Release --no-build -- --document-check
 if ($LASTEXITCODE -ne 0) { throw 'Native document generation tests failed' }
 & $sdk run --project (Join-Path $projectRoot 'tools/CompositionProbe') -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Native composition probe failed' }
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
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'widget-command.ps1') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/widget-ipc.md') -Destination (Join-Path $output 'WIDGET-IPC.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'),(Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'PRIVACY.md'),(Join-Path $projectRoot 'SECURITY.md'),(Join-Path $projectRoot 'ui/THIRD-PARTY-LICENSES.txt') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/verification.md') -Destination (Join-Path $output 'VERIFICATION.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/cleanup-contract.md') -Destination (Join-Path $output 'CLEANUP-CONTRACT.md') -Force
$bridge=Join-Path $projectRoot 'integrations/browser-bridge'
$bridgeZip=Join-Path $projectRoot 'artifacts/browser-bridge.zip'
Compress-Archive -LiteralPath @((Join-Path $bridge 'manifest.json'),(Join-Path $bridge 'soundcloud.js'),(Join-Path $bridge 'content.js'),(Join-Path $bridge 'worker.js'),(Join-Path $bridge 'README.md')) -DestinationPath $bridgeZip -Force
Copy-Item -LiteralPath $bridgeZip -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $bridge 'README.md') -Destination (Join-Path $output 'BROWSER-BRIDGE.md') -Force
$assistantBridge=Join-Path $projectRoot 'integrations/assistant-browser-bridge'
$assistantZip=Join-Path $projectRoot 'artifacts/assistant-browser-bridge.zip'
Compress-Archive -LiteralPath @((Join-Path $assistantBridge 'manifest.json'),(Join-Path $assistantBridge 'worker.js'),(Join-Path $assistantBridge 'popup.html'),(Join-Path $assistantBridge 'popup.js'),(Join-Path $assistantBridge 'README.md')) -DestinationPath $assistantZip -Force
Copy-Item -LiteralPath $assistantZip -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $assistantBridge 'README.md') -Destination (Join-Path $output 'ASSISTANT-BROWSER.md') -Force
$readmePath=Join-Path $output 'README.md'
$readme=(Get-Content -LiteralPath $readmePath -Raw).Replace('docs/verification.md','VERIFICATION.md').Replace('docs/cleanup-contract.md','CLEANUP-CONTRACT.md').Replace('docs/widget-ipc.md','WIDGET-IPC.md').Replace('integrations/browser-bridge/README.md','BROWSER-BRIDGE.md').Replace('integrations/assistant-browser-bridge/README.md','ASSISTANT-BROWSER.md')
Set-Content -LiteralPath $readmePath -Value $readme -Encoding UTF8
Write-Output "Single executable: $single/YoWidgets.exe"
