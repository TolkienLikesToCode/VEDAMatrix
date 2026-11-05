@echo off
echo ========================================
echo  VEDA Matrix - Debug Start
echo ========================================
echo.

cd /d "%~dp0"

echo [1/3] Restore...
dotnet restore
if errorlevel 1 goto :error

echo.
echo [2/3] Build...
dotnet build -c Debug --no-restore
if errorlevel 1 goto :error

echo.
echo [3/3] Start...
echo.
dotnet run -c Debug --no-build

echo.
echo Anwendung beendet.
pause
exit /b 0

:error
echo.
echo FEHLER – siehe Ausgabe oben.
pause
exit /b 1
