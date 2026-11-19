-- Только SELECT; SQL*Plus 10+. Запуск: @05-data-profile.sql JACK.
-- Полные сканирования крупных таблиц: на восстановленной копии или в согласованное нерабочее время.
-- Вывод содержит агрегаты, без ФИО, телефонов и номеров лицевых счетов.
whenever sqlerror exit failure rollback
define owner = &1
set feedback off
set verify off
set echo off
set pagesize 50000
set linesize 240
set trimspool on
set colsep ';'
spool jack_31_data_profile.csv

prompt === Дата профиля. Запросы не являются единым снимком изменяющейся базы.
select to_char(sysdate, 'YYYY-MM-DD HH24:MI:SS') as profile_at from dual;

prompt === Оплаты: точный объём и диапазоны дат
select count(*) as rows_exact,
       to_char(min(payfordate), 'YYYY-MM-DD') as min_period,
       to_char(max(payfordate), 'YYYY-MM-DD') as max_period,
       to_char(min(paydate), 'YYYY-MM-DD') as min_paid_at,
       to_char(max(paydate), 'YYYY-MM-DD') as max_paid_at,
       count(distinct physicalpersonid) as accounts_with_payments
  from &owner..abn_pay;

prompt === Расчётные годы: суммы исходные, между денежными единицами не складывать
select extract(year from payfordate) as period_year, count(*) as operations,
       sum(summpay) as original_amount,
       count(case when summpay = 0 then 1 end) as zero_operations,
       count(case when summpay < 0 then 1 end) as negative_operations,
       count(case when extract(year from paydate) <> extract(year from payfordate) then 1 end) as other_payment_year
  from &owner..abn_pay
 group by extract(year from payfordate)
 order by 1;

prompt === Точный объём реквизитов квитанций и диапазон поступлений
select count(*) as rows_exact,
       to_char(min(paydate), 'YYYY-MM-DD') as min_paid_at,
       to_char(max(paydate), 'YYYY-MM-DD') as max_paid_at,
       count(distinct payid) as referenced_payments
  from &owner..post_pay_attr;

prompt === Абоненты: отсутствие поля не является основанием удалить запись
select count(*) as accounts,
       count(case when trim(surname) is null then 1 end) as missing_surname,
       count(case when trim(phone) is null then 1 end) as missing_phone,
       count(case when trim(inventno) is null then 1 end) as missing_inner_code
  from &owner..physpers;

prompt === Повторы внутреннего кода после удаления пробелов: число групп, без значений
select count(*) as duplicate_code_groups
  from (select trim(inventno)
          from &owner..physpers
         where trim(inventno) is not null
         group by trim(inventno)
        having count(*) > 1);

prompt === Распределение числа счетов по домам
select min(accounts) as min_accounts, round(avg(accounts), 2) as average_accounts,
       max(accounts) as max_accounts
  from (select buildingid, count(*) as accounts from &owner..physpers group by buildingid);

prompt === Связи: строки без соответствующего абонента или платежа
select 'ABN_PAY -> PHYSPERS' as relation_name, count(*) as orphan_rows
  from &owner..abn_pay p
 where not exists (select 1 from &owner..physpers s where s.physicalpersonid = p.physicalpersonid)
union all
select 'POST_PAY_ATTR -> ABN_PAY', count(*)
  from &owner..post_pay_attr a
 where not exists (select 1 from &owner..abn_pay p where p.payid = a.payid);

prompt === Длина расчёта начислений: пары абонент-тип и число месяцев с 1998 года
select count(*) as account_connection_types,
       to_char(min(first_month), 'YYYY-MM-DD') as first_connection_month,
       sum(greatest(0, months_between(add_months(trunc(sysdate, 'YYYY'), 12),
                   greatest(first_month, date '1998-01-01')))) as candidate_month_calculations
  from (select physicalpersonid, conntypeid, trunc(min(checkdate), 'MM') as first_month
          from &owner..connlist
         group by physicalpersonid, conntypeid);

prompt === Договоры: точное число строк и диапазон начала
select count(*) as contracts, count(distinct physicalpersonid) as accounts_with_contracts,
       to_char(min(begindate), 'YYYY-MM-DD') as first_begin,
       to_char(max(begindate), 'YYYY-MM-DD') as last_begin
  from &owner..contract;

spool off
exit
