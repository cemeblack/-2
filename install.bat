@echo off
chcp 65001 >nul
setlocal

set "NAME=AutoShutdown"
set "SRC=%~dp0AutoShutdown.cs"
set "DEST=%LOCALAPPDATA%\%NAME%"
set "EXE=%DEST%\%NAME%.exe"

if not exist "%SRC%" (
    echo Не найден файл AutoShutdown.cs рядом с install.bat.
    echo Если вы запускаете из ZIP-архива - сначала распакуйте его.
    pause
    exit /b 1
)

rem Компилятор C#, который уже входит в Windows (.NET Framework 4)
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Не найден компилятор .NET Framework 4 - csc.exe
    pause
    exit /b 1
)

rem Если программа уже запущена - останавливаем, чтобы заменить exe
taskkill /im "%NAME%.exe" /f >nul 2>&1
timeout /t 1 /nobreak >nul

if not exist "%DEST%" mkdir "%DEST%"

echo Сборка программы...
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:"%EXE%" "%SRC%"
if errorlevel 1 (
    echo Ошибка сборки.
    pause
    exit /b 1
)

rem Автозапуск при входе в Windows
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "%NAME%" /t REG_SZ /d "\"%EXE%\"" /f >nul

start "" "%EXE%"

echo.
echo Готово! Программа установлена в %DEST%
echo и будет запускаться сама при каждом входе в Windows.
echo Компьютер выключится в 21:00, таймер появится за час до этого.
echo.
pause
