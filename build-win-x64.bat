@echo off
setlocal
cd /d "%~dp0"

echo ========================================
echo  VEDA Matrix – Build Windows x64
echo ========================================
echo.

dotnet publish -c Release -r win-x64 --self-contained true -o ./publish/win-x64 ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
  echo BUILD FEHLGESCHLAGEN
  exit /b 1
)

echo.
echo Fertig: publish\win-x64\VedaMatrix.exe
echo.
