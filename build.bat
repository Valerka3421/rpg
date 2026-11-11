@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================
echo   Сборка standalone-версии игры
echo ============================================
echo.

python -m PyInstaller --version >nul 2>&1
if errorlevel 1 (
    echo Устанавливаю PyInstaller...
    python -m pip install --upgrade pyinstaller
    if errorlevel 1 (
        echo.
        echo [ОШИБКА] Не удалось установить pyinstaller.
        echo Проверь, что Python установлен и есть интернет.
        pause
        exit /b 1
    )
)

echo.
echo Сборка...
if exist "assets" (
    python -m PyInstaller --noconfirm --onefile --windowed --name Aethelgard ^
        --add-data "assets;assets" ^
        --icon "assets\icon.ico" ^
        --collect-all pygame ^
        main.py
) else (
    python -m PyInstaller --noconfirm --onefile --windowed --name Aethelgard ^
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
