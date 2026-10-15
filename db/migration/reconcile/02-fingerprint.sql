-- Отпечаток PostgreSQL-стороны и сравнение с Oracle. Повторяемый скрипт.

-- Категория столбца по типу PostgreSQL. Должна совпадать с категориями в db/stand/oracle/fingerprint.sql.
create or replace function recon.pg_type_category(p_type text) returns text
language sql immutable as $$
    select case
        when p_type in ('smallint', 'integer', 'bigint', 'numeric') then 'num'
        when p_type in ('real', 'double precision') then 'float'
        when p_type in ('date', 'timestamp without time zone') then 'dt'
        when p_type in ('character', 'character varying') then 'txt'
        else 'other'
    end
$$;

-- Снимает отпечаток всех таблиц схемы p_schema в recon.fingerprint (side = 'pg').
-- Каждая таблица просматривается один раз.
create or replace function recon.pg_fingerprint(p_schema text) returns integer
language plpgsql as $$
declare
    t      record;
    c      record;
    exprs  text[];
    names  text[];
    q      text;
    vals   text[];
    n      integer := 0;
begin
    delete from recon.fingerprint where side = 'pg';
    for t in
        select table_name from information_schema.tables
         where table_schema = p_schema and table_type = 'BASE TABLE'
         order by table_name
    loop
        exprs := array['count(*)::text'];
        names := array['|rows'];
        for c in
            select column_name, data_type from information_schema.columns
             where table_schema = p_schema and table_name = t.table_name
             order by ordinal_position
        loop
            exprs := exprs || format('count(%I)::text', c.column_name);
            names := names || (c.column_name || '|nn');
            case recon.pg_type_category(c.data_type)
                when 'num' then
                    exprs := exprs || format('sum(%I)::text', c.column_name);
                    names := names || (c.column_name || '|sum');
                when 'float' then
                    exprs := exprs || format('sum(%I::numeric)::text', c.column_name);
                    names := names || (c.column_name || '|fsum');
                when 'dt' then
                    exprs := exprs || format('to_char(min(%I), ''YYYY-MM-DD HH24:MI:SS'')', c.column_name)
                                   || format('to_char(max(%I), ''YYYY-MM-DD HH24:MI:SS'')', c.column_name);
                    names := names || (c.column_name || '|min') || (c.column_name || '|max');
                when 'txt' then
                    exprs := exprs || format('sum(length(rtrim(%I)))::text', c.column_name);
                    names := names || (c.column_name || '|len');
                else
                    null;
            end case;
        end loop;
        q := format('select array[%s] from %I.%I', array_to_string(exprs, ', '), p_schema, t.table_name);
        execute q into vals;
        insert into recon.fingerprint (side, table_name, column_name, metric, value)
        select 'pg', t.table_name, split_part(nm, '|', 1), split_part(nm, '|', 2), v
          from unnest(names, vals) as u(nm, v);
        n := n + 1;
    end loop;
    return n;
end
$$;

-- Переносит загруженный CSV Oracle в recon.fingerprint (side = 'oracle'), приводя имена к нижнему регистру
-- (Ora2Pg с PRESERVE_CASE 0 делает так же).
create or replace function recon.load_oracle_fingerprint() returns integer
language plpgsql as $$
declare
    n integer;
begin
    delete from recon.fingerprint where side = 'oracle';
    insert into recon.fingerprint (side, table_name, column_name, metric, value)
    select 'oracle', lower(trim(table_name)), lower(trim(coalesce(column_name, ''))), trim(metric),
           nullif(trim(value), '')
      from recon.oracle_fingerprint_raw
     where trim(coalesce(table_name, '')) <> '';
    get diagnostics n = row_count;
    return n;
end
$$;

-- Равны ли два значения метрики
create or replace function recon.same_value(p_metric text, a text, b text) returns boolean
language plpgsql immutable as $$
begin
    if a is null or b is null then
        return a is not distinct from b;
    end if;
    if p_metric in ('rows', 'nn', 'sum', 'len') then
        return a::numeric = b::numeric;
    end if;
    if p_metric = 'fsum' then
        -- FLOAT/double: допускаем погрешность округления двоичной дроби
        return abs(a::numeric - b::numeric) <= 1e-9 * greatest(1, abs(a::numeric));
    end if;
    return a = b;
end
$$;

-- Все расхождения: значение отличается или метрика есть только с одной стороны
create or replace view recon.diff as
select coalesce(o.table_name, p.table_name)   as table_name,
       coalesce(o.column_name, p.column_name) as column_name,
       coalesce(o.metric, p.metric)           as metric,
       o.value                                as oracle_value,
       p.value                                as pg_value,
       case
           when o.metric is null then 'есть только в PostgreSQL'
           when p.metric is null then 'есть только в Oracle'
           else 'значения различаются'
       end                                    as problem
  from (select * from recon.fingerprint where side = 'oracle') o
  full join (select * from recon.fingerprint where side = 'pg') p
    on p.table_name = o.table_name and p.column_name = o.column_name and p.metric = o.metric
 where o.metric is null or p.metric is null or not recon.same_value(o.metric, o.value, p.value);

-- Краткая сводка по таблицам
create or replace view recon.table_summary as
select t.table_name,
       max(case when f.side = 'oracle' and f.metric = 'rows' then f.value end) as oracle_rows,
       max(case when f.side = 'pg' and f.metric = 'rows' then f.value end)     as pg_rows,
       (select count(*) from recon.diff d where d.table_name = t.table_name)    as differences
  from (select distinct table_name from recon.fingerprint) t
  join recon.fingerprint f on f.table_name = t.table_name
 group by t.table_name
 order by t.table_name;
