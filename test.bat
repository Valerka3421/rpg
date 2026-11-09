@echo off
chcp 65001 >nul
cd /d "%~dp0"

REM Тесты требуют pytest. Пробуем venv, затем системный Python.
if exist ".venv\Scripts\python.exe" goto :venv
goto :system

:venv
".venv\Scripts\python.exe" -m pytest -v
if not errorlevel 1 goto :done
echo.
echo [В .venv нет pytest — пробую системный Python]

:system
python -m pytest -v

:done
pause
