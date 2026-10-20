#!/usr/bin/env bash
# Выгрузка структуры схемы JACK для проекта миграции (Linux). Только чтение: в базе ничего не меняется.
#
#   ./jack-export.sh [пользователь@база]                шаги 1–2: структура, код, Data Pump
#   ./jack-export.sh --row-counts [пользователь@база]   шаг 3: точное число строк, вне рабочего времени
#
# По умолчанию подключение system@avers102. Пароль спрашивается при запуске.
# Результат: архив jack-structure-<дата>.tar.gz в этой папке. Подробно — README.md.
set -euo pipefail
cd "$(dirname "$0")"

die() { echo "Ошибка: $*" >&2; exit 1; }

row_counts=false
if [[ ${1:-} == --row-counts ]]; then row_counts=true; shift; fi
login=${1:-system@avers102}
user=${login%%@*}
db=; [[ $login == *@* ]] && db=@${login#*@}

command -v sqlplus >/dev/null || die "не найден sqlplus. Задайте окружение Oracle (например, '. oraenv') и запустите снова."

export NLS_LANG=AMERICAN_AMERICA.AL32UTF8
read -rsp "Пароль пользователя $user: " pass; echo

sql() {
  sqlplus -L -s /nolog <<EOF
whenever sqlerror exit 1
connect $user/"$pass"$db
$1
EOF
}

if $row_counts; then
  sql @04-row-counts.sql || die "SQL*Plus завершился с ошибкой, см. сообщения выше."
  echo "Готово: jack_30_row_counts.csv"
  exit 0
fi

echo "=== Шаг 1. Структура и код"
sql @00-run-all.sql || die "не удалось подключиться к базе, см. сообщения выше."

if command -v expdp >/dev/null; then
  echo "=== Шаг 2. Data Pump (expdp снова спросит пароль)"
  expdp "$login" parfile=expdp-metadata.par || echo "expdp завершился с ошибкой: пришлите jack_metadata.log и текст ошибки."

  dir=$(sql "set heading off feedback off pagesize 0
select directory_path from dba_directories where directory_name = 'DATA_PUMP_DIR';
exit" | tr -d '[:space:]')
  for f in jack_metadata.dmp jack_metadata.log; do
    if [[ -r $dir/$f ]]; then cp "$dir/$f" .
    else echo "Файл $f не скопирован: заберите его с сервера базы из каталога $dir"; fi
  done
else
  echo "=== Шаг 2 пропущен: на этом компьютере нет expdp. Его можно выполнить позже на сервере базы (README.md)."
fi

archive=jack-structure-$(date +%F).tar.gz
tar czf "$archive" jack_*
echo "Готово: $archive. Передайте этот архив разработчикам."
