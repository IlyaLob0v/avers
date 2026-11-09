-- Перенос JACK → Аверс: из схемы legacy (копия JACK после Ora2Pg) в таблицы Аверса той же базы.
-- Правила полей — db/migration/mapping.md. Все шаги в одной транзакции: при любой ошибке база остаётся как была.
-- Запускается на пустой базе Аверса (после установки без учебных данных): если абоненты уже есть, скрипт останавливается.
-- Ключи JACK сохраняются как ключи Аверса (дом = BUILDINGID, абонент = PHYSICALPERSONID, заявка = DEMANDID и т. д.),
-- поэтому отдельная таблица соответствия ключей не нужна.
-- Пользователи, настройки, схемы домов, домофоны и судебные дела не трогаются: в JACK их нет.

begin;

do $$
begin
    if exists (select from "Subscribers") or exists (select from "Houses") or exists (select from "Payments") then
        raise exception 'В базе Аверса уже есть дома, абоненты или оплаты. Перенос делается только в пустую базу.';
    end if;
end $$;

-- Пустые абоненты: JACK заводит их для заявок «на дом» (CREATEEMPTYCLIENT пишет только дом и квартиру).
-- Признак: нет ФИО, отводов, договоров и оплат. Такие абоненты не переносятся, их заявки остаются на доме.
create or replace view legacy.avers_empty_client as
select p.physicalpersonid
  from legacy.physpers p
 where coalesce(trim(p.surname), '') = '' and coalesce(trim(p.name), '') = '' and coalesce(trim(p.patronymicname), '') = ''
   and not exists (select from legacy.abn_pay a where a.physicalpersonid = p.physicalpersonid)
   and not exists (select from legacy.connlist c where c.physicalpersonid = p.physicalpersonid)
   and not exists (select from legacy.contract c where c.physicalpersonid = p.physicalpersonid);

-- Улицы. Тип улицы дописывается, только если без него названия совпадают («Ленина ул.» и «Ленина пр.»).
insert into "Streets" ("Id", "Name")
select s.streetid,
       case when count(*) over (partition by lower(trim(s.streetname))) > 1
            then trim(s.streetname) || ' ' || coalesce(trim(n.nametype), '')
            else trim(s.streetname) end
  from legacy.streets s
  left join legacy.nametype n on n.nametypeid = s.nametypeid;

-- Организации (домовладельцы): OWNER → JURPERS. Телефонов и руководителей в JACK нет.
insert into "Organizations" ("Id", "Name", "Address", "Branch", "Phones", "HeadName", "HeadPhone", "EngineerName", "EngineerPhone")
select o.ownerid,
       coalesce(nullif(trim(j.name), ''), trim(j.shortname), ''),
       coalesce(trim(st.streetname) || ', ' || trim(b.buildingno), ''),
       '', '', '', '', '', ''
  from legacy.owner o
  join legacy.jurpers j on j.juridicalpersonid = o.juridicalpersonid
  left join legacy.building b on b.buildingid = j.buildingid
  left join legacy.streets st on st.streetid = b.streetid;

-- Механики: работающие сотрудники (WORKMAN без даты увольнения).
insert into "Mechanics" ("Id", "FullName", "Position", "Phone", "Address")
select w.workmanid,
       concat_ws(' ', nullif(trim(p.surname), ''), nullif(trim(p.name), ''), nullif(trim(p.patronymicname), '')),
       coalesce(trim(t.jobtitlename), ''), coalesce(trim(p.phone), ''), ''
  from legacy.workman w
  join legacy.personnel_department p on p.persid = w.persid
  left join legacy.jobtitle t on t.jobtitleid = w.jobtitleid
 where w.sackingdate is null;

-- Дома.
insert into "Houses" ("Id", "StreetId", "Number", "Floors", "Entrances", "Risers", "Apartments", "Zone", "Serviced",
                      "OrganizationId", "Antennas", "OtherNotes", "LastPpr", "PzhreuIndex", "PzhreuAccount",
                      "AerialCables", "Amplifiers", "PowerSupplies", "TechFloorKey")
select b.buildingid, b.streetid, trim(b.buildingno),
       coalesce(b.floorsnumber, 0), coalesce(b.entrynumber, 0), coalesce(b.stemscount, 0), coalesce(b.flatsnumber, 0),
       coalesce(left(trim(a.servareaname), 80), ''), true,
       o."Id", coalesce(b.antennscount::text, ''), coalesce(trim(b.note), ''),
       (select max(p.factdate)::date from legacy.ppr_table p where p.buildingid = b.buildingid),
       '', '', '', '', '', ''
  from legacy.building b
  left join legacy.servarea a on a.servareaid = b.servareaid
  left join "Organizations" o on o."Id" = b.ownerid;

-- Абоненты. Договор и индекс/лицевой счёт ПЖРЭУ — последние по дате. Льгота — действующая и только из трёх,
-- которые ведёт АДС (Subscriber.Benefits); остальные льготы оформляет МФЦ, у абонента — «не применяется».
insert into "Subscribers" ("Id", "HouseId", "InnerCode", "Account", "LastName", "FirstName", "Patronymic", "Phone",
                           "Apartment", "Section", "ContractDate", "ContractTerminated", "ContractNumber", "ContractHolder",
                           "Notes", "PzhreuIndex", "PzhreuAccount", "OekAccount", "SettlementFrom", "LastPaymentDate",
                           "PrintAccountWithPayDate", "Benefit")
select p.physicalpersonid, p.buildingid, coalesce(trim(p.inventno), ''), p.physicalpersonid::text,
       coalesce(trim(p.surname), ''), coalesce(trim(p.name), ''), coalesce(trim(p.patronymicname), ''),
       coalesce(trim(p.phone), ''), p.flatno::text, case when coalesce(p.section, 0) = 0 then '' else p.section::text end,
       c.begindate::date, c.enddate::date, coalesce(c.contractid::text, ''), '',
       coalesce(trim(p.note), ''), coalesce(sc.pgru_index::text, ''), coalesce(sc.pgru_score::text, ''), '', 0,
       (select max(a.paydate)::date from legacy.abn_pay a where a.physicalpersonid = p.physicalpersonid),
       true, coalesce(bn.name, 'не применяется')
  from legacy.physpers p
  left join lateral (select c.contractid, c.begindate, c.enddate from legacy.contract c
                      where c.physicalpersonid = p.physicalpersonid
                      order by c.begindate desc, c.contractid desc limit 1) c on true
  left join lateral (select s.pgru_index, s.pgru_score from legacy.physpers_score s
                      where s.physicalpersonid = p.physicalpersonid
                      order by s.scoredate desc nulls last limit 1) sc on true
  left join lateral (select lower(trim(s.socialpositionname)) as name from legacy.privroll r
                       join legacy.social s on s.socialpositionid = r.socialpositionid
                      where r.physicalpersonid = p.physicalpersonid and r.enddate is null
                        and lower(trim(s.socialpositionname)) in ('юридическое лицо', 'коллективная оплата', 'коллективный усилитель в квартире')
                      order by r.dateofupdate desc limit 1) bn on true
 where p.physicalpersonid not in (select physicalpersonid from legacy.avers_empty_client);

-- Оплаты: в Аверсе одна строка на абонента и месяц. Начисление — из legacy.avers_accrual (функция JACK, см.
-- db/stand/oracle/accruals.sql), платежи за месяц (ABN_PAY.PAYFORDATE) складываются, дата и кто внёс — у последнего платежа.
-- Источник — тип оплаты JACK, для оплат через ПЖРЭУ (есть строка в POST_PAY_ATTR) — «ПЖРЭУ».
-- Месяц без платежей остаётся неоплаченным (сумма пустая); платёж в 0 рублей переносится как есть.
insert into "Payments" ("SubscriberId", "Year", "Month", "Tariff", "Amount", "PaidAt", "RecordedBy", "Source")
with accrual as (
    select physicalpersonid as id, date_trunc('month', month)::date as month, round(sum(amount), 2) as tariff
      from legacy.avers_accrual
     group by 1, 2
), pay as (
    select p.physicalpersonid as id, date_trunc('month', p.payfordate)::date as month, sum(p.summpay) as amount,
           max(p.paydate)::date as paid_at,
           (array_agg(p.username order by p.paydate desc, p.payid desc))[1] as recorded_by,
           (array_agg(case when pp.payid is not null then 'ПЖРЭУ' else trim(t.paytype) end
                      order by p.paydate desc, p.payid desc))[1] as source
      from legacy.abn_pay p
      left join legacy.pay_type t on t.paytypeid = p.paytypeid
      left join (select distinct payid from legacy.post_pay_attr) pp on pp.payid = p.payid
     where p.payfordate >= date '1998-01-01'
     group by 1, 2
)
select coalesce(a.id, p.id), extract(year from coalesce(a.month, p.month)), extract(month from coalesce(a.month, p.month)),
       coalesce(a.tariff, 0), p.amount, p.paid_at, p.recorded_by, p.source
  from accrual a
  full join pay p on p.id = a.id and p.month = a.month
 where coalesce(a.id, p.id) in (select "Id" from "Subscribers");

-- «Расчёт с» — год первого перенесённого месяца.
update "Subscribers" s set "SettlementFrom" = y.first_year
  from (select "SubscriberId", min("Year") as first_year from "Payments" group by 1) y
 where y."SubscriberId" = s."Id";

-- Квитанции ПЖРЭУ. Начислено — начисление того месяца, за который платёж.
insert into "UnifiedReceipts" ("SubscriberId", "PzhreuIndex", "Account", "Moment", "Paid", "NOs", "NKass", "NOper", "NKvit",
                               "InnerCode", "Amount")
select a.physicalpersonid, a.pgru_index::text, a.pgru_score::text, a.paydate::date, a.summpay,
       a.n_os::text, a.n_kass::text, a.n_oper::text, a.n_kvit::text, s."InnerCode", coalesce(m."Tariff", 0)
  from legacy.post_pay_attr a
  join "Subscribers" s on s."Id" = a.physicalpersonid
  left join legacy.abn_pay p on p.payid = a.payid
  left join "Payments" m on m."SubscriberId" = a.physicalpersonid
                        and m."Year" = extract(year from p.payfordate) and m."Month" = extract(month from p.payfordate)
 where a.paydate >= date '1998-01-01';

-- Заявки. Номер заявки — DEMANDID (DEMANDCODE в JACK — набор признаков неисправности, а не номер).
-- Не переносятся «проверки подключения» без заявки: CHECKCONNECT = 1 и DEMANDCODE 0 или 524288 (так их отличает
-- процедура JACK DELETECHECK). Исполнитель в JACK не хранится — ставим ответственного механика участка дома.
-- Выполнена, если есть дата выполнения. Статуса «отменена» в Аверсе нет: отменённая заявка закрывается
-- с пометкой «Заявка отменена».
insert into "Tickets" ("Id", "Number", "Date", "Time", "SubscriberId", "HouseId", "Apartment", "Section", "Phone",
                       "Fault", "WorkDone", "Kind", "Mechanic", "AcceptedBy", "Status", "DoneDate", "RisersNote")
select d.demandid, d.demandid, d.receivingdate::date, d.receivingdate::time, s."Id", p.buildingid,
       case when p.flatno > 0 then p.flatno::text else '' end,
       case when coalesce(p.section, 0) = 0 then '' else p.section::text end,
       coalesce(trim(d.phone), ''), left(coalesce(trim(d.faultdescription), ''), 500),
       left(concat_ws('. ', nullif(trim(d.note), ''), nullif(trim(d.pgrureportnote), ''),
                      case when d.iscanceled = 1 then 'Заявка отменена' end), 500),
       case when w.worktypename ilike '%антен%' then 0
            when w.worktypename ilike '%кабел%' then 1
            when w.worktypename ilike '%усил%' then 2
            when w.worktypename ilike '%домоф%' then 3
            when w.worktypename ilike '%провайд%' or w.worktypename ilike '%интернет%' then 4
            else 5 end,
       coalesce(m.name, ''), coalesce(trim(d.userid), ''),
       case when d.executiondate is not null or d.iscanceled = 1 then 2 else 0 end,
       coalesce(d.executiondate, case when d.iscanceled = 1 then d.receivingdate end)::date, ''
  from legacy.demands d
  join legacy.physpers p on p.physicalpersonid = d.physicalpersonid
  join legacy.building b on b.buildingid = p.buildingid
  left join "Subscribers" s on s."Id" = d.physicalpersonid
  left join legacy.wrktypes w on w.worktypeid = d.worktypeid
  left join (select a.servareaid,
                    concat_ws(' ', nullif(trim(pd.surname), ''), nullif(trim(pd.name), ''), nullif(trim(pd.patronymicname), '')) as name
               from legacy.servarea a
               join legacy.workman wm on wm.workmanid = a.responsibleperformerid
               join legacy.personnel_department pd on pd.persid = wm.persid) m on m.servareaid = b.servareaid
 where not (d.checkconnect = 1 and d.demandcode in (0, 524288));

-- ППР: выполнено — есть фактическая дата; нет даты и месяц прошёл — просрочено; иначе запланировано.
insert into "PprEntries" ("HouseId", "Year", "Month", "Status")
select p.buildingid, extract(year from p.plandate), extract(month from p.plandate),
       case when p.factdate is not null then 2
            when date_trunc('month', p.plandate) < date_trunc('month', current_date) then 3
            else 1 end
  from legacy.ppr_table p;

-- Счётчики ключей — после самого большого перенесённого, чтобы новые записи Аверса не столкнулись со старыми.
select setval(pg_get_serial_sequence('"Streets"', 'Id'), coalesce(max("Id"), 0) + 1, false) from "Streets";
select setval(pg_get_serial_sequence('"Organizations"', 'Id'), coalesce(max("Id"), 0) + 1, false) from "Organizations";
select setval(pg_get_serial_sequence('"Mechanics"', 'Id'), coalesce(max("Id"), 0) + 1, false) from "Mechanics";
select setval(pg_get_serial_sequence('"Houses"', 'Id'), coalesce(max("Id"), 0) + 1, false) from "Houses";
select setval(pg_get_serial_sequence('"Subscribers"', 'Id'), coalesce(max("Id"), 0) + 1, false) from "Subscribers";
select setval(pg_get_serial_sequence('"Tickets"', 'Id'), coalesce(max("Id"), 0) + 1, false) from "Tickets";

commit;
analyze;
