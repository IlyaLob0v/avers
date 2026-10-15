-- Автотест сверки без Oracle: отпечаток «Oracle» имитируется копией отпечатка PostgreSQL.
-- Данные синтетические. Запуск — run-tests.sh (локально и в CI).
\set ON_ERROR_STOP 1
set client_min_messages = warning;

\ir ../01-recon-schema.sql
\ir ../02-fingerprint.sql
\ir ../03-encoding.sql
\ir ../04-business-checks.sql

drop schema if exists legacy_test cascade;
create schema legacy_test;
create table legacy_test.payment (
    id      bigint primary key,
    paid_at timestamp(0),
    amount  numeric(12, 2),
    rate    numeric,
    ratio   double precision,
    source  varchar(20),
    kind    char(3),
    note    text
);
insert into legacy_test.payment
select i, timestamp '2026-08-01' + i * interval '1 hour', round(50 + (i * 7919 % 50000) / 100.0, 2),
       case when i % 5 = 0 then null else 85 + (i % 4) * 12.5 end, i / 3.0,
       (array['Почта', 'Сбербанк', 'ЕРЦ', 'Касса'])[i % 4 + 1], 'ул', 'Учебная заметка, д. 19Д, корп. 72/1'
  from generate_series(1, 1000) i;
create table legacy_test.empty_table (id integer, name varchar(10));

-- 1. Отпечаток PostgreSQL
do $$
begin
    assert recon.pg_fingerprint('legacy_test') = 2, 'ожидалось 2 таблицы';
    assert (select value from recon.fingerprint where side = 'pg' and table_name = 'payment' and metric = 'rows') = '1000';
    assert (select value from recon.fingerprint where side = 'pg' and table_name = 'payment'
             and column_name = 'amount' and metric = 'sum')::numeric
           = (select sum(amount) from legacy_test.payment), 'сумма amount';
    assert (select value from recon.fingerprint where side = 'pg' and table_name = 'payment'
             and column_name = 'paid_at' and metric = 'min') = '2026-08-01 01:00:00', 'min даты';
    assert exists (select from recon.fingerprint where side = 'pg' and column_name = 'ratio' and metric = 'fsum');
    assert exists (select from recon.fingerprint where side = 'pg' and column_name = 'kind' and metric = 'len');
    assert not exists (select from recon.fingerprint where side = 'pg' and column_name = 'note' and metric = 'len'),
           'text (CLOB) — только nn';
    assert (select value from recon.fingerprint where side = 'pg' and table_name = 'empty_table'
             and column_name = 'name' and metric = 'len') is null, 'пустая таблица: сумма = NULL';
end $$;

-- 2. «Oracle» = тот же отпечаток в верхнем регистре, как его выдаёт fingerprint.sql; float — в другом формате
truncate recon.oracle_fingerprint_raw;
insert into recon.oracle_fingerprint_raw
select upper(table_name), upper(column_name), metric,
       case when metric = 'fsum' then (value::numeric + 1e-12)::text
            when metric in ('rows', 'nn') then lpad(value, 10) else value end
  from recon.fingerprint where side = 'pg';
do $$
begin
    perform recon.load_oracle_fingerprint();
    assert (select count(*) from recon.diff) = 0, 'одинаковые отпечатки дали расхождения';
end $$;

-- 3. Ошибка в 1 копейку и потерянная строка должны быть видны
update legacy_test.payment set amount = amount + 0.01 where id = 7;
delete from legacy_test.payment where id = 8;
do $$
begin
    perform recon.pg_fingerprint('legacy_test');
    assert exists (select from recon.diff where table_name = 'payment' and column_name = 'amount' and metric = 'sum'),
           'не замечена ошибка в копейку';
    assert exists (select from recon.diff where table_name = 'payment' and metric = 'rows'), 'не замечена потеря строки';
end $$;

-- 4. Таблица или столбец есть только с одной стороны
alter table legacy_test.payment add column extra integer;
do $$
begin
    perform recon.pg_fingerprint('legacy_test');
    assert exists (select from recon.diff where column_name = 'extra' and problem = 'есть только в PostgreSQL');
end $$;

-- 5. Кириллица
create table legacy_test.names (id integer, full_name varchar(100), note text);
insert into legacy_test.names values
    (1, 'Ёлкина Анна Сергеевна', 'ул. Учебная, д. 19Д'),   -- норма
    (2, 'РџРµС‚СЂРѕРІ', null),                              -- UTF-8, прочитанный как cp1251
    (3, 'ÐŸÐµÑ‚Ñ€Ð¾Ð²', null),                              -- UTF-8, прочитанный как latin1
    (4, 'Петр??', 'Сидоров ?ван'),                          -- потерянные символы
    (5, U&'\FFFDетров', null),                              -- битые байты
    (6, 'Семёнов Сёма', 'Как дела?');                       -- норма (одиночный ? после пробела не считаем)
do $$
declare
    r record;
begin
    select * into strict r from recon.encoding_issues('legacy_test') where table_name = 'names' and column_name = 'full_name';
    assert r.mojibake = 2, format('кракозябры: %s', r.mojibake);
    assert r.question = 1, format('знаки ?: %s', r.question);
    assert r.replacement = 1, format('символ U+FFFD: %s', r.replacement);
    select * into strict r from recon.encoding_issues('legacy_test') where table_name = 'names' and column_name = 'note';
    assert r.question = 1 and r.mojibake = 0, 'note';
    assert not exists (select from recon.encoding_issues('legacy_test') where table_name = 'payment'),
           'ложное срабатывание на нормальной кириллице';
end $$;

-- 6. Сверка с бухгалтерией
do $$
declare
    r record;
begin
    select * into strict r from recon.run_business_checks() where code = 'oek_2026_08';
    assert r.status = 'запрос не задан';
    update recon.business_check set query = 'select 4914739.57' where code = 'oek_2026_08';
    update recon.business_check set query = 'select 1' where code = 'total_2026_08';
    select * into strict r from recon.run_business_checks() where code = 'oek_2026_08';
    assert r.status = 'совпадает', r.status;
    select * into strict r from recon.run_business_checks() where code = 'total_2026_08';
    assert r.status = 'РАСХОЖДЕНИЕ', r.status;
    update recon.business_check set query = null;
end $$;

drop schema legacy_test cascade;
\echo 'test.sql: все проверки пройдены'
