$ErrorActionPreference = "Stop"

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputRoot = Split-Path -Parent $sourceRoot
$compiler = Join-Path $env:SystemRoot "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "找不到 .NET Framework 4.x C# 编译器：$compiler"
}

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:x64 `
    "/win32manifest:$sourceRoot\app.manifest" `
    "/win32icon:$outputRoot\SmartWindow.ico" `
    "/out:$outputRoot\SmartWindow.exe" `
    "/resource:$outputRoot\logo.png,SmartWindow.Resources.logo.png" `
    "/resource:$outputRoot\github.png,SmartWindow.Resources.github.png" `
    "/resource:$outputRoot\HYWenHei-85W.otf,SmartWindow.Resources.HYWenHei-85W.otf" `
    "/resource:$outputRoot\MediaMonitor.ps1,SmartWindow.Resources.MediaMonitor.ps1" `
    "/resource:$outputRoot\MediaCommand.ps1,SmartWindow.Resources.MediaCommand.ps1" `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Xml.dll `
    "$sourceRoot\SmartWindow.cs" `
    "$sourceRoot\IslandVisual.cs" `
    "$sourceRoot\MediaMonitor.cs" `
    "$sourceRoot\GlobalHotkeys.cs" `
    "$sourceRoot\GlobalMouseHook.cs" `
    "$sourceRoot\ConsoleForm.cs" `
    "$sourceRoot\EmbeddedAssets.cs" `
    "$sourceRoot\Localization.cs" `
    "$sourceRoot\AssemblyInfo.cs"

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

Write-Host "已重新编译：$outputRoot\SmartWindow.exe"
