@echo off
chcp 65001 >nul
setlocal

set "NAME=AutoShutdown"

taskkill /im "%NAME%.exe" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "%NAME%" /f >nul 2>&1
timeout /t 1 /nobreak >nul
rmdir /s /q "%LOCALAPPDATA%\%NAME%" >nul 2>&1

echo Программа остановлена и удалена, автозапуск отключён.
pause
