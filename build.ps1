# Run from any working directory: powershell -executionpolicy bypass -file .\build.ps1
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\MediaDesk\MediaDesk.csproj'
$output = Join-Path $PSScriptRoot 'release\win-x64'

if (-not (get-command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK 10 is missing. Install it with: winget install Microsoft.DotNet.SDK.10'
}
$sdkMajor = @(dotnet --list-sdks | where-object { $_ -match '^10\.' }).Count
if ($sdkMajor -eq 0) {
    throw 'A .NET 10 SDK is required. Run: winget install Microsoft.DotNet.SDK.10'
}
write-host '=== mediadesk: publishing self-contained Windows x64 EXE ==='
& dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)" }
$exe = Join-Path $output 'MediaDesk.exe'
if (-not (test-path $exe)) { throw 'The compiler did not produce MediaDesk.exe' }
$videoTool = Join-Path $PSScriptRoot 'tools\yt-dlp.exe'
if (test-path -literalpath $videoTool) {
    $outTools = Join-Path $output 'tools'
    new-item -itemtype directory -path $outTools -force | out-null
    copy-item -literalpath $videoTool -destination (Join-Path $outTools 'yt-dlp.exe') -force
    write-host 'Bundled yt-dlp.exe as an external helper.'
}
else {
    write-host 'Video-page helper not installed. Run setup-video-tools.ps1 to enable video pages.' -ForegroundColor Yellow
}
write-host '=== build complete ===' -ForegroundColor Green
write-host $exe
get-item $exe | select-object FullName,Length,LastWriteTime | format-list
