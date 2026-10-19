#!/usr/bin/env bash
# Ставит сервер Аверса и базу PostgreSQL на Ubuntu 24.04. Можно на тот же сервер, что и xrdp.
# Запуск из распакованной сборки Avers-server-linux-x64: sudo bash install.sh
# Повторный запуск обновляет программу; база, пароль к ней и копии остаются.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
src=${1:-$here/avers-server}
[ -f "$src/Avers.Server" ] || { echo "В папке $src нет файла Avers.Server"; exit 1; }
[ "$(id -u)" -eq 0 ] || { echo "Запустите через sudo"; exit 1; }

apt-get update
apt-get install -y postgresql
systemctl enable --now postgresql

id avers >/dev/null 2>&1 || useradd --system --home-dir /opt/avers-server --shell /usr/sbin/nologin avers

# Пароль к базе придумывается один раз и хранится только в /etc/avers-server.env.
env_file=/etc/avers-server.env
if [ ! -f "$env_file" ]; then
    db_password=$(openssl rand -hex 24)
    su postgres -c "psql -v ON_ERROR_STOP=1 -q" <<SQL
CREATE ROLE avers LOGIN PASSWORD '$db_password';
CREATE DATABASE avers OWNER avers ENCODING 'UTF8' TEMPLATE template0;
SQL
    install -m 640 -o root -g avers /dev/null "$env_file"
    cat > "$env_file" <<ENV
ConnectionStrings__Avers=Host=localhost;Database=avers;Username=avers;Password=$db_password
ASPNETCORE_URLS=http://0.0.0.0:5115
Avers__Database__SeedDemo=true
ENV
fi

systemctl stop avers-server 2>/dev/null || true
rm -rf /opt/avers-server
cp -r "$src" /opt/avers-server
chown -R root:root /opt/avers-server
chmod 755 /opt/avers-server/Avers.Server

install -m 644 "$here/avers-server.service" /etc/systemd/system/avers-server.service
install -m 755 "$here/avers-password" /usr/local/bin/avers-password
install -m 755 "$here/avers-backup" /etc/cron.daily/avers-backup
install -d -m 750 -o postgres -g postgres /var/backups/avers

systemctl daemon-reload
systemctl enable avers-server
systemctl restart avers-server

if command -v ufw >/dev/null && ufw status | grep -q "Status: active"; then
    ufw allow 5115/tcp
fi

for _ in $(seq 30); do
    if curl -fsS http://localhost:5115/health >/dev/null 2>&1; then
        echo "Сервер Аверса работает: http://$(hostname -I | awk '{print $1}'):5115"
        echo "Смените пароли сотрудников: sudo avers-password <логин>"
        exit 0
    fi
    sleep 1
done
echo "Сервер не ответил. Журнал: sudo journalctl -u avers-server -n 50"
exit 1
