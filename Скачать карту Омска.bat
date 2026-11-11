@echo off
chcp 65001 >nul
cd /d "%~dp0"
rem Скачивает офлайн-карту Омска (OpenStreetMap) для учебного сервера на этом компьютере и ставит на неё дома.
rem Нужен интернет только на время скачивания. Сервер Аверса при этом лучше закрыть.
set bbox=73.05,54.80,73.65,55.15
set version=1.31.2
set dir=%CD%\src\Avers.Server\map
if not exist "%dir%" mkdir "%dir%"

echo 1/3. Программа для вырезки карты...
curl -fsSL -o "%TEMP%\pmtiles.zip" https://github.com/protomaps/go-pmtiles/releases/download/v%version%/go-pmtiles_%version%_Windows_x86_64.zip || goto fail
tar -xf "%TEMP%\pmtiles.zip" -C "%TEMP%" pmtiles.exe || goto fail

echo 2/3. Карта Омска, несколько минут...
for /l %%d in (1,1,5) do (
  for /f %%x in ('powershell -NoProfile -Command "(Get-Date).ToUniversalTime().AddDays(-%%d).ToString('yyyyMMdd')"') do (
    if not exist "%dir%\city.pmtiles" "%TEMP%\pmtiles.exe" extract https://build.protomaps.com/%%x.pmtiles "%dir%\city.pmtiles" --bbox=%bbox%
  )
)
if not exist "%dir%\city.pmtiles" goto fail

echo 3/3. Адреса домов...
curl -fsS --retry 3 --max-time 600 https://overpass-api.de/api/interpreter --data-urlencode "data=[out:json][timeout:300];nwr[\"addr:housenumber\"][\"addr:street\"](54.80,73.05,55.15,73.65);out tags center;" -o "%dir%\addresses.json" || goto fail

dotnet run --project src\Avers.Server\Avers.Server.csproj -c Release -- map-import "%dir%\addresses.json" || goto fail
echo.
echo Карта готова. Запустите «Запустить сервер Аверса.bat» и нажмите «Карта» в программе.
pause
exit /b 0

:fail
echo Не получилось скачать карту. Проверьте интернет и запустите этот файл ещё раз.
pause
exit /b 1
