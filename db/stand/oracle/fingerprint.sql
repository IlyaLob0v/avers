-- Отпечаток схемы Oracle для сверки переноса: только агрегаты, без содержимого строк.
-- Формат вывода (CSV, разделитель ;): ТАБЛИЦА;СТОЛБЕЦ;МЕТРИКА;ЗНАЧЕНИЕ
-- Метрики и категории типов совпадают с recon.pg_fingerprint() (db/migration/reconcile/02-fingerprint.sql).
-- Каждая таблица просматривается один раз. Только чтение.
--
-- Запуск: sqlplus -s user/pwd@db @fingerprint.sql JACK > fingerprint.csv
-- Совместимо с Oracle 10.2: при желании администратор может снять такой же отпечаток с боевой базы
-- в момент полной выгрузки (вне рабочего времени) — тогда сверяется и сама выгрузка.
set serveroutput on size unlimited format wrapped
set feedback off heading off pagesize 0 linesize 4000 trimspool on verify off echo off termout on
whenever sqlerror exit failure

alter session set nls_numeric_characters = '.,';

declare
  v_owner  varchar2(128) := upper('&1');
  type t_list is table of varchar2(4000) index by pls_integer;
  exprs    t_list;
  names    t_list;
  n        pls_integer;
  cur      integer;
  rc       integer;
  val      varchar2(4000);
  cnt      number;

  procedure add(p_expr varchar2, p_name varchar2) is
  begin
    n := n + 1;
    exprs(n) := p_expr;
    names(n) := p_name;
  end;

  function category(p_type varchar2) return varchar2 is
  begin
    if p_type in ('NUMBER', 'INTEGER') then return 'num';
    elsif p_type in ('FLOAT', 'BINARY_FLOAT', 'BINARY_DOUBLE') then return 'float';
    elsif p_type = 'DATE' or (p_type like 'TIMESTAMP%' and p_type not like '%TIME ZONE') then return 'dt';
    elsif p_type in ('CHAR', 'NCHAR', 'VARCHAR2', 'NVARCHAR2') then return 'txt';
    elsif p_type in ('LONG', 'LONG RAW') then return 'long';
    else return 'other';
    end if;
  end;
begin
  for t in (select table_name from all_tables
             where owner = v_owner and table_name not like 'BIN$%' and nvl(temporary, 'N') = 'N'
               and (iot_type is null or iot_type = 'IOT')
             order by table_name) loop
    n := 0;
    exprs.delete;
    names.delete;
    add('to_char(count(*))', ';rows');
    for c in (select column_name, data_type from all_tab_columns
               where owner = v_owner and table_name = t.table_name
               order by column_id) loop
      declare
        q   varchar2(200) := '"' || c.column_name || '"';
        cat varchar2(10) := category(c.data_type);
      begin
        if cat = 'long' then
          -- LONG нельзя агрегировать; число непустых значений считаем отдельным запросом
          execute immediate 'select count(*) from "' || v_owner || '"."' || t.table_name || '" where ' || q || ' is not null'
            into cnt;
          dbms_output.put_line(t.table_name || ';' || c.column_name || ';nn;' || cnt);
        else
          if cat = 'other' then
            -- LOB и прочие типы: count(столбец) для них не работает, считаем через is not null
            add('to_char(count(case when ' || q || ' is not null then 1 end))', c.column_name || ';nn');
          else
            add('to_char(count(' || q || '))', c.column_name || ';nn');
          end if;
          if cat = 'num' then
            add('to_char(sum(' || q || '), ''TM9'')', c.column_name || ';sum');
          elsif cat = 'float' then
            add('to_char(sum(' || q || '), ''TM9'')', c.column_name || ';fsum');
          elsif cat = 'dt' then
            add('to_char(min(' || q || '), ''YYYY-MM-DD HH24:MI:SS'')', c.column_name || ';min');
            add('to_char(max(' || q || '), ''YYYY-MM-DD HH24:MI:SS'')', c.column_name || ';max');
          elsif cat = 'txt' then
            add('to_char(sum(length(rtrim(' || q || '))))', c.column_name || ';len');
          end if;
        end if;
      end;
    end loop;

    declare
      sql_text varchar2(32767) := 'select ';
    begin
      for i in 1 .. n loop
        sql_text := sql_text || case when i > 1 then ', ' end || exprs(i);
      end loop;
      sql_text := sql_text || ' from "' || v_owner || '"."' || t.table_name || '"';
      cur := dbms_sql.open_cursor;
      dbms_sql.parse(cur, sql_text, dbms_sql.native);
      for i in 1 .. n loop
        dbms_sql.define_column(cur, i, val, 4000);
      end loop;
      rc := dbms_sql.execute_and_fetch(cur);
      for i in 1 .. n loop
        dbms_sql.column_value(cur, i, val);
        dbms_output.put_line(t.table_name || ';' || names(i) || ';' || val);
      end loop;
      dbms_sql.close_cursor(cur);
    exception when others then
      if dbms_sql.is_open(cur) then dbms_sql.close_cursor(cur); end if;
      dbms_output.put_line(t.table_name || ';;error;' || replace(sqlerrm, ';', ','));
    end;
  end loop;
end;
/
exit
