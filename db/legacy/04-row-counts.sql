-- =====================================================================
-- Точное число строк в каждой таблице схемы JACK. Только чтение.
-- ВНИМАНИЕ: просматривает каждую таблицу целиком — запускать вне рабочего времени.
-- Выгружаются только числа, без содержимого таблиц.
--
--   set NLS_LANG=AMERICAN_AMERICA.AL32UTF8
--   sqlplus system@avers102 @04-row-counts.sql
-- =====================================================================
whenever sqlerror continue
set verify off feedback off echo off
set pagesize 50000 linesize 400 trimspool on colsep ';' underline off
define owner = JACK

spool jack_30_row_counts.csv
select table_name,
       to_number(extractvalue(xmltype(dbms_xmlgen.getxml(
         'select count(*) c from "' || owner || '"."' || table_name || '"')), '/ROWSET/ROW/C')) row_count
  from dba_tables
 where owner = '&owner' and table_name not like 'BIN$%' and nvl(temporary, 'N') = 'N'
 order by table_name;
spool off
exit
