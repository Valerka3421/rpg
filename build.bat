@echo off
REM ──────────────────────────────────────────────────────────────────────
REM  build.bat — сборка standalone-версии игры через PyInstaller.
REM  Совместим с Windows 7+, проверяет наличие Python и PyInstaller.
REM ──────────────────────────────────────────────────────────────────────
setlocal EnableDelayedExpansion
chcp 65001 >nul 2>&1
cd /d "%~dp0"
echo ============================================
echo   Сборка standalone-версии игры
echo ============================================
echo.

REM Определить Python (venv > системный > py launcher)
set "PYTHON="
if exist ".venv\Scripts\python.exe" (
    set "PYTHON=.venv\Scripts\python.exe"
) else if exist "venv\Scripts\python.exe" (
    set "PYTHON=venv\Scripts\python.exe"
) else (
    where python >nul 2>&1
    if not errorlevel 1 set "PYTHON=python"
)
if "!PYTHON!"=="" (
    where py >nul 2>&1
    if not errorlevel 1 set "PYTHON=py"
)

if "!PYTHON!"=="" (
    echo [ОШИБКА] Python не найден.
    echo Установи Python 3.10+ с https://python.org/
    pause
    exit /b 1
)

REM Проверка PyInstaller
!PYTHON! -m PyInstaller --version >nul 2>&1
if errorlevel 1 (
    echo Устанавливаю PyInstaller...
    !PYTHON! -m pip install --upgrade pyinstaller
    if errorlevel 1 (
        echo.
        echo [ОШИБКА] Не удалось установить pyinstaller.
        echo Проверь интернет-соединение и права доступа.
        pause
        exit /b 1
    )
)

echo.
echo Сборка...
if exist "assets" (
    !PYTHON! -m PyInstaller --noconfirm --onefile --windowed --name Aethelgard ^
        --add-data "assets;assets" ^
        --icon "assets\icon.ico" ^
        --collect-all pygame ^
        main.py
) else (
    !PYTHON! -m PyInstaller --noconfirm --onefile --windowed --name Aethelgard ^
        --collect-all pygame ^
        main.py
)

if errorlevel 1 (
    echo.
    echo [ОШИБКА] Сборка провалилась.
    pause
    exit /b 1
)

echo.
echo ============================================
echo   ГОТОВО!
echo   Файл: dist\Aethelgard.exe
echo   Размер ~40 МБ, Python внутри.
echo   Можно копировать на любую Windows-машину.
echo ============================================
pause
