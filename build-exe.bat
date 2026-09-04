@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "NO_PAUSE="
for %%A in (%*) do (
    if /i "%%~A"=="--no-pause" set "NO_PAUSE=1"
    if /i "%%~A"=="-no-pause" set "NO_PAUSE=1"
    if /i "%%~A"=="-NoPause" set "NO_PAUSE=1"
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Exe.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo Log salvo em:
echo %~dp0build-exe.log

if not defined NO_PAUSE pause
exit /b %EXIT_CODE%
