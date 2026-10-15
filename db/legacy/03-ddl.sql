-- DDL всей схемы &owner через DBMS_METADATA. Только чтение. Совместимо с Oracle 10.2.
-- Запускается из 00-run-all.sql. Ссылки на другие базы (DB_LINK) сюда не попадают — в них хеши паролей.
set heading off pagesize 0 linesize 32767 long 100000000 longchunksize 32767 trimspool on
set feedback off termout off

begin
  dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'SQLTERMINATOR', true);
  dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'PRETTY', true);
  dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'STORAGE', false);
  dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'SEGMENT_ATTRIBUTES', false);
end;
/

spool jack_20_ddl.sql
select dbms_metadata.get_ddl(replace(object_type, ' ', '_'), object_name, owner)
  from dba_objects
 where owner = '&owner'
   and object_name not like 'BIN$%'
   and generated = 'N'
   and object_type in ('SEQUENCE', 'TABLE', 'INDEX', 'VIEW', 'MATERIALIZED VIEW', 'SYNONYM',
                       'TYPE', 'FUNCTION', 'PROCEDURE', 'PACKAGE', 'TRIGGER')
 order by decode(object_type, 'SEQUENCE', 1, 'TYPE', 2, 'TABLE', 3, 'INDEX', 4, 'VIEW', 5,
                 'MATERIALIZED VIEW', 6, 'SYNONYM', 7, 'FUNCTION', 8, 'PROCEDURE', 9, 'PACKAGE', 10, 11),
          object_name;
spool off

set termout on
