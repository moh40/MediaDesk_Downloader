# Downloads the official Windows x64 yt-dlp executable, verifies SHA-256,
# and places it beside the built MediaDesk.exe (no Android emulator needed).
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$tools = Join-Path $PSScriptRoot 'tools'
$releaseTools = Join-Path $PSScriptRoot 'release\win-x64\tools'
if (-not (Test-Path -LiteralPath $tools)) { New-Item -ItemType Directory -Path $tools -Force | Out-Null }
$temporary = Join-Path $tools 'yt-dlp.exe.download'
$checksums = Join-Path $tools 'SHA2-256SUMS.download'
$destination = Join-Path $tools 'yt-dlp.exe'

try {
    Write-Host '=== Downloading yt-dlp from official GitHub releases ===' -ForegroundColor Cyan
    Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -UseBasicParsing -OutFile $temporary
    Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS' -UseBasicParsing -OutFile $checksums
    $content = [System.IO.File]::ReadAllText($checksums)
    $match = [regex]::Match($content, '(?im)^([a-f0-9]{64})\s+\*?yt-dlp\.exe\s*$')
    if (-not $match.Success) { throw 'Official checksum for yt-dlp.exe was not found; refusing to install.' }
    $expected = $match.Groups[1].Value.ToLowerInvariant()
    $actual = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expected -ne $actual) { throw 'yt-dlp.exe SHA256 mismatch; refusing to install.' }
    Move-Item -LiteralPath $temporary -Destination $destination -Force
    Write-Host 'yt-dlp SHA256 verified.' -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $temporary, $checksums -Force -ErrorAction SilentlyContinue
}

if (Test-Path -LiteralPath (Split-Path -Parent $releaseTools)) {
    New-Item -ItemType Directory -Path $releaseTools -Force | Out-Null
    Copy-Item -LiteralPath $destination -Destination (Join-Path $releaseTools 'yt-dlp.exe') -Force
}
Write-Host ('yt-dlp installed: ' + $destination) -ForegroundColor Green
if (Get-Command ffmpeg.exe -ErrorAction SilentlyContinue) {
    Write-Host 'ffmpeg found in PATH.' -ForegroundColor Green
}
else {
    Write-Host 'WARNING: ffmpeg not found. Some video/audio formats will fail until ffmpeg is installed.' -ForegroundColor Yellow
}
