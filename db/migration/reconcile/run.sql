-- Прогон сверки на стенде. Вызывает db/stand/stand.sh reconcile, вручную:
--   psql -v schema=legacy -v oracle_csv=/dumps/stand/legacy/oracle_fingerprint.csv -f run.sql
-- Перед этим Oracle-сторона снимается скриптом db/stand/oracle/fingerprint.sql.
\set ON_ERROR_STOP 1
\pset footer off
set client_min_messages = warning;
\if :{?require_business_checks}
\else
\set require_business_checks true
\endif

\ir 01-recon-schema.sql
\ir 02-fingerprint.sql
\ir 03-encoding.sql
\ir 04-business-checks.sql

truncate recon.oracle_fingerprint_raw;
-- \copy не подставляет переменные psql, поэтому команда собирается в переменную
\set copy_cmd '\\copy recon.oracle_fingerprint_raw from ' :'oracle_csv' ' with (format csv, delimiter '';'')'
:copy_cmd

select recon.load_oracle_fingerprint() as "метрик из Oracle", recon.pg_fingerprint(:'schema') as "таблиц в PostgreSQL";

\echo
\echo '=== Таблицы: число строк и расхождений'
select table_name as "таблица", oracle_rows as "строк в Oracle", pg_rows as "строк в PostgreSQL",
       differences as "расхождений"
  from recon.table_summary;

\echo
\echo '=== Расхождения (первые 200)'
select table_name as "таблица", column_name as "столбец", metric as "метрика",
       oracle_value as "Oracle", pg_value as "PostgreSQL", problem as "что не так"
  from recon.diff
 order by 1, 2, 3
 limit 200;

\echo
\echo '=== Кириллица: столбцы с подозрительными значениями (только счётчики)'
create temp table encoding_result as select * from recon.encoding_issues(:'schema');
select table_name as "таблица", column_name as "столбец", question as "знаки ?",
       replacement as "символ �", mojibake as "кракозябры"
  from encoding_result;

\echo
\echo '=== Сверка с бухгалтерией'
create temp table business_result as select * from recon.run_business_checks();
select title as "проверка", period as "период", expected as "эталон", actual as "в базе",
       difference as "разница", status as "статус"
  from business_result;

insert into recon.run_log (pg_schema, oracle_rows, pg_rows, diff_rows, encoding_issue_columns)
select :'schema',
       (select count(*) from recon.fingerprint where side = 'oracle'),
       (select count(*) from recon.fingerprint where side = 'pg'),
       (select count(*) from recon.diff),
       (select count(*) from encoding_result);

-- Пустая сторона — это не «расхождений нет», а несостоявшаяся сверка
select count(*) filter (where side = 'oracle') = 0 or count(*) filter (where side = 'pg') = 0 as side_empty
  from recon.fingerprint \gset
\if :side_empty
\echo 'ИТОГ: сверять нечего — отпечаток Oracle или PostgreSQL пуст (проверьте имена схем).'
do $$ begin raise exception 'Сверка не выполнена'; end $$;
\endif
\if :require_business_checks
select coalesce(bool_and(status = 'совпадает'), false) as business_ok from business_result \gset
\if :business_ok
\else
do $$ begin raise exception 'Бухгалтерская сверка не пройдена: заполните контрольные запросы и устраните расхождения'; end $$;
\endif
\endif
select count(*) as diff_count from recon.diff \gset
select count(*) as error_count from recon.fingerprint where metric = 'error' \gset
select count(*) as encoding_count from encoding_result \gset
\echo
select (:diff_count + :error_count + :encoding_count) = 0 as recon_ok \gset
\if :recon_ok
\if :require_business_checks
\echo 'ИТОГ: расхождений нет.'
\else
\echo 'ИТОГ: техническая сверка пройдена; бухгалтерская приёмка не выполнялась.'
\endif
\else
\echo 'ИТОГ: есть расхождения — перенос не принят. Подробности выше.'
do $$ begin raise exception 'Сверка не пройдена'; end $$;
\endif
