@echo off
chcp 65001 >nul
rem Ставит корневой сертификат сервера Аверса в доверенные (для всех пользователей компьютера).
rem Положите рядом файл avers-root.crt с сервера и запустите: правой кнопкой - "Запуск от имени администратора".
cd /d "%~dp0"
if not exist avers-root.crt (
  echo Рядом с этим файлом нет avers-root.crt. Скачайте его с сервера, см. docs/09-server.md.
  pause
  exit /b 1
)
echo Сверьте контрольную сумму с той, что написал install.sh на сервере:
certutil -hashfile avers-root.crt SHA256 | findstr /v ":"
echo.
choice /c 12 /m "Суммы совпали? 1 - да, 2 - нет"
if errorlevel 2 exit /b 1
certutil -addstore -f Root avers-root.crt
if errorlevel 1 (
  echo Не получилось. Запустите этот файл от имени администратора.
  pause
  exit /b 1
)
echo Готово. Теперь в avers.json адрес сервера пишется с https://
pause
