$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$executable = Join-Path $root "SmartWindow.exe"

if (-not (Test-Path -LiteralPath $executable)) {
    throw "找不到 SmartWindow.exe，请保持交付文件夹完整。"
}

$running = Get-Process -Name "SmartWindow" -ErrorAction SilentlyContinue
if ($running) {
    exit 0
}

Start-Process `
    -FilePath $executable `
    -ArgumentList "--background" `
    -WorkingDirectory $root `
    -WindowStyle Hidden
