-- Инвентаризация схемы &owner. Только чтение. Совместимо с Oracle 10.2.
-- Запускается из 00-run-all.sql (там задано &owner). Нужны права на словарь (DBA_*), например SYSTEM.
-- Каждый запрос пишет свой CSV (разделитель ';', первая строка — заголовки).
set pagesize 50000 linesize 32767 trimspool on trimout on colsep ';' underline off
set heading on feedback off termout off
column table_comment format a1000
column column_comment format a1000
column cols format a2000

-- 1. Версия и кодировка базы
spool jack_01_version.csv
select banner from v$version;
spool off
spool jack_01_nls.csv
select parameter, value from nls_database_parameters order by parameter;
spool off

-- 2. Объекты схемы по типам (сколько и сколько невалидных)
spool jack_02_objects_summary.csv
select object_type, count(*) cnt, sum(decode(status, 'INVALID', 1, 0)) invalid_cnt
  from dba_objects
 where owner = '&owner' and object_name not like 'BIN$%'
 group by object_type
 order by object_type;
spool off

-- 2а. Полный список объектов
spool jack_02_objects.csv
select object_type, object_name, status, created, last_ddl_time
  from dba_objects
 where owner = '&owner' and object_name not like 'BIN$%'
 order by object_type, object_name;
spool off

-- 3. Объём схемы целиком (решает, влезет ли копия в Oracle Free: лимит 12 ГБ данных)
spool jack_03_schema_size.csv
select segment_type, count(*) segments, round(sum(bytes) / 1024 / 1024, 1) size_mb
  from dba_segments
 where owner = '&owner'
 group by rollup(segment_type)
 order by segment_type;
spool off

-- 3а. Таблицы: число строк по статистике, дата сбора статистики, размер
spool jack_03_tables.csv
select t.table_name, t.num_rows, t.last_analyzed, t.tablespace_name, t.partitioned, t.temporary,
       round(sum(s.bytes) / 1024 / 1024, 1) size_mb
  from dba_tables t
  left join dba_segments s on s.owner = t.owner and s.segment_name = t.table_name
 where t.owner = '&owner' and t.table_name not like 'BIN$%'
 group by t.table_name, t.num_rows, t.last_analyzed, t.tablespace_name, t.partitioned, t.temporary
 order by size_mb desc nulls last, t.table_name;
spool off

-- 4. Столбцы
spool jack_04_columns.csv
select table_name, column_id, column_name, data_type, data_length, char_length, char_used,
       data_precision, data_scale, nullable, num_distinct, num_nulls
  from dba_tab_columns
 where owner = '&owner' and table_name not like 'BIN$%'
 order by table_name, column_id;
spool off

-- 4а. Комментарии к таблицам и столбцам (в кавычках: внутри могут быть ';')
spool jack_04_comments.csv
select table_name, '' column_name, '"' || replace(comments, '"', '""') || '"' table_comment
  from dba_tab_comments
 where owner = '&owner' and comments is not null
union all
select table_name, column_name, '"' || replace(comments, '"', '""') || '"'
  from dba_col_comments
 where owner = '&owner' and comments is not null
 order by 1, 2;
spool off

-- 5. Ключи и связи: P — первичный, U — уникальный, R — внешний ключ
spool jack_05_constraints.csv
select c.table_name, c.constraint_name, c.constraint_type, c.status,
       r.table_name ref_table, c.r_constraint_name, c.delete_rule, cc.cols
  from dba_constraints c
  join (select owner, constraint_name,
               rtrim(xmlagg(xmlelement(e, column_name || ',') order by position).extract('//text()').getstringval(), ',') cols
          from dba_cons_columns
         where owner = '&owner'
         group by owner, constraint_name) cc
    on cc.owner = c.owner and cc.constraint_name = c.constraint_name
  left join dba_constraints r on r.owner = c.r_owner and r.constraint_name = c.r_constraint_name
 where c.owner = '&owner' and c.constraint_type in ('P', 'U', 'R')
 order by c.table_name, c.constraint_type, c.constraint_name;
spool off

-- 6. Индексы со столбцами
spool jack_06_indexes.csv
select i.table_name, i.index_name, i.index_type, i.uniqueness, i.status, ic.cols
  from dba_indexes i
  join (select index_owner, index_name,
               rtrim(xmlagg(xmlelement(e, column_name || ',') order by column_position).extract('//text()').getstringval(), ',') cols
          from dba_ind_columns
         where index_owner = '&owner'
         group by index_owner, index_name) ic
    on ic.index_owner = i.owner and ic.index_name = i.index_name
 where i.owner = '&owner'
 order by i.table_name, i.index_name;
spool off

-- 7. Последовательности
spool jack_07_sequences.csv
select sequence_name, min_value, max_value, increment_by, cycle_flag, cache_size, last_number
  from dba_sequences
 where sequence_owner = '&owner'
 order by sequence_name;
spool off

-- 8. Зависимости: какой код какие таблицы использует
spool jack_08_dependencies.csv
select name, type, referenced_owner, referenced_name, referenced_type
  from dba_dependencies
 where owner = '&owner'
 order by type, name, referenced_name;
spool off

-- 9. Связи с внешним миром: синонимы, ссылки на другие базы (без паролей), выданные права на объекты схемы
spool jack_09_synonyms.csv
select owner, synonym_name, table_owner, table_name, db_link
  from dba_synonyms
 where owner = '&owner' or table_owner = '&owner'
 order by owner, synonym_name;
spool off
spool jack_09_db_links.csv
select owner, db_link, username, host
  from dba_db_links
 where owner in ('&owner', 'PUBLIC');
spool off
spool jack_09_grants.csv
select grantee, table_name, privilege
  from dba_tab_privs
 where owner = '&owner'
 order by grantee, table_name, privilege;
spool off

set termout on
