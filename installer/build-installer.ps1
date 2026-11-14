$ErrorActionPreference = "Stop"

$installerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $installerRoot
$compiler = Join-Path $env:SystemRoot "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$payload = Join-Path $projectRoot "SmartWindow.exe"
$license = Join-Path $projectRoot "LICENSE.md"
$output = Join-Path $projectRoot "SmartWindow-Setup-x64.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "找不到 .NET Framework 4.x C# 编译器：$compiler"
}
if (-not (Test-Path -LiteralPath $payload)) {
    throw "找不到 SmartWindow.exe：$payload"
}
if (-not (Test-Path -LiteralPath $license)) {
    throw "找不到 LICENSE.md：$license"
}

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:x64 `
    "/win32manifest:$installerRoot\app.manifest" `
    "/win32icon:$projectRoot\SmartWindow.ico" `
    "/out:$output" `
    "/resource:$payload,SmartWindowInstaller.Payload.exe" `
    "/resource:$license,SmartWindowInstaller.LICENSE.md" `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Windows.Forms.dll `
    "$installerRoot\Program.cs" `
    "$installerRoot\AssemblyInfo.cs"

if ($LASTEXITCODE -ne 0) {
    throw "安装器编译失败，退出码：$LASTEXITCODE"
}

Write-Host "已生成安装器：$output"

