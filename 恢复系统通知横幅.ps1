$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$executable = Join-Path $root "SmartWindow.exe"

Add-Type -AssemblyName System.Windows.Forms

if (Get-Process -Name "SmartWindow" -ErrorAction SilentlyContinue) {
    [System.Windows.Forms.MessageBox]::Show(
        "智慧窗仍在运行。请先右键任务栏托盘的智慧窗图标，选择「退出并恢复横幅」，再执行本脚本。",
        "智慧窗",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null
    exit 1
}

if (-not (Test-Path -LiteralPath $executable)) {
    throw "找不到 SmartWindow.exe，请保持交付文件夹完整。"
}

Start-Process `
    -FilePath $executable `
    -ArgumentList "--restore" `
    -WorkingDirectory $root `
    -WindowStyle Hidden `
    -Wait

[System.Windows.Forms.MessageBox]::Show(
    "系统通知横幅设置已恢复。",
    "智慧窗",
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information
) | Out-Null
