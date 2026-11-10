-- Сверка после переноса: legacy (копия JACK) против таблиц Аверса. Повторяемый скрипт, данные не меняет,
-- кроме таблицы итогов recon.transfer_check. Если хоть одна проверка не сошлась — завершается ошибкой.
--
-- Проверки «РАСХОЖДЕНИЕ» останавливают переезд. Строки «справка» — то, что не переносится по правилам
-- (пустые абоненты, проверки подключения, оплаты до 1998 года): их надо прочитать глазами, но они не ошибка.
-- Сверка с цифрами бухгалтерии и 50 карточек вручную — отдельно, см. docs/12-cutover-plan.md.

create schema if not exists recon;
drop table if exists recon.transfer_check;
create table recon.transfer_check (
    n       serial primary key,
    title   text not null,
    jack    numeric,
    avers   numeric,
    status  text not null
);

create temp table subscriber_ids as select "Id" as id from "Subscribers";

insert into recon.transfer_check (title, jack, avers, status)
select title, jack, avers, case when jack = avers then 'совпадает' else 'РАСХОЖДЕНИЕ' end
  from (values
    ('Улицы', (select count(*) from legacy.streets), (select count(*) from "Streets")),
    ('Дома', (select count(*) from legacy.building), (select count(*) from "Houses")),
    ('Абоненты (без пустых)',
        (select count(*) from legacy.physpers) - (select count(*) from legacy.avers_empty_client),
        (select count(*) from "Subscribers")),
    ('Заявки (без проверок подключения)',
        (select count(*) from legacy.demands where not (checkconnect = 1 and demandcode in (0, 524288))),
        (select count(*) from "Tickets")),
    ('ППР', (select count(*) from legacy.ppr_table), (select count(*) from "PprEntries")),
    ('Квитанции ПЖРЭУ с 1998 года, штук',
        (select count(*) from legacy.post_pay_attr a join subscriber_ids s on s.id = a.physicalpersonid
          where a.paydate >= date '1998-01-01'),
        (select count(*) from "UnifiedReceipts")),
    ('Оплаты с 1998 года, сумма',
        (select coalesce(sum(p.summpay), 0) from legacy.abn_pay p join subscriber_ids s on s.id = p.physicalpersonid
          where p.payfordate >= date '1998-01-01'),
        (select coalesce(sum("Amount"), 0) from "Payments")),
    ('Начисления, сумма',
        (select coalesce(sum(t), 0) from (select round(sum(a.amount), 2) as t from legacy.avers_accrual a
                                            join subscriber_ids s on s.id = a.physicalpersonid
                                           group by a.physicalpersonid, date_trunc('month', a.month)) x),
        (select coalesce(sum("Tariff"), 0) from "Payments"))
  ) v(title, jack, avers);

-- Помесячно: оплаты и начисления по каждому месяцу. Число месяцев, где не сошлось, должно быть 0.
with jack as (
    select date_trunc('month', month)::date as month, round(sum(amount), 2) as charged, 0::numeric as paid
      from legacy.avers_accrual a join subscriber_ids s on s.id = a.physicalpersonid
     group by a.physicalpersonid, 1
    union all
    select date_trunc('month', payfordate)::date, 0, summpay
      from legacy.abn_pay p join subscriber_ids s on s.id = p.physicalpersonid
     where payfordate >= date '1998-01-01'
), jack_month as (
    select month, sum(charged) as charged, sum(paid) as paid from jack group by month
), avers_month as (
    select make_date("Year", "Month", 1) as month, sum("Tariff") as charged, coalesce(sum("Amount"), 0) as paid
      from "Payments" group by 1
)
insert into recon.transfer_check (title, jack, avers, status)
select 'Месяцы, где не сошлись начисления или оплаты', 0, count(*), case when count(*) = 0 then 'совпадает' else 'РАСХОЖДЕНИЕ' end
  from jack_month j
  full join avers_month a on a.month = j.month
 where coalesce(j.charged, 0) <> coalesce(a.charged, 0) or coalesce(j.paid, 0) <> coalesce(a.paid, 0);

-- Долг (начислено − оплачено) по каждому абоненту за все перенесённые месяцы.
with jack as (
    select physicalpersonid as id, sum(t) as debt
      from (select a.physicalpersonid, round(sum(a.amount), 2) as t from legacy.avers_accrual a
             group by a.physicalpersonid, date_trunc('month', a.month)
            union all
            select physicalpersonid, -summpay from legacy.abn_pay where payfordate >= date '1998-01-01') x
     where physicalpersonid in (select id from subscriber_ids)
     group by 1
), avers as (
    select "SubscriberId" as id, sum("Tariff" - coalesce("Amount", 0)) as debt from "Payments" group by 1
)
insert into recon.transfer_check (title, jack, avers, status)
select title, jack, avers, case when jack = avers then 'совпадает' else 'РАСХОЖДЕНИЕ' end
  from (select 'Долг всех абонентов, сумма' as title,
               (select coalesce(sum(debt), 0) from jack) as jack, (select coalesce(sum(debt), 0) from avers) as avers
        union all
        select 'Абоненты, у которых не сошёлся долг', 0,
               (select count(*) from jack j full join avers a on a.id = j.id where coalesce(j.debt, 0) <> coalesce(a.debt, 0))) v;

-- Справка: что не переносится по правилам.
insert into recon.transfer_check (title, jack, avers, status) values
    ('Пустые абоненты для заявок на дом (не переносятся)', (select count(*) from legacy.avers_empty_client), null, 'справка'),
    ('Проверки подключения без заявки (не переносятся)',
        (select count(*) from legacy.demands where checkconnect = 1 and demandcode in (0, 524288)), null, 'справка'),
    ('Оплаты до 1998 года, неденоминированные (не переносятся), штук',
        (select count(*) from legacy.abn_pay where payfordate < date '1998-01-01'), null, 'справка'),
    ('Платежи в 0 рублей (месяц считается оплаченным)',
        (select count(*) from legacy.abn_pay where summpay = 0 and payfordate >= date '1998-01-01'), null, 'справка'),
    ('Уволенные механики (не переносятся)',
        (select count(*) from legacy.workman where sackingdate is not null), null, 'справка'),
    ('Действующие льготы, которые ведёт МФЦ (у абонента «не применяется»)',
        (select count(distinct r.physicalpersonid) from legacy.privroll r join legacy.social s on s.socialpositionid = r.socialpositionid
          where r.enddate is null
            and lower(trim(s.socialpositionname)) not in ('юридическое лицо', 'коллективная оплата', 'коллективный усилитель в квартире')),
        null, 'справка');

select title as "Проверка", jack as "JACK", avers as "Аверс", status as "Итог" from recon.transfer_check order by n;

do $$
begin
    if exists (select from recon.transfer_check where status = 'РАСХОЖДЕНИЕ') then
        raise exception 'ИТОГ: есть расхождения, переезд не принимать';
    end if;
    raise notice 'ИТОГ: перенос сошёлся';
end $$;
