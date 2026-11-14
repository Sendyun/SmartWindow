param(
    [string]$MakeAppxPath = "",
    [string]$SignToolPath = "",
    [string]$CertificateThumbprint = ""
)

$ErrorActionPreference = "Stop"

$msixRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $msixRoot
$staging = Join-Path $msixRoot "staging"
$output = Join-Path $projectRoot "SmartWindow_1.0.1.0_x64.msix"

function Find-SdkTool([string]$name) {
    $roots = @(
        "C:\Program Files (x86)\Windows Kits\10\bin",
        (Join-Path $projectRoot "..\sdk-buildtools")
    )
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }
        $tool = Get-ChildItem `
            -LiteralPath $root `
            -Recurse `
            -Filter $name `
            -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($tool) {
            return $tool.FullName
        }
    }
    return $null
}

if ([string]::IsNullOrWhiteSpace($MakeAppxPath)) {
    $MakeAppxPath = Find-SdkTool "makeappx.exe"
}
if ([string]::IsNullOrWhiteSpace($SignToolPath)) {
    $SignToolPath = Find-SdkTool "signtool.exe"
}
if (-not $MakeAppxPath -or -not (Test-Path -LiteralPath $MakeAppxPath)) {
    throw "找不到 makeappx.exe。请安装 Windows SDK 或传入 -MakeAppxPath。"
}
if (-not $SignToolPath -or -not (Test-Path -LiteralPath $SignToolPath)) {
    throw "找不到 signtool.exe。请安装 Windows SDK 或传入 -SignToolPath。"
}

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item `
    -LiteralPath (Join-Path $projectRoot "SmartWindow.exe") `
    -Destination $staging
Copy-Item `
    -LiteralPath (Join-Path $msixRoot "Package.appxmanifest") `
    -Destination (Join-Path $staging "AppxManifest.xml")
Copy-Item `
    -LiteralPath (Join-Path $msixRoot "Assets") `
    -Destination $staging `
    -Recurse

& $MakeAppxPath pack /d $staging /p $output /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx 打包失败，退出码：$LASTEXITCODE"
}

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object {
            $_.Subject -eq "CN=4591EE90-EA8E-423D-84B2-08FC9222DF54" -and
            $_.HasPrivateKey
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
    if (-not $certificate) {
        $certificate = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject "CN=4591EE90-EA8E-423D-84B2-08FC9222DF54" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -KeyUsage DigitalSignature `
            -KeyExportPolicy Exportable `
            -NotAfter (Get-Date).AddYears(3)
    }
    $CertificateThumbprint = $certificate.Thumbprint
}

& $SignToolPath sign `
    /fd SHA256 `
    /sha1 $CertificateThumbprint `
    $output
if ($LASTEXITCODE -ne 0) {
    throw "MSIX 签名失败，退出码：$LASTEXITCODE"
}

$verifyDirectory = Join-Path $msixRoot "verify"
if (Test-Path -LiteralPath $verifyDirectory) {
    Remove-Item -LiteralPath $verifyDirectory -Recurse -Force
}
& $MakeAppxPath unpack /p $output /d $verifyDirectory /o
if ($LASTEXITCODE -ne 0) {
    throw "MSIX 解包验证失败，退出码：$LASTEXITCODE"
}

$uploadDirectory = Join-Path $msixRoot "upload"
if (Test-Path -LiteralPath $uploadDirectory) {
    Remove-Item -LiteralPath $uploadDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $uploadDirectory | Out-Null
Copy-Item -LiteralPath $output -Destination $uploadDirectory
$uploadArchive = Join-Path $projectRoot "SmartWindow_1.0.1.0_x64.msixupload"
if (Test-Path -LiteralPath $uploadArchive) {
    Remove-Item -LiteralPath $uploadArchive -Force
}
Compress-Archive `
    -LiteralPath (Join-Path $uploadDirectory "SmartWindow_1.0.1.0_x64.msix") `
    -DestinationPath ($uploadArchive + ".zip") `
    -CompressionLevel Optimal
Move-Item `
    -LiteralPath ($uploadArchive + ".zip") `
    -Destination $uploadArchive

Write-Host "已生成并签名 MSIX：$output"
Write-Host "商店上传文件：$uploadArchive"
Write-Host "签名证书指纹：$CertificateThumbprint"

