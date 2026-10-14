-- Инвентаризация схемы JACK. Только чтение. Oracle 10.2.
set pagesize 50000 linesize 400 trimspool on colsep ';'

-- 1. Версия и кодировка
select banner from v$version;
select parameter, value from nls_database_parameters
 where parameter in ('NLS_CHARACTERSET','NLS_NCHAR_CHARACTERSET','NLS_LANGUAGE','NLS_TERRITORY');

-- 2. Объекты схемы по типам
select object_type, count(*) cnt, sum(decode(status,'INVALID',1,0)) invalid
  from all_objects where owner = 'JACK'
 group by object_type order by object_type;

-- 3. Таблицы и объёмы (num_rows — по статистике; точные числа см. п. 7)
select t.table_name, t.num_rows, t.last_analyzed,
       round(sum(s.bytes)/1024/1024,1) size_mb
  from all_tables t
  left join dba_segments s on s.owner = t.owner and s.segment_name = t.table_name
 where t.owner = 'JACK'
 group by t.table_name, t.num_rows, t.last_analyzed
 order by size_mb desc nulls last;

-- 4. Столбцы
select table_name, column_id, column_name, data_type, data_length, data_precision, data_scale, nullable
  from all_tab_columns where owner = 'JACK'
 order by table_name, column_id;

-- 5. Ключи и связи
select c.table_name, c.constraint_name, c.constraint_type, c.r_constraint_name,
       listagg_cols.cols
  from all_constraints c
  join (select owner, constraint_name,
               rtrim(xmlagg(xmlelement(e, column_name || ',') order by position).extract('//text()'), ',') cols
          from all_cons_columns where owner = 'JACK' group by owner, constraint_name) listagg_cols
    on listagg_cols.owner = c.owner and listagg_cols.constraint_name = c.constraint_name
 where c.owner = 'JACK' and c.constraint_type in ('P','U','R')
 order by c.table_name, c.constraint_type;

-- 6. Индексы и последовательности
select table_name, index_name, uniqueness from all_indexes where owner = 'JACK' order by table_name;
select sequence_name, last_number, increment_by from all_sequences where sequence_owner = 'JACK';

-- 7. Точные числа строк по ключевым таблицам (подставить реальные имена при необходимости)
select 'PHYSPERS' t, count(*) from jack.physpers union all
select 'JURPERS', count(*) from jack.jurpers union all
select 'PHYSPERS_SCORE', count(*) from jack.physpers_score union all
select 'BUILDING', count(*) from jack.building union all
select 'STREETS', count(*) from jack.streets union all
select 'CONNLIST', count(*) from jack.connlist union all
select 'ALL_DEMANDS', count(*) from jack.all_demands union all
select 'PAY_IMPORT', count(*) from jack.pay_import;
