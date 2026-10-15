#!/usr/bin/env bash
# Стенд миграции Аверса: выгрузка JACK → Oracle Free 23 → Ora2Pg → PostgreSQL (схема legacy) → сверка.
# План — docs/04-db-migration-plan.md, описание — db/stand/README.md.
#
# Всё работает только с контейнерами стенда (db/docker-compose.yml, профиль legacy).
# Боевые базы (servora/avers102, PostgreSQL 192.168.37.x) этот скрипт не трогает и не умеет трогать.
#
# Windows: запускать из Git Bash (ставится вместе с Git), из корня репозитория:
#   bash db/stand/stand.sh help
set -euo pipefail
export MSYS_NO_PATHCONV=1 # Git Bash: не превращать пути контейнера (/dumps/...) в пути Windows

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE=(docker compose -f "$ROOT/db/docker-compose.yml" --profile legacy)
if [[ -f "$ROOT/.env" ]]; then COMPOSE+=(--env-file "$ROOT/.env"); fi

# Схема Oracle, которую переносим, и схема PostgreSQL, куда кладём её копию «как есть»
ORA_SCHEMA="${ORA_SCHEMA:-JACK}"
PG_SCHEMA="${PG_SCHEMA:-legacy}"
DUMPS="$ROOT/db/legacy/dumps" # в git не попадает (.gitignore)
OUT_HOST="$DUMPS/stand/$PG_SCHEMA"
OUT="/dumps/stand/$PG_SCHEMA" # то же внутри контейнеров

say() { printf '\n\033[1m== %s\033[0m\n' "$*"; }
die() {
  printf '\033[31mОШИБКА: %s\033[0m\n' "$*" >&2
  exit 1
}

check_names() {
  [[ "$ORA_SCHEMA" =~ ^[A-Za-z][A-Za-z0-9_]*$ ]] || die "странное имя схемы Oracle: $ORA_SCHEMA"
  # Схему PostgreSQL стенд пересоздаёт с нуля, поэтому разрешены только имена legacy*,
  # чтобы случайно не удалить схему новой модели.
  [[ "$PG_SCHEMA" =~ ^legacy[a-z0-9_]*$ ]] || die "схема PostgreSQL должна начинаться с legacy, а не: $PG_SCHEMA"
  ORA_SCHEMA="${ORA_SCHEMA^^}"
}

# SQL*Plus внутри контейнера Oracle под SYSTEM, текст команд — со stdin
ora_sql() {
  # shellcheck disable=SC2016 # переменная раскрывается внутри контейнера
  "${COMPOSE[@]}" exec -T -e NLS_LANG=AMERICAN_AMERICA.AL32UTF8 oracle-legacy \
    bash -c 'sqlplus -s -L "system/${ORACLE_PASSWORD}@localhost/FREEPDB1"'
}

# Команда в контейнере ora2pg (там есть ora2pg, sqlplus, psql)
tools() {
  "${COMPOSE[@]}" run --rm -T -e ORA_SCHEMA="$ORA_SCHEMA" -e PG_SCHEMA="$PG_SCHEMA" -e OUT="$OUT" ora2pg "$1"
}

# psql внутри контейнера PostgreSQL
pg_sql() {
  "${COMPOSE[@]}" exec -T postgres psql -U avers -d avers -v ON_ERROR_STOP=1 -q "$@"
}

cmd_up() {
  say "Запускаю PostgreSQL и Oracle Free (первый запуск Oracle — несколько минут)"
  mkdir -p "$DUMPS" # создать заранее: иначе Docker создаст папку от имени root
  "${COMPOSE[@]}" up -d --wait postgres oracle-legacy
  # Каталог, куда Oracle пишет журналы импорта (на Linux нужен владелец oracle)
  "${COMPOSE[@]}" exec -T -u root oracle-legacy bash -c 'mkdir -p /dumps/oracle && chown oracle /dumps/oracle'
  ora_sql <<'SQL'
whenever sqlerror exit failure
create or replace directory avers_dumps as '/dumps';
create or replace directory avers_work as '/dumps/oracle';
exit
SQL
  echo "Стенд работает."
}

# Удаляет схему на стенде Oracle перед повторным импортом
drop_ora_schema() {
  ora_sql <<SQL
whenever sqlerror exit failure
begin
  execute immediate 'drop user $ORA_SCHEMA cascade';
exception when others then
  if sqlcode != -1918 then raise; end if; -- ORA-01918: пользователя ещё нет
end;
/
exit
SQL
}

after_import() {
  say "Перекомпилирую код и считаю объекты схемы $ORA_SCHEMA на стенде"
  ora_sql <<SQL
set pagesize 100 linesize 200 feedback off
exec dbms_utility.compile_schema('$ORA_SCHEMA', false)
column object_type format a25
select object_type, count(*) objects, sum(decode(status, 'INVALID', 1, 0)) invalid
  from dba_objects where owner = '$ORA_SCHEMA' group by object_type order by 1;
exit
SQL
}

# import <файл.dmp> — выгрузка Data Pump (expdp). Файл положить в db/legacy/dumps/.
cmd_import() {
  local file="${1:-}" dir="${2:-avers_dumps}"
  [[ -n "$file" ]] || die "укажите файл выгрузки: stand.sh import jack.dmp (файл — в db/legacy/dumps/)"
  [[ "$dir" != avers_dumps || -f "$DUMPS/$file" ]] || die "нет файла $DUMPS/$file"
  cmd_up
  say "Импорт $file в Oracle Free (схема $ORA_SCHEMA пересоздаётся)"
  drop_ora_schema
  local rc=0
  "${COMPOSE[@]}" exec -T oracle-legacy bash -c \
    "impdp \"system/\${ORACLE_PASSWORD}@localhost/FREEPDB1\" directory=$dir dumpfile='$file' \
       logfile=avers_work:impdp_$ORA_SCHEMA.log schemas=$ORA_SCHEMA transform=segment_attributes:n" || rc=$?
  # impdp: 5 = завершён с предупреждениями (например, гранты несуществующим на стенде пользователям)
  if ((rc == 5)); then
    echo "impdp завершился с предупреждениями — журнал: db/legacy/dumps/oracle/impdp_$ORA_SCHEMA.log"
  elif ((rc != 0)); then
    die "impdp завершился с кодом $rc. Если выгрузка сделана старым exp, а не expdp — используйте import-exp"
  fi
  after_import
}

# import-exp <файл.dmp> — выгрузка старой утилитой exp (не Data Pump)
cmd_import_exp() {
  local file="${1:-}"
  [[ -n "$file" && -f "$DUMPS/$file" ]] || die "укажите файл из db/legacy/dumps/: stand.sh import-exp jack.dmp"
  cmd_up
  say "Импорт $file (формат exp) в Oracle Free"
  drop_ora_schema
  ora_sql <<SQL
whenever sqlerror exit failure
create user $ORA_SCHEMA identified by "Stand_only_1" quota unlimited on users;
grant connect, resource to $ORA_SCHEMA;
exit
SQL
  local rc=0
  "${COMPOSE[@]}" exec -T -e NLS_LANG=AMERICAN_AMERICA.AL32UTF8 oracle-legacy bash -c \
    "imp \"system/\${ORACLE_PASSWORD}@localhost/FREEPDB1\" file='/dumps/$file' fromuser=$ORA_SCHEMA touser=$ORA_SCHEMA \
       log=/dumps/oracle/imp_$ORA_SCHEMA.log" || rc=$?
  ((rc == 0)) || echo "imp завершился с кодом $rc (предупреждения?) — журнал: db/legacy/dumps/oracle/imp_$ORA_SCHEMA.log"
  after_import
}

# shellcheck disable=SC2016 # переменные раскрываются внутри контейнера ora2pg
ORA2PG='ora2pg -q -c /stand/ora2pg/ora2pg.conf -n "$ORA_SCHEMA" -N "$PG_SCHEMA" -w "$ORA_PASSWORD"'

# report — оценка сложности переноса (HTML)
cmd_report() {
  say "Отчёт Ora2Pg об объектах и сложности переноса"
  mkdir -p "$OUT_HOST"
  tools "$ORA2PG -t SHOW_REPORT --estimate_cost --dump_as_html > \"\$OUT/ora2pg-report.html\""
  echo "Отчёт: db/legacy/dumps/stand/$PG_SCHEMA/ora2pg-report.html"
}

# schema — структура: таблицы и последовательности в PostgreSQL, код Oracle — в файлы для разбора
cmd_schema() {
  say "Выгружаю структуру $ORA_SCHEMA через Ora2Pg"
  mkdir -p "$OUT_HOST"
  # Старые файлы удаляются внутри контейнера: на Linux они принадлежат root
  tools "set -e; rm -rf \"\$OUT/schema\" \"\$OUT/code\"; mkdir -p \"\$OUT/schema\" \"\$OUT/code\"
    $ORA2PG -t TABLE -b \"\$OUT/schema\" -o table.sql
    $ORA2PG -t SEQUENCE -b \"\$OUT/schema\" -o sequence.sql
    # Код Oracle в legacy не загружаем: черновик перевода на PL/pgSQL — только для разбора логики
    for t in VIEW PROCEDURE FUNCTION PACKAGE TRIGGER TYPE; do
      $ORA2PG -p -t \$t -b \"\$OUT/code\" -o \$(echo \$t | tr A-Z a-z).sql || echo \"(\$t: пропущено)\"
    done"
  say "Создаю схему $PG_SCHEMA в PostgreSQL (старая копия удаляется)"
  pg_sql -c "drop schema if exists $PG_SCHEMA cascade" -c "create schema $PG_SCHEMA"
  pg_sql -f "$OUT/schema/table.sql"
  pg_sql -f "$OUT/schema/sequence.sql"
  echo "Структура загружена. Файлы: db/legacy/dumps/stand/$PG_SCHEMA/schema и .../code"
}

# data — перенос строк (COPY), затем ключи, индексы и внешние ключи
cmd_data() {
  say "Переношу данные $ORA_SCHEMA → $PG_SCHEMA (Ora2Pg COPY)"
  tools "$ORA2PG -t COPY --pg_dsn 'dbi:Pg:dbname=avers;host=postgres;port=5432' --pg_user avers --pg_pwd \"\$PGPASSWORD\""
  say "Создаю первичные ключи, ограничения, индексы и внешние ключи"
  local f
  for f in CONSTRAINTS_table.sql INDEXES_table.sql FKEYS_table.sql; do
    if [[ -f "$OUT_HOST/schema/$f" ]]; then pg_sql -f "$OUT/schema/$f"; fi
  done
  pg_sql -c "analyze"
  echo "Данные перенесены."
}

# reconcile — сверка Oracle и PostgreSQL (число строк, суммы, даты, длины строк, кириллица, бухгалтерия)
cmd_reconcile() {
  say "Снимаю отпечаток Oracle (только агрегаты)"
  mkdir -p "$OUT_HOST"
  "${COMPOSE[@]}" exec -T -e NLS_LANG=AMERICAN_AMERICA.AL32UTF8 oracle-legacy bash -c \
    "sqlplus -s -L \"system/\${ORACLE_PASSWORD}@localhost/FREEPDB1\" @/stand/oracle/fingerprint.sql $ORA_SCHEMA" \
    >"$OUT_HOST/oracle_fingerprint.csv"
  say "Снимаю отпечаток PostgreSQL и сравниваю"
  local report
  report="$OUT_HOST/reconcile-$(date +%Y%m%d-%H%M%S).txt"
  local rc=0
  pg_sql -v schema="$PG_SCHEMA" -v oracle_csv="$OUT/oracle_fingerprint.csv" \
    -f /migration/reconcile/run.sql 2>&1 | tee "$report" || rc=$?
  echo "Отчёт сверки: ${report#"$ROOT/"}"
  return "$rc"
}

cmd_all() {
  local file="${1:-}"
  [[ -n "$file" ]] || die "укажите файл выгрузки: stand.sh all jack.dmp"
  cmd_import "$file"
  cmd_report
  cmd_schema
  cmd_data
  cmd_reconcile
}

# selftest — репетиция всего конвейера на синтетической схеме AVERS_DEMO (данные выдуманы)
cmd_selftest() {
  ORA_SCHEMA=AVERS_DEMO
  PG_SCHEMA=legacy_selftest
  OUT_HOST="$DUMPS/stand/$PG_SCHEMA"
  OUT="/dumps/stand/$PG_SCHEMA"
  cmd_up
  say "Создаю учебную схему AVERS_DEMO и выгружаю её через expdp"
  ora_sql <"$oracle_demo_script"
  "${COMPOSE[@]}" exec -T oracle-legacy bash -c \
    "expdp \"system/\${ORACLE_PASSWORD}@localhost/FREEPDB1\" schemas=AVERS_DEMO directory=avers_work \
       dumpfile=selftest_avers_demo.dmp logfile=expdp_selftest.log reuse_dumpfiles=y"
  cmd_import selftest_avers_demo.dmp avers_work
  cmd_report
  cmd_schema
  cmd_data
  cmd_reconcile || die "самопроверка: сверка чистого переноса не прошла"

  say "Проверяю, что сверка ловит ошибку: меняю одну сумму на 1 копейку"
  pg_sql -c "update $PG_SCHEMA.demo_payment set amount = amount + 0.01 where id = (select min(id) from $PG_SCHEMA.demo_payment)"
  if cmd_reconcile >/dev/null 2>&1; then die "самопроверка: сверка НЕ заметила испорченную сумму"; fi
  pg_sql -c "update $PG_SCHEMA.demo_payment set amount = amount - 0.01 where id = (select min(id) from $PG_SCHEMA.demo_payment)"
  echo "Сверка заметила расхождение — так и должно быть."
  say "САМОПРОВЕРКА ПРОЙДЕНА: стенд готов к настоящей выгрузке JACK"
}
oracle_demo_script="$ROOT/db/stand/oracle/demo-schema.sql"

cmd_psql() { "${COMPOSE[@]}" exec postgres psql -U avers -d avers "$@"; }
cmd_status() { "${COMPOSE[@]}" ps; }
cmd_down() { "${COMPOSE[@]}" stop; }

cmd_help() {
  cat <<'HELP'
Стенд миграции Аверса. Запуск из корня репозитория: bash db/stand/stand.sh <команда>

  selftest            репетиция на учебной схеме (без настоящих данных): проверить, что стенд работает
  all <файл.dmp>      весь перенос по порядку: import → report → schema → data → reconcile

  up                  запустить PostgreSQL и Oracle Free
  import <файл.dmp>   восстановить выгрузку expdp из db/legacy/dumps/ в Oracle Free
  import-exp <файл>   то же для выгрузки старой утилитой exp
  report              отчёт Ora2Pg о сложности переноса (HTML)
  schema              создать таблицы в PostgreSQL (схема legacy), код Oracle — в файлы для разбора
  data                перенести строки, затем ключи и индексы
  reconcile           сверка Oracle ↔ PostgreSQL; код выхода не 0 = есть расхождения
  psql                консоль PostgreSQL
  status | down       состояние | остановить контейнеры (данные сохраняются)

Переменные: ORA_SCHEMA (по умолчанию JACK), PG_SCHEMA (по умолчанию legacy).
Пароли — в .env в корне репозитория (образец .env.example). Результаты — в db/legacy/dumps/stand/ (не в git).
HELP
}

main() {
  local cmd="${1:-help}"
  shift || true
  check_names
  case "$cmd" in
    up) cmd_up ;;
    import) cmd_import "$@" ;;
    import-exp) cmd_import_exp "$@" ;;
    report) cmd_report ;;
    schema) cmd_schema ;;
    data) cmd_data ;;
    reconcile) cmd_reconcile ;;
    all) cmd_all "$@" ;;
    selftest) cmd_selftest ;;
    psql) cmd_psql "$@" ;;
    status) cmd_status ;;
    down) cmd_down ;;
    help | -h | --help) cmd_help ;;
    *) die "неизвестная команда: $cmd (см. stand.sh help)" ;;
  esac
}

main "$@"
