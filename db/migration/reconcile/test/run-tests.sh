#!/usr/bin/env bash
# Автотесты сверки (db/migration/reconcile) на чистом PostgreSQL, без Oracle.
# Подключение — стандартными переменными PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE.
#   PGHOST=localhost PGUSER=avers PGPASSWORD=avers_dev PGDATABASE=avers bash db/migration/reconcile/test/run-tests.sh
set -euo pipefail
export PGOPTIONS="${PGOPTIONS:-} -c client_min_messages=warning"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "== 1. Функции сверки"
psql -X -q -v ON_ERROR_STOP=1 -f "$here/test.sql"

echo "== 2. Полный прогон run.sql: одинаковые данные → код 0"
psql -X -q -v ON_ERROR_STOP=1 <<'SQL'
drop schema if exists legacy_e2e cascade;
create schema legacy_e2e;
create table legacy_e2e.street (id integer primary key, name varchar(50), created date);
insert into legacy_e2e.street values (1, 'Учебная', '2001-02-03'), (2, 'Тестовая', null);
SQL
# Отпечаток «Oracle» в формате db/stand/oracle/fingerprint.sql
cat >"$work/oracle.csv" <<'CSV'
STREET;;rows;2
STREET;ID;nn;2
STREET;ID;sum;3
STREET;NAME;nn;2
STREET;NAME;len;15
STREET;CREATED;nn;1
STREET;CREATED;min;2001-02-03 00:00:00
STREET;CREATED;max;2001-02-03 00:00:00
CSV
psql -X -q -v schema=legacy_e2e -v oracle_csv="$work/oracle.csv" -f "$here/../run.sql" >"$work/ok.txt"
grep -q 'ИТОГ: расхождений нет' "$work/ok.txt"

echo "== 3. Полный прогон run.sql: расхождение → код не 0"
psql -X -q -c "update legacy_e2e.street set name = 'Учебна' where id = 1"
if psql -X -q -v schema=legacy_e2e -v oracle_csv="$work/oracle.csv" -f "$here/../run.sql" >"$work/bad.txt" 2>&1; then
  echo "ОШИБКА: run.sql не заметил расхождение" >&2
  exit 1
fi
grep -q 'ИТОГ: есть расхождения' "$work/bad.txt"

echo "== 4. Пустая сторона → код не 0"
if psql -X -q -v schema=legacy_nothing -v oracle_csv="$work/oracle.csv" -f "$here/../run.sql" >"$work/empty.txt" 2>&1; then
  echo "ОШИБКА: run.sql принял сверку с пустой стороной" >&2
  exit 1
fi
grep -q 'сверять нечего' "$work/empty.txt"

psql -X -q -c "drop schema legacy_e2e cascade" -c "drop schema recon cascade"
echo "Все тесты сверки пройдены."
