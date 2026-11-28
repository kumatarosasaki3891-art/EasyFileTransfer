param(
    [ValidateSet("Store", "Sideload")]
    [string]$Mode = "Store",

    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",

    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$distDir = if ([string]::IsNullOrWhiteSpace($OutputDir)) { Join-Path $scriptDir "dist" } else { $OutputDir }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  簡単ファイル転送 (EasyFileTransfer) MSIX Build Tool     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Mode:          $Mode" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Platform:      $Platform" -ForegroundColor Yellow
Write-Host "Output Dir:    $distDir" -ForegroundColor Yellow
Write-Host "Display Name:  簡単ファイル転送" -ForegroundColor Gray
Write-Host "Identity Name: XXXXXXXX.XXXXXXXXXXXXX" -ForegroundColor Gray
Write-Host "Publisher:     CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX (XXXX)" -ForegroundColor Gray
Write-Host "PFN:           XXXXXXXX.XXXXXXXXXXXXX_XXXXXXXXXXXXX" -ForegroundColor Gray
Write-Host "Store ID:      XXXXXXXXXXXX" -ForegroundColor Gray
Write-Host "課金形態:      完全無料（課金・アプリ内購入なし）" -ForegroundColor Green
Write-Host "----------------------------------------------------------" -ForegroundColor Gray

# 署名用の証明書 (.pfx) は公開リポジトリに含めていないため、Sideloadビルド時はローカルにご用意ください
$certPath = Join-Path $scriptDir "Package_TemporaryKey.pfx"
$projectPath = Join-Path $scriptDir "EasyFileTransfer\EasyFileTransfer.csproj"
$easyDir = Join-Path $scriptDir "EasyFileTransfer"

# Resizetizer / アイコン / PRI / AppHost キャッシュの完全クリーンアップ（古いタイムスタンプのアイコン上書き時の反映漏れ防止）
$targetObjDir = Join-Path $easyDir "obj\$Configuration\net10.0-windows10.0.19041.0\win-$Platform"
if (Test-Path $targetObjDir) {
    $resizetizerDir = Join-Path $targetObjDir "resizetizer"
    if (Test-Path $resizetizerDir) {
        Remove-Item -Path $resizetizerDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    foreach ($pattern in @("maui*.*", "priconfig*", "*.pri", "apphost.exe", "EasyFileTransfer.exe")) {
        Get-ChildItem -Path $targetObjDir -Filter $pattern -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

$targetBinDir = Join-Path $easyDir "bin\$Configuration\net10.0-windows10.0.19041.0\win-$Platform"
if (Test-Path $targetBinDir) {
    foreach ($pattern in @("EasyFileTransfer.exe", "resources.pri", "appicon*.png", "appicon.ico", "splashSplashScreen*.png")) {
        Get-ChildItem -Path $targetBinDir -Filter $pattern -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

$publishDir = Join-Path $targetBinDir "publish"
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}

$workAppxDir = Join-Path $targetBinDir "AppxWork\$Mode"
if (Test-Path $workAppxDir) {
    Remove-Item -Path $workAppxDir -Recurse -Force
}

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

if ($Mode -eq "Sideload") {
    Write-Host "`nBuilding Sideload MSIX (Signed with local certificate)..." -ForegroundColor Green

    if (-not (Test-Path $certPath)) {
        Write-Error "Certificate file not found at: $certPath"
        exit 1
    }

    dotnet publish $projectPath `
        -f net10.0-windows10.0.19041.0 `
        -c $Configuration `
        -r win-$Platform `
        -p:WindowsPackageType=MSIX `
        -p:AppxPackageDir="$workAppxDir\" `
        -p:PackageCertificateKeyFile="$certPath" `
        -p:PackageCertificatePassword="" `
        -p:AppxPackageSigningEnabled=true `
        -p:AppxSymbolPackageEnabled=false `
        -p:DebugType=none `
        -p:DebugSymbols=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    $cerPath = Join-Path $scriptDir "Package.cer"
    $testDirs = Get-ChildItem -Path $workAppxDir -Directory -Filter "*_Test" -ErrorAction SilentlyContinue
    $srcOutDir = if ($testDirs) { ($testDirs | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName } else { $workAppxDir }

    $msixSource = Get-ChildItem -Path $srcOutDir -Filter "*.msix" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $msixSource) {
        $msixSource = Get-ChildItem -Path $workAppxDir -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if (-not $msixSource) {
        throw "Sideload MSIX package was not generated under: $workAppxDir"
    }

    $sideloadOutDir = Join-Path $distDir "Sideload"
    if (Test-Path $sideloadOutDir) {
        Remove-Item -Path $sideloadOutDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $sideloadOutDir -Force | Out-Null

    $destMsix = Join-Path $sideloadOutDir $msixSource.Name
    Copy-Item -Path $msixSource.FullName -Destination $destMsix -Force

    $depsSource = Join-Path $msixSource.DirectoryName "Dependencies"
    if (Test-Path $depsSource) {
        Copy-Item -Path $depsSource -Destination (Join-Path $sideloadOutDir "Dependencies") -Recurse -Force
    }

    $destCer = Join-Path $sideloadOutDir "EasyFileTransfer.cer"
    if (Test-Path $cerPath) {
        Copy-Item -Path $cerPath -Destination $destCer -Force
    }

    $msixSizeMb = [math]::Round((Get-Item $destMsix).Length / 1MB, 2)
    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host "  Sideload MSIX Build Succeeded!                           " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "MSIX Package ($msixSizeMb MB):" -ForegroundColor Cyan
    Write-Host "   $destMsix"
    if (Test-Path $destCer) {
        Write-Host "Certificate to Trust (.cer):" -ForegroundColor Cyan
        Write-Host "   $destCer"
        Write-Host "`nInstall Guide (Local Testing):" -ForegroundColor Yellow
        Write-Host "   1. EasyFileTransfer.cer をダブルクリック -> 「証明書のインストール」"
        Write-Host "      -> 「ローカル コンピューター」 -> 「信頼されたルート証明機関」を選択して保存"
        Write-Host "   2. $($msixSource.Name) をダブルクリックしてインストール"
    }
} else {
    # Store Submission Mode
    Write-Host "`nBuilding Microsoft Store Submission Package (.msix)..." -ForegroundColor Green
    dotnet publish $projectPath `
        -f net10.0-windows10.0.19041.0 `
        -c $Configuration `
        -r win-$Platform `
        -p:WindowsPackageType=MSIX `
        -p:AppxPackageDir="$workAppxDir\" `
        -p:UapAppxPackageBuildMode=StoreUpload `
        -p:AppxPackageSigningEnabled=false `
        -p:AppxSymbolPackageEnabled=false `
        -p:DebugType=none `
        -p:DebugSymbols=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    $uploadSubDirs = Get-ChildItem -Path $workAppxDir -Directory -ErrorAction SilentlyContinue
    $targetDir = if ($uploadSubDirs) { ($uploadSubDirs | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName } else { $workAppxDir }

    $msixSource = Get-ChildItem -Path $targetDir -Filter "*.msix" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $msixSource) {
        $msixSource = Get-ChildItem -Path $workAppxDir -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    $msixUploadSource = Get-ChildItem -Path $targetDir -Filter "*.msixupload" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $msixUploadSource) {
        $msixUploadSource = Get-ChildItem -Path $workAppxDir -Recurse -Filter "*.msixupload" -ErrorAction SilentlyContinue | Select-Object -First 1
    }

    if (-not $msixSource -and -not $msixUploadSource) {
        throw "Store MSIX package was not generated under: $workAppxDir"
    }

    $copiedOutputs = @()
    if ($msixSource) {
        $destMsix = Join-Path $distDir $msixSource.Name
        Copy-Item -Path $msixSource.FullName -Destination $destMsix -Force
        $copiedOutputs += $destMsix
    }
    if ($msixUploadSource) {
        $destUpload = Join-Path $distDir $msixUploadSource.Name
        Copy-Item -Path $msixUploadSource.FullName -Destination $destUpload -Force
        $copiedOutputs += $destUpload
    }

    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host "  Microsoft Store Submission Package Succeeded!            " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "Store Package Path:" -ForegroundColor Cyan
    foreach ($outFile in $copiedOutputs) {
        $sizeMb = [math]::Round((Get-Item $outFile).Length / 1MB, 2)
        Write-Host "   $outFile ($sizeMb MB)"
    }
    Write-Host "`nPartner Center Submission Guide:" -ForegroundColor Yellow
    Write-Host "   1. Partner Center (https://partner.microsoft.com/dashboard) にログイン"
    Write-Host "   2. アプリ「EasyFileTransfer」(StoreID: XXXXXXXXXXXX) の申請ページを開く"
    Write-Host "   3. 「パッケージ」ページに上記 .msix ファイルをアップロード"
    Write-Host "   4. 価格と提供状況: 「無料」を選択（課金・アプリ内購入なし）"
    Write-Host "   5. 審査に提出"
}
