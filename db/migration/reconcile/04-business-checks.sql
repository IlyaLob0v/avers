-- Сверка с цифрами бухгалтерии. Повторяемый скрипт.
--
-- Эталоны — из бумажной сверки бухгалтерии (docs/04-db-migration-plan.md, раздел 5).
-- Запрос, который считает ту же цифру по перенесённым данным, пока НЕ ЗАДАН: структура JACK неизвестна.
-- Его впишут после разбора DDL (db/migration/mapping.md). До этого проверки помечаются «запрос не задан».
-- Запрос должен вернуть одно число, например:
--   update recon.business_check set query = 'select sum(...) from legacy.... where ...' where code = 'oek_2026_08';

create table if not exists recon.business_check (
    code     text primary key,
    title    text not null,
    period   text not null,
    expected numeric(14, 2) not null,
    query    text,
    note     text
);
comment on table recon.business_check is 'Эталонные суммы бухгалтерии и запросы, которые должны их повторить';

insert into recon.business_check (code, title, period, expected, note) values
    ('oek_2026_08',      'Сверка ОЭК',      '2026-08', 4914739.57, 'Бумажная сверка бухгалтерии за август 2026'),
    ('physical_2026_08', 'Физлица',         '2026-08', 5447224.99, 'Бумажная сверка бухгалтерии за август 2026'),
    ('total_2026_08',    'Итого',           '2026-08', 5669697.49, 'Бумажная сверка бухгалтерии за август 2026')
on conflict (code) do nothing;

create or replace function recon.run_business_checks()
returns table (code text, title text, period text, expected numeric, actual numeric, difference numeric, status text)
language plpgsql as $$
declare
    b record;
begin
    for b in select * from recon.business_check order by code loop
        code := b.code;
        title := b.title;
        period := b.period;
        expected := b.expected;
        actual := null;
        difference := null;
        if b.query is null then
            status := 'запрос не задан';
        else
            begin
                execute b.query into actual;
                difference := actual - b.expected;
                status := case when difference = 0 then 'совпадает' else 'РАСХОЖДЕНИЕ' end;
            exception when others then
                status := 'ошибка запроса: ' || sqlerrm;
            end;
        end if;
        return next;
    end loop;
end
$$;
