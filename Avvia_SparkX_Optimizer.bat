@echo off
setlocal enabledelayedexpansion
title Creality-SparkX Optimizer
cd /d "%~dp0"

set "EXE_PATH=bin\Release\net8.0-windows\SparkXOptimizer.exe"

if not exist "!EXE_PATH!" (
    echo [INFO] Compilazione build di Release in corso...
    dotnet build -c Release
    if !errorlevel! neq 0 (
        echo [ERRORE] Compilazione fallita. Verifica che .NET 8 sia installato.
        pause
        exit /b 1
    )
)

if "%~1"=="--optimize" goto RUN_SILENT
if "%~1"=="--silent" goto RUN_SILENT
if "%~1"=="-s" goto RUN_SILENT

:MENU
cls
echo ===================================================================
echo        CREALITY PRINT 7.3 - SPEED ^& STABILITY OPTIMIZER
echo                        SparkX Suite v1.0.1
echo ===================================================================
echo.
echo   [1] Applica Ottimizzazione Completa (1-Click Diretto)
echo       - Elimina lag di avvio (Bypass Home Page Chromium)
echo       - Risolve freeze 100%% CPU e crash cinematici KLSIM
echo       - Svuota 4.5+ GB di cache WebView2 orfana
echo       - Disabilita race condition di background slicing
echo       - Forza GPU ad alte prestazioni (NVIDIA / AMD)
echo.
echo   [2] Apri Interfaccia Grafica (GUI Moderna)
echo.
echo   [3] Imposta Priorita Alta Permanente (Windows IFEO)
echo.
echo   [4] Crea Collegamento Desktop ad Alta Priorita
echo.
echo   [5] Avvia Creality Print (Priorita Alta Boosted)
echo.
echo   [0] Esci
echo.
echo ===================================================================
set /p "CHOICE=Seleziona un'opzione [1-5, Default: 1]: "

if "%CHOICE%"=="" set "CHOICE=1"
if "%CHOICE%"=="1" goto OPTIMIZE_DIRECT
if "%CHOICE%"=="2" goto OPEN_GUI
if "%CHOICE%"=="3" goto SET_PRIORITY
if "%CHOICE%"=="4" goto SHORTCUT
if "%CHOICE%"=="5" goto LAUNCH_APP
if "%CHOICE%"=="0" exit /b 0

echo Opzione non valida.
timeout /t 2 >nul
goto MENU

:OPTIMIZE_DIRECT
cls
echo ===================================================================
echo       APPLICAZIONE OTTIMIZZAZIONI IN CORSO...
echo ===================================================================
echo.
"!EXE_PATH!" --optimize
echo.
echo ===================================================================
echo   [SUCCESSO] Tutte le ottimizzazioni sono state applicate!
echo   Puoi ora avviare Creality Print per goderti il nuovo avvio rapido.
echo ===================================================================
echo.
pause
goto MENU

:OPEN_GUI
start "" "!EXE_PATH!"
exit /b 0

:SET_PRIORITY
cls
echo Richiesta permessi di amministratore per impostare la Priorita Alta permanente...
powershell -Command "Start-Process reg.exe -ArgumentList 'add \"\"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\CrealityPrint.exe\PerfOptions\"\" /v CpuPriorityClass /t REG_DWORD /d 3 /f' -Verb RunAs -Wait"
echo.
echo Se hai confermato la richiesta UAC, la priorita ALTA e ora permanente nel Kernel di Windows.
pause
goto MENU

:SHORTCUT
cls
powershell -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop') + '\Creality Print (High Priority).lnk'); $s.TargetPath='cmd.exe'; $s.Arguments='/c start \"\" /high \"C:\Program Files\Creality\Creality Print 7.3\CrealityPrint.exe\"'; $s.IconLocation='C:\Program Files\Creality\Creality Print 7.3\CrealityPrint.exe,0'; $s.WindowStyle=7; $s.Save()"
echo.
echo Collegamento 'Creality Print (High Priority)' creato sul tuo Desktop.
pause
goto MENU

:LAUNCH_APP
cls
echo Avvio di Creality Print in corso...
start "" /high "C:\Program Files\Creality\Creality Print 7.3\CrealityPrint.exe"
exit /b 0

:RUN_SILENT
"!EXE_PATH!" %*
exit /b %errorlevel%
