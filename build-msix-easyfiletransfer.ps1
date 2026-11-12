param(
    [ValidateSet("Store", "Sideload")]
    [string]$Mode = "Store",

    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  簡単ファイル転送 (EasyFileTransfer) MSIX Build Tool     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Mode:          $Mode" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Platform:      $Platform" -ForegroundColor Yellow
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

# Resizetizer のキャッシュ不整合によるスプラッシュ・アセット欠落防止
$targetObjDir = Join-Path $easyDir "obj\$Configuration\net10.0-windows10.0.19041.0\win-$Platform"
if (Test-Path $targetObjDir) {
    Get-ChildItem -Path $targetObjDir -Filter "mauisplash.*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

if ($Mode -eq "Sideload") {
    $outBase = Join-Path $easyDir "AppPackages"
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
        -p:AppxPackageDir="$outBase\" `
        -p:PackageCertificateKeyFile="$certPath" `
        -p:PackageCertificatePassword="" `
        -p:AppxPackageSigningEnabled=true `
        -p:AppxSymbolPackageEnabled=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    $cerPath = Join-Path $scriptDir "Package.cer"
    $testDirs = Get-ChildItem -Path $outBase -Directory -Filter "*_Test" -ErrorAction SilentlyContinue
    $outDir = if ($testDirs) { $testDirs | Sort-Object LastWriteTime -Descending | Select-Object -First 1 } else { $null }

    if ($outDir -and (Test-Path $outDir.FullName)) {
        if (Test-Path $cerPath) {
            Copy-Item $cerPath (Join-Path $outDir.FullName "EasyFileTransfer.cer") -Force
        }
        $msixFile = (Get-ChildItem -Path $outDir.FullName -Filter "*.msix" | Select-Object -First 1).FullName

        Write-Host "`n==========================================================" -ForegroundColor Green
        Write-Host "  Sideload MSIX Build Succeeded!                           " -ForegroundColor Green
        Write-Host "==========================================================" -ForegroundColor Green
        Write-Host "MSIX Package:" -ForegroundColor Cyan
        Write-Host "   $msixFile"
        if (Test-Path (Join-Path $outDir.FullName "EasyFileTransfer.cer")) {
            Write-Host "Certificate to Trust (.cer):" -ForegroundColor Cyan
            Write-Host "   $($outDir.FullName)\EasyFileTransfer.cer"
            Write-Host "`nInstall Guide (Local Testing):" -ForegroundColor Yellow
            Write-Host "   1. EasyFileTransfer.cer をダブルクリック -> 「証明書のインストール」"
            Write-Host "      -> 「ローカル コンピューター」 -> 「信頼されたルート証明機関」を選択して保存"
            Write-Host "   2. $($msixFile | Split-Path -Leaf) をダブルクリックしてインストール"
        }
    } else {
        Write-Host "`nBuild complete! Package output: $outBase" -ForegroundColor Green
    }
} else {
    # Store Submission Mode
    $uploadDir = Join-Path $easyDir "bin\$Configuration\net10.0-windows10.0.19041.0\win-$Platform\Upload"
    Write-Host "`nBuilding Microsoft Store Submission Package (.msix)..." -ForegroundColor Green
    dotnet publish $projectPath `
        -f net10.0-windows10.0.19041.0 `
        -c $Configuration `
        -r win-$Platform `
        -p:WindowsPackageType=MSIX `
        -p:AppxPackageDir="$uploadDir\" `
        -p:UapAppxPackageBuildMode=StoreUpload `
        -p:AppxPackageSigningEnabled=false `
        -p:AppxSymbolPackageEnabled=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    $uploadSubDirs = Get-ChildItem -Path $uploadDir -Directory -ErrorAction SilentlyContinue
    $targetDir = if ($uploadSubDirs) { $uploadSubDirs | Sort-Object LastWriteTime -Descending | Select-Object -First 1 } else { $null }
    $msixFile = if ($targetDir) { 
        $found = Get-ChildItem -Path $targetDir.FullName -Filter "*.msix" | Select-Object -First 1
        if (-not $found) {
            $found = Get-ChildItem -Path $targetDir.FullName -Filter "*.msixupload" | Select-Object -First 1
        }
        if ($found) { $found.FullName } else { $null }
    } else { $null }

    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host "  Microsoft Store Submission Package Succeeded!            " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "Store Package Path:" -ForegroundColor Cyan
    if ($msixFile) {
        Write-Host "   $msixFile"
    } else {
        Write-Host "   $uploadDir"
    }
    Write-Host "`nPartner Center Submission Guide:" -ForegroundColor Yellow
    Write-Host "   1. Partner Center (https://partner.microsoft.com/dashboard) にログイン"
    Write-Host "   2. アプリ「EasyFileTransfer」(StoreID: XXXXXXXXXXXX) の申請ページを開く"
    Write-Host "   3. 「パッケージ」ページに上記 .msix ファイルをアップロード"
    Write-Host "   4. 価格と提供状況: 「無料」を選択（課金・アプリ内購入なし）"
    Write-Host "   5. 審査に提出"
}
