@echo off
setlocal
cd /d "%~dp0"

echo ========================================================
echo  EasyFileTransfer - MSIX Build Launcher
echo ========================================================
echo 1. Build Microsoft Store Package (StoreUpload - for Submission)
echo 2. Build Sideload Package (Signed - for Local Testing)
echo ========================================================
set /p CHOICE="Select build mode (1 or 2, default is 1): "

if "%CHOICE%"=="2" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-msix-easyfiletransfer.ps1" -Mode Sideload
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-msix-easyfiletransfer.ps1" -Mode Store
)

pause
