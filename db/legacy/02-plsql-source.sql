-- Исходный код серверной логики схемы JACK. Только чтение.
set pagesize 0 linesize 4000 long 1000000 trimspool on

-- Процедуры, функции, пакеты, триггеры
select type, name, line, text from all_source
 where owner = 'JACK' order by type, name, line;

-- Представления
select view_name, text from all_views where owner = 'JACK';

-- Триггеры: на какие таблицы и события
select trigger_name, table_name, triggering_event, status from all_triggers where owner = 'JACK';

-- Задания по расписанию
select job, what, interval, broken from dba_jobs where schema_user = 'JACK';
