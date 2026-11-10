@echo off
setlocal enabledelayedexpansion
title SparkX - Creality Print Send Dialog Zoom Fix (Issue #649)

:: Verifica privilegi di Amministratore ed auto-elevazione
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Richiesta elevazione Amministratore (UAC)...
    powershell -NoProfile -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

:menu
cls
echo ======================================================================
echo    CREALITY PRINT 7.3 - SEND PRINT DIALOG ZOOM FIX (Issue #649)
echo    Risoluzione pulsanti "Start Print" / "Send Only" tagliati fuori
echo ======================================================================
echo.
echo  Questo fix corregge il Send Print dialog in cui i pulsanti di invio stampa
echo  vengono tagliati fuori dallo schermo costringendo a ridimensionare la finestra.
echo  Imposta lo zoom automatico all'80%% e abilita la regolazione dinamica con Ctrl +/-.
echo.
echo  [1] Applica Fix (Zoom 80%% predefinito + Ctrl +/- dinamico)
echo  [2] Imposta uno Zoom personalizzato (es. 70%%, 75%%, 85%%)
echo  [3] Ripristina file originale (index.html.bak)
echo  [4] Esci
echo.
set /p choice="Seleziona un'opzione [1-4]: "

if "%choice%"=="1" goto apply_default
if "%choice%"=="2" goto apply_custom
if "%choice%"=="3" goto restore_original
if "%choice%"=="4" exit /b 0
goto menu

:apply_default
set "ZOOM_VAL=0.80"
set "ZOOM_PCT=80"
goto run_patch

:apply_custom
echo.
set /p custom_pct="Inserisci la percentuale di zoom desiderata (es. 75, 80, 85): "
if "%custom_pct%"=="" set "custom_pct=80"
set "ZOOM_PCT=%custom_pct%"
powershell -NoProfile -Command "[double]$p = %custom_pct% / 100.0; [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.00}', $p)" > "%TEMP%\sparkx_zoom.tmp"
set /p ZOOM_VAL=<"%TEMP%\sparkx_zoom.tmp"
del "%TEMP%\sparkx_zoom.tmp" >nul 2>&1
goto run_patch

:run_patch
echo.
echo [1/3] Ricerca installazione di Creality Print 7.3...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& {
    $paths = @(
        'C:\Program Files\Creality\Creality Print 7.3\resources\web\sendToPrinterPage\index.html',
        'C:\Program Files\Creality\Creality Print\resources\web\sendToPrinterPage\index.html'
    )
    $target = $null
    foreach ($p in $paths) {
        if (Test-Path $p) { $target = $p; break }
    }
    if (!$target) {
        Write-Host '[ERRORE] File sendToPrinterPage/index.html non trovato!' -ForegroundColor Red
        exit 1
    }

    Write-Host \"[2/3] Trovato: $target\" -ForegroundColor Cyan
    $bak = $target + '.bak'
    if (!(Test-Path $bak)) {
        Copy-Item $target $bak -Force
        Write-Host \"[BACKUP] Creato backup originale: $bak\" -ForegroundColor Yellow
    }

    $content = [System.IO.File]::ReadAllText($target, [System.Text.Encoding]::UTF8)
    
    # Rimuovi eventuale patch precedente se presente
    if ($content.Contains('<!-- SparkX Fix: Send Print Dialog Auto-Fit (Issue #649) -->')) {
        $reg = [regex]'<!-- SparkX Fix: Send Print Dialog Auto-Fit \(Issue #649\) -->[\s\S]*?<\/script>'
        $content = $reg.Replace($content, '')
    }

    $patch = @'
        <!-- SparkX Fix: Send Print Dialog Auto-Fit (Issue #649) -->
        <style id='sparkx-send-dialog-style'>
            html, body {
                overflow-y: auto !important;
            }
            body {
                zoom: ' + $env:ZOOM_PCT + '% !important;
            }
        </style>
        <script id='sparkx-send-dialog-script'>
            (function() {
                var currentZoom = ' + $env:ZOOM_VAL + ';
                function applyZoom(z) {
                    currentZoom = z;
                    document.body.style.zoom = (currentZoom * 100) + '%';
                }
                window.addEventListener('keydown', function(e) {
                    if (e.ctrlKey) {
                        if (e.key === '+' || e.key === '=') {
                            e.preventDefault();
                            applyZoom(Math.min(1.2, currentZoom + 0.05));
                        } else if (e.key === '-' || e.key === '_') {
                            e.preventDefault();
                            applyZoom(Math.max(0.5, currentZoom - 0.05));
                        } else if (e.key === '0') {
                            e.preventDefault();
                            applyZoom(' + $env:ZOOM_VAL + ');
                        }
                    }
                });
            })();
        </script>
'@
    $patch = $patch.Replace(''' + $env:ZOOM_PCT + ''', $env:ZOOM_PCT).Replace(''' + $env:ZOOM_VAL + ''', $env:ZOOM_VAL)

    $headIdx = $content.IndexOf('</head>')
    if ($headIdx -ge 0) {
        $content = $content.Insert($headIdx, $patch + [Environment]::NewLine)
        [System.IO.File]::WriteAllText($target, $content, [System.Text.Encoding]::UTF8)
        Write-Host \"[3/3] [SUCCESSO] Patch applicata con successo! (Zoom $env:ZOOM_PCT%)\" -ForegroundColor Green
        Write-Host \"      Puoi usare Ctrl + e Ctrl - direttamente nella finestra per regolare lo zoom.\" -ForegroundColor Green
    } else {
        Write-Host '[ERRORE] Tag </head> non trovato nel file HTML.' -ForegroundColor Red
        exit 1
    }
}"

echo.
pause
goto menu

:restore_original
echo.
echo [1/2] Ripristino del file index.html originale...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& {
    $paths = @(
        'C:\Program Files\Creality\Creality Print 7.3\resources\web\sendToPrinterPage\index.html',
        'C:\Program Files\Creality\Creality Print\resources\web\sendToPrinterPage\index.html'
    )
    $restored = $false
    foreach ($p in $paths) {
        $bak = $p + '.bak'
        if (Test-Path $bak) {
            Copy-Item $bak $p -Force
            Write-Host \"[RIPRISTINO] Ripristinato file originale da: $bak\" -ForegroundColor Green
            $restored = $true
        }
    }
    if (!$restored) {
        Write-Host '[AVVISO] Nessun file di backup .bak trovato da ripristinare.' -ForegroundColor Yellow
    }
}"

echo.
pause
goto menu
