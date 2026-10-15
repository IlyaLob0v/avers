@echo off
chcp 65001 >nul
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo На этом компьютере не найдена платформа .NET 10.
  echo Установите её с https://dotnet.microsoft.com/download/dotnet/10.0
  echo Выберите «SDK 10.0» для Windows x64, затем снова нажмите этот файл.
  pause
  exit /b 1
)
echo Запускаю Аверс...
dotnet run --project src\Avers.Desktop\Avers.Desktop.csproj -c Release
if errorlevel 1 pause
