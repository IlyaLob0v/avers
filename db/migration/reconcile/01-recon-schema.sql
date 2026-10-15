-- Сверка переноса Oracle JACK → PostgreSQL: служебная схема recon.
-- Повторяемый скрипт: можно запускать сколько угодно раз.
--
-- Идея. С каждой стороны снимается «отпечаток» каждой таблицы — только агрегаты, без содержимого строк:
--   rows — число строк;
--   по каждому столбцу nn — сколько непустых значений, и в зависимости от типа:
--     числа        → sum  (точная сумма; для FLOAT/double — fsum с допуском на округление),
--     даты         → min, max (до секунды),
--     строки       → len  (сумма длин без хвостовых пробелов, в символах).
-- Отпечаток Oracle снимает db/stand/oracle/fingerprint.sql, отпечаток PostgreSQL — recon.pg_fingerprint().
-- Любое расхождение (recon.diff) = перенос не принят.

create schema if not exists recon;

create table if not exists recon.fingerprint (
    side        text not null check (side in ('oracle', 'pg')),
    table_name  text not null,
    column_name text not null default '',
    metric      text not null,
    value       text,
    primary key (side, table_name, column_name, metric)
);
comment on table recon.fingerprint is 'Агрегаты по таблицам с двух сторон переноса (без персональных данных)';

-- Сырой вид для загрузки CSV из Oracle (имена в верхнем регистре, разделитель ;)
create table if not exists recon.oracle_fingerprint_raw (
    table_name  text,
    column_name text,
    metric      text,
    value       text
);

create table if not exists recon.run_log (
    id          bigint generated always as identity primary key,
    started_at  timestamptz not null default now(),
    pg_schema   text not null,
    oracle_rows integer,
    pg_rows     integer,
    diff_rows   integer,
    encoding_issue_columns integer
);
comment on table recon.run_log is 'История прогонов сверки';
