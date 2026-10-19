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
echo Запускаю сервер Аверса с учебной базой в отдельном окне. Не закрывайте его, пока проверяете.
start "Сервер Аверса" dotnet run --project src\Avers.Server\Avers.Server.csproj -c Release --urls http://0.0.0.0:5115

set tries=0
:wait
set /a tries+=1
if %tries% gtr 90 (
  echo Сервер не ответил за 3 минуты. Посмотрите ошибку в окне «Сервер Аверса».
  pause
  exit /b 1
)
timeout /t 2 /nobreak >nul
curl -s -o nul http://localhost:5115/health || goto wait

echo.
echo Сервер работает. Адрес для телефона в той же сети Wi-Fi:
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /c:"IPv4"') do for /f %%b in ("%%a") do echo   http://%%b:5115/m/
echo Логин радиомеханика: novikov, пароль: avers
echo.
echo Запускаю программу Аверс, подключённую к этому серверу...
dotnet run --project src\Avers.Desktop\Avers.Desktop.csproj -c Release -- --server=http://localhost:5115
if errorlevel 1 pause
