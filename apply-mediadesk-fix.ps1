# MediaDesk 0.1.1 non-destructive patch for the existing D:\MediaDesk-Windows project
$ErrorActionPreference = 'Stop'
$root = 'D:\MediaDesk-Windows'
$payload = Join-Path $PSScriptRoot 'files'
if (-not (Test-Path -LiteralPath (Join-Path $root 'src\MediaDesk\MediaDesk.csproj'))) {
    throw "MediaDesk project not found at $root"
}
if (Get-Process -Name MediaDesk -ErrorAction SilentlyContinue) {
    throw 'Close MediaDesk before running this patch, then retry.'
}
$items = @(
  'src\MediaDesk\App.xaml',
  'src\MediaDesk\App.xaml.cs',
  'src\MediaDesk\MainWindow.xaml',
  'src\MediaDesk\MainWindow.xaml.cs',
  'src\MediaDesk\PlayerWindow.xaml',
  'src\MediaDesk\Services\DownloadEngine.cs',
  'src\MediaDesk\Services\ErrorLog.cs',
  'src\MediaDesk\Models\AppState.cs',
  'src\MediaDesk\MediaDesk.csproj',
  'README_AR.md'
)
# Validate every shipped file before changing any project file.
foreach ($item in $items) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $item))) {
        throw "Missing patch file: $item"
    }
}
$backup = Join-Path $root ('backup-before-v0.1.1-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
foreach ($item in $items) {
    $src = Join-Path $payload $item
    $dst = Join-Path $root $item
    $old = Join-Path $backup $item
    if (Test-Path -LiteralPath $dst) {
        $oldParent = Split-Path $old -Parent
        New-Item -ItemType Directory -Path $oldParent -Force | Out-Null
        Copy-Item -LiteralPath $dst -Destination $old -Force
    }
    $parent = Split-Path $dst -Parent
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Force
}
Write-Host "PATCH APPLIED - backup: $backup" -ForegroundColor Green
Write-Host 'Publishing the Windows EXE...' -ForegroundColor Cyan
& (Join-Path $root 'build.ps1')
Write-Host 'PATCH AND BUILD FINISHED' -ForegroundColor Green
Write-Host "EXE: $(Join-Path $root 'release\win-x64\MediaDesk.exe')"
Write-Host 'If the app still closes, check: %LOCALAPPDATA%\MediaDesk\errors.log'
