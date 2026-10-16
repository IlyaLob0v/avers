#!/usr/bin/env bash
# Ставит Аверс на сервер Ubuntu 24.04 для работы через удалённый рабочий стол (xrdp).
# Запуск из распакованной сборки Avers-linux-x64: sudo bash install.sh
# Повторный запуск обновляет программу; перед этим все сотрудники должны закрыть Аверс.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
src=${1:-$here/Avers}
[ -f "$src/Avers" ] || { echo "В папке $src нет файла Avers"; exit 1; }

apt-get update
apt-get install -y xrdp xorgxrdp openbox locales fonts-dejavu-core
locale-gen ru_RU.UTF-8

rm -rf /opt/avers
cp -r "$src" /opt/avers
chmod 755 /opt/avers/Avers

install -m 755 "$here/startwm.sh" /etc/xrdp/startwm.sh
adduser xrdp ssl-cert
systemctl enable xrdp
systemctl restart xrdp

echo "Аверс установлен в /opt/avers. Учётная запись для сотрудника: sudo adduser <логин>"
