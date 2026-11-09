@echo off
chcp 65001 >nul
cd /d "%~dp0"

REM 1) Если есть собранный .exe — запускаем его (Python не нужен)
if exist "Aethelgard.exe" goto :exe
if exist "dist\Aethelgard.exe" goto :dist_exe

REM 2) Иначе — через Python: сначала venv, потом системный интерпретатор
if exist ".venv\Scripts\python.exe" goto :venv
if exist "venv\Scripts\python.exe" goto :venv2
python main.py
goto :check

:exe
start "" "Aethelgard.exe"
exit /b

:dist_exe
start "" "dist\Aethelgard.exe"
exit /b

:venv
".venv\Scripts\python.exe" main.py
goto :check

:venv2
"venv\Scripts\python.exe" main.py
goto :check

:check
if errorlevel 1 (
    echo.
    echo [Игра завершилась с ошибкой. Код: %errorlevel%]
    echo Если у тебя нет Python — попроси разработчика собрать Aethelgard.exe
    pause
)
