-- Исходный код серверной логики схемы &owner. Только чтение. Совместимо с Oracle 10.2.
-- Запускается из 00-run-all.sql.
set heading off pagesize 0 linesize 4000 long 10000000 longchunksize 4000 trimspool on
set feedback off termout off recsep off

-- 1. Процедуры, функции, пакеты, триггеры, типы — текстом, по объектам
spool jack_10_plsql_source.sql
select decode(line, 1, chr(10) || '-- ===== ' || type || ' ' || name || chr(10)) || text
  from dba_source
 where owner = '&owner'
 order by type, name, line;
spool off

-- 2. Представления (текст)
spool jack_11_views.sql
select chr(10) || '-- ===== VIEW ' || view_name || chr(10), text
  from dba_views
 where owner = '&owner'
 order by view_name;
spool off

-- 3. Триггеры: на какие таблицы и события, включены ли
set heading on pagesize 50000 colsep ';' underline off
spool jack_12_triggers.csv
select trigger_name, table_name, trigger_type, triggering_event, status
  from dba_triggers
 where owner = '&owner'
 order by table_name, trigger_name;
spool off

-- 4. Задания по расписанию (DBMS_JOB и DBMS_SCHEDULER)
spool jack_13_jobs.csv
select job, log_user, schema_user, broken, interval, what
  from dba_jobs
 where schema_user = '&owner' or log_user = '&owner';
select job_name, job_type, job_action, repeat_interval, enabled, state
  from dba_scheduler_jobs
 where owner = '&owner';
spool off

set termout on
