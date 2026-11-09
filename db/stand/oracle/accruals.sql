-- Начисления JACK по месяцам — для переноса оплат в Аверс (db/migration/transfer/transfer.sql).
-- JACK начислений не хранит, а считает их функцией CLIENTMONTHDEBT_NEW (тариф × отводы − льготы, особый тариф дома,
-- тариф юрлица). Чтобы долги в Аверсе совпали с JACK, считаем той же функцией, а не повторяем формулу.
-- Выполняется только на стенде, в восстановленной копии JACK, до выгрузки структуры Ora2Pg (stand.sh accruals):
-- создаёт таблицу AVERS_ACCRUAL, которую Ora2Pg переносит в legacy вместе с остальными.
-- Боевую базу не трогать: скрипт пишет в схему.
--
-- Допущения (сверить на данных):
-- * Считаем с января 1998 года: раньше суммы в неденоминированных рублях. Оплаты до 1998 года тоже не переносятся.
-- * По каждому типу отвода абонента (CONNLIST.CONNTYPEID), начиная с месяца первой записи отвода, по декабрь текущего года.
-- * Нулевые начисления не сохраняются.
whenever sqlerror exit failure
set feedback off
alter session set current_schema = &1;

begin
  execute immediate 'drop table avers_accrual purge';
exception when others then
  if sqlcode != -942 then raise; end if;
end;
/

create table avers_accrual nologging as
select physicalpersonid, month, conntypeid, amount
  from (select c.physicalpersonid, m.month, c.conntypeid,
               clientmonthdebt_new(c.physicalpersonid, m.month, c.conntypeid) amount
          from (select physicalpersonid, conntypeid, greatest(trunc(min(checkdate), 'MM'), date '1998-01-01') first_month
                  from connlist
                 group by physicalpersonid, conntypeid) c
          join (select add_months(date '1998-01-01', level - 1) month
                  from dual
               connect by level <= months_between(add_months(trunc(sysdate, 'YYYY'), 11), date '1998-01-01') + 1) m
            on m.month >= c.first_month)
 where amount != 0;

alter table avers_accrual add constraint avers_accrual_pk primary key (physicalpersonid, month, conntypeid);
select count(*) "СТРОК НАЧИСЛЕНИЙ", sum(amount) "СУММА" from avers_accrual;
exit
