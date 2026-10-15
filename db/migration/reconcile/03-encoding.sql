-- Проверка кириллицы после переноса. Повторяемый скрипт.
-- Выводит только число подозрительных строк по столбцам, сами значения не показывает.
--   question    — «?» вместо букв (символ не нашёлся в кодировке при выгрузке): «??», «?» внутри слова или в начале слова;
--   replacement — символ U+FFFD «�» (битые байты UTF-8);
--   mojibake    — «кракозябры»: UTF-8, прочитанный как cp1251 («РџСЂРё…»), или как latin1 («ÐŸÑ€Ð¸…»).

create or replace function recon.encoding_issues(p_schema text)
returns table (table_name text, column_name text, question bigint, replacement bigint, mojibake bigint)
language plpgsql as $$
declare
    t     record;
    c     record;
    exprs text[];
    cols  text[];
    vals  bigint[];
    i     integer;
    -- В строковых константах ниже — только шаблоны поиска.
    re_question    constant text := '\?\?|[А-Яа-яЁё]\?[А-Яа-яЁё]|(^|\s)\?[А-Яа-яЁё]';
    re_replacement constant text := U&'\FFFD';
    re_mojibake    constant text :=
        'Р[ЂЃ‚ѓ„…†‡€‰Љ‹ЊЌЋЏђ‘’“”•–—™љ›њќћџ°±µ¶·»ЅѕІіЈјЎўҐґЇї]|С[ЂЃ‚ѓ„…†‡€‰Љ‹ЊЌЋЏђ‘’“”•–—™љ›њќћџ]|[ÐÑ][' || U&'\0080' || '-' || U&'\00BF' || ']';
begin
    for t in
        select tb.table_name from information_schema.tables tb
         where tb.table_schema = p_schema and tb.table_type = 'BASE TABLE'
         order by tb.table_name
    loop
        exprs := '{}';
        cols := '{}';
        for c in
            select col.column_name from information_schema.columns col
             where col.table_schema = p_schema and col.table_name = t.table_name
               and col.data_type in ('character', 'character varying', 'text')
             order by col.ordinal_position
        loop
            cols := cols || c.column_name::text;
            exprs := exprs
                || format('count(*) filter (where %I ~ %L)', c.column_name, re_question)
                || format('count(*) filter (where strpos(%I, %L) > 0)', c.column_name, re_replacement)
                || format('count(*) filter (where %I ~ %L)', c.column_name, re_mojibake);
        end loop;
        continue when cardinality(cols) = 0;
        execute format('select array[%s] from %I.%I', array_to_string(exprs, ', '), p_schema, t.table_name)
           into vals;
        for i in 1 .. cardinality(cols) loop
            if vals[3 * i - 2] + vals[3 * i - 1] + vals[3 * i] > 0 then
                table_name := t.table_name;
                column_name := cols[i];
                question := vals[3 * i - 2];
                replacement := vals[3 * i - 1];
                mojibake := vals[3 * i];
                return next;
            end if;
        end loop;
    end loop;
end
$$;
