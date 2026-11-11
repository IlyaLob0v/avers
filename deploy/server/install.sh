#!/usr/bin/env bash
# Ставит сервер Аверса и базу PostgreSQL на Ubuntu 24.04. Можно на тот же сервер, что и xrdp.
# Запуск из распакованной сборки Avers-server-linux-x64: sudo bash install.sh
# Повторный запуск обновляет программу; база, пароль к ней и копии остаются, перед обновлением делается копия базы.
# Адреса, по которым сотрудники открывают сервер (попадут в сертификат HTTPS), — первый IP и имя сервера;
# свои можно задать так: sudo AVERS_ADDRESSES="192.168.1.10 avers-srv" bash install.sh
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
src=${1:-$here/avers-server}
[ -f "$src/Avers.Server" ] || { echo "В папке $src нет файла Avers.Server"; exit 1; }
[ "$(id -u)" -eq 0 ] || { echo "Запустите через sudo"; exit 1; }

apt-get update
apt-get install -y postgresql caddy rsync libfontconfig1
systemctl enable --now postgresql

id avers >/dev/null 2>&1 || useradd --system --home-dir /opt/avers-server --shell /usr/sbin/nologin avers

install -m 755 "$here/avers-password" /usr/local/bin/avers-password
install -m 755 "$here/avers-demo-check" /usr/local/bin/avers-demo-check
install -m 755 "$here/avers-internet" /usr/local/bin/avers-internet
install -m 755 "$here/avers-map" /usr/local/bin/avers-map
install -m 755 "$here/avers-backup" /usr/local/bin/avers-backup
install -m 755 "$here/avers-restore-check" /usr/local/bin/avers-restore-check
install -d -m 700 -o root -g root /var/backups/avers
[ -f /etc/avers-backup.env ] || install -m 600 "$here/avers-backup.env" /etc/avers-backup.env

# Пароль к базе придумывается один раз и хранится только в /etc/avers-server.env.
env_file=/etc/avers-server.env
first_install=false
if [ ! -f "$env_file" ]; then
    first_install=true
    db_password=$(openssl rand -hex 24)
    su postgres -c "psql -v ON_ERROR_STOP=1 -q" <<SQL
CREATE ROLE avers LOGIN PASSWORD '$db_password';
CREATE DATABASE avers OWNER avers ENCODING 'UTF8' TEMPLATE template0;
SQL
    install -m 640 -o root -g avers /dev/null "$env_file"
    echo "ConnectionStrings__Avers=Host=localhost;Database=avers;Username=avers;Password=$db_password" > "$env_file"
fi

# Учебные абоненты и сотрудники с паролем avers рабочему серверу не нужны.
if grep -q '^Avers__Database__SeedDemo=' "$env_file"; then
    sed -i '/^Avers__Database__SeedDemo=/d' "$env_file"
    seed_demo_removed=true
else
    seed_demo_removed=false
fi

# Сервер слушает только этот компьютер; сотрудники приходят через Caddy по HTTPS (порт 443).
was_plain_http=false
grep -q '^ASPNETCORE_URLS=http://0.0.0.0:5115$' "$env_file" && was_plain_http=true
sed -i '/^ASPNETCORE_URLS=/d' "$env_file"
echo "ASPNETCORE_URLS=http://127.0.0.1:5115" >> "$env_file"

systemctl stop avers-server 2>/dev/null || true
if [ "$first_install" = false ]; then
    # Новая версия при запуске меняет таблицы. Без копии откатиться на старую версию было бы не с чем.
    echo "Копия базы перед обновлением…"
    if ! /usr/local/bin/avers-backup pre-update; then
        systemctl start avers-server || true
        echo "Копия не получилась, обновление отменено, работает прежняя версия."
        exit 1
    fi
fi

rm -rf /opt/avers-server
cp -r "$src" /opt/avers-server
chown -R root:root /opt/avers-server
chmod 755 /opt/avers-server/Avers.Server

install -m 644 "$here/avers-server.service" /etc/systemd/system/avers-server.service
for unit in avers-backup.service avers-backup.timer avers-restore-check.service avers-restore-check.timer; do
    install -m 644 "$here/$unit" "/etc/systemd/system/$unit"
done
# Раньше ночная копия запускалась из cron; теперь — таймер avers-backup.timer.
rm -f /etc/cron.daily/avers-backup

systemctl daemon-reload
systemctl enable avers-server
systemctl enable --now avers-backup.timer avers-restore-check.timer
systemctl restart avers-server

# HTTPS в офисе: Caddy со своим центром сертификации (tls internal). Корневой сертификат раздаётся на рабочие места.
office=/etc/caddy/avers-office.caddy
addresses=${AVERS_ADDRESSES:-$(sed -n 's/^# Адреса: //p' "$office" 2>/dev/null || true)}
addresses=${addresses:-"$(hostname -I | awk '{print $1}') $(hostname)"}
sites=""
for a in $addresses; do sites="$sites${sites:+, }https://$a"; done
# Сайт механиков от прежних версий avers-internet лежал прямо в Caddyfile — переносим в отдельный файл.
if [ -f /etc/caddy/Caddyfile ] && grep -q 'X-Avers-Internet' /etc/caddy/Caddyfile && [ ! -f /etc/caddy/avers-internet.caddy ]; then
    mv /etc/caddy/Caddyfile /etc/caddy/avers-internet.caddy
fi
cat > /etc/caddy/Caddyfile <<'CADDY'
{
    # Корневой сертификат кладёт в хранилище сервера install.sh, на рабочие места — администратор (docs/09-server.md).
    skip_install_trust
}
import /etc/caddy/avers-*.caddy
CADDY
cat > "$office" <<CADDY
# Адреса: $addresses
$sites {
    tls internal
    # Только из локальной сети: если роутер пробросил 443 для страницы механиков, из интернета сюда не попасть.
    @outside not remote_ip 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16 127.0.0.0/8 ::1/128 fc00::/7
    respond @outside 403
    reverse_proxy 127.0.0.1:5115
}
CADDY
systemctl enable caddy
systemctl restart caddy

caddy_root=/var/lib/caddy/.local/share/caddy/pki/authorities/local/root.crt
root_cert=/usr/local/share/ca-certificates/avers-root.crt
for _ in $(seq 30); do [ -f "$caddy_root" ] && break; sleep 1; done
if [ -f "$caddy_root" ]; then
    install -m 644 "$caddy_root" "$root_cert"
    update-ca-certificates >/dev/null
else
    echo "Caddy не создал корневой сертификат. Журнал: sudo journalctl -u caddy -n 50"
fi

if command -v ufw >/dev/null && ufw status | grep -q "Status: active"; then
    ufw allow 443/tcp
    ufw delete allow 5115/tcp >/dev/null 2>&1 || true
fi

first_address=${addresses%% *}
for _ in $(seq 30); do
    if curl -fsS http://127.0.0.1:5115/health >/dev/null 2>&1; then
        echo
        echo "Сервер Аверса работает. Адрес для программ (avers.json): https://$first_address"
        if [ -f "$root_cert" ]; then
            echo "Корневой сертификат для рабочих мест: $root_cert"
            echo "  скачать на Windows: scp ${SUDO_USER:-пользователь}@$first_address:$root_cert ."
            echo "  контрольная сумма SHA256: $(sha256sum "$root_cert" | awk '{print $1}')"
            curl -fsS --cacert "$root_cert" "https://$first_address/health" >/dev/null 2>&1 \
                || echo "  ВНИМАНИЕ: https://$first_address/health не отвечает. Журнал: sudo journalctl -u caddy -n 50"
        fi
        if [ "$was_plain_http" = true ]; then
            echo
            echo "ВНИМАНИЕ: раньше программы подключались по http://…:5115, теперь этот порт закрыт."
            echo "На каждом рабочем месте поставьте корневой сертификат и замените адрес в avers.json на https://$first_address"
            echo "(docs/09-server.md, «Переход с http на https»)."
        fi
        if [ "$seed_demo_removed" = true ]; then
            echo
            echo "ВНИМАНИЕ: в $env_file были включены учебные данные (Avers__Database__SeedDemo), строка убрана."
            echo "Уже залитые учебные абоненты и сотрудники остались в базе. Проверьте учебные пароли:"
            avers-demo-check || true
            echo "Учебные абоненты — все абоненты, заведённые до переноса данных из старой базы (docs/09-server.md)."
        fi
        if [ "$first_install" = true ]; then
            echo
            echo "База пустая. Придумайте пароль администратора (логин admin):"
            avers-password admin || echo "Пароль не задан. Задайте его позже: sudo avers-password admin"
        fi
        exit 0
    fi
    sleep 1
done
echo "Сервер не ответил. Журнал: sudo journalctl -u avers-server -n 50"
exit 1
