@echo off
title Creality-SparkX Optimizer Launcher
cd /d "%~dp0"

echo ===================================================
echo     Creality Print 7.3 - Speed and Stability Optimizer
echo                    SparkX Suite
echo ===================================================
echo.

if exist "bin\Release\net8.0-windows\SparkXOptimizer.exe" (
    start "" "bin\Release\net8.0-windows\SparkXOptimizer.exe" %*
    exit /b 0
)

echo Compiling Release build...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Build failed. Please verify .NET 8 SDK is installed.
    pause
    exit /b 1
)

start "" "bin\Release\net8.0-windows\SparkXOptimizer.exe" %*
exit /b 0
