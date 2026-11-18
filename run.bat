@echo off
REM ──────────────────────────────────────────────────────────────────────
REM  run.bat — кросс-версионный запуск игры.
REM  Совместим с Windows 7+, PowerShell 5+, и системным Python 3.10+.
REM ──────────────────────────────────────────────────────────────────────
setlocal EnableDelayedExpansion
chcp 65001 >nul 2>&1
cd /d "%~dp0"

REM 1) Если есть собранный .exe — запускаем его (Python не нужен)
if exist "Aethelgard.exe" goto :exe
if exist "dist\Aethelgard.exe" goto :dist_exe

REM 2) Иначе — через Python: сначала venv, потом системный интерпретатор
REM    Проверяем наличие python в PATH
where python >nul 2>&1
if errorlevel 1 (
    where py >nul 2>&1
    if errorlevel 1 goto :no_python
    set "PYTHON=py"
) else (
    set "PYTHON=python"
)

REM 2a) Виртуальное окружение
if exist ".venv\Scripts\python.exe" goto :venv
if exist "venv\Scripts\python.exe" goto :venv2

REM 2b) Системный Python
%PYTHON% main.py
goto :check

:exe
start "" "Aethelgard.exe"
exit /b 0

:dist_exe
start "" "dist\Aethelgard.exe"
exit /b 0

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
    echo Проверь, что установлен Python 3.10+ и зависимости:
    echo     pip install -r requirements.txt
    echo Или собери Aethelgard.exe через build.bat
    echo.
    pause
)
exit /b %errorlevel%

:no_python
echo.
echo [ОШИБКА] Python не найден в PATH.
echo Установи Python 3.10+ с https://python.org/
echo Или собери Aethelgard.exe через build.bat на машине с Python.
echo.
pause
exit /b 1
