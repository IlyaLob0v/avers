-- Учебная схема AVERS_DEMO для самопроверки стенда (stand.sh selftest).
-- Данные полностью синтетические и к JACK отношения не имеют: структура JACK неизвестна,
-- здесь только типичные для Oracle типы, на которых обычно ломается перенос:
-- NUMBER без точности с копейками, DATE, CHAR, кириллица, CLOB, внешние ключи, последовательность, триггер.
-- Запуск: в контейнере oracle-legacy под SYSTEM в FREEPDB1.
whenever sqlerror exit failure
set feedback off

begin
  execute immediate 'drop user avers_demo cascade';
exception when others then
  if sqlcode != -1918 then raise; end if;
end;
/
create user avers_demo identified by "Demo_only_1" quota unlimited on users;
grant create session, create table, create sequence, create view, create procedure, create trigger to avers_demo;

create table avers_demo.demo_street (
  id     number(10) primary key,
  name   varchar2(100 char) not null,
  kind   char(3 char)
);
comment on table avers_demo.demo_street is 'Учебный справочник улиц';

create table avers_demo.demo_account (
  id         number(10) primary key,
  street_id  number(10) not null references avers_demo.demo_street (id),
  house      varchar2(10),
  full_name  varchar2(200 char),
  opened     date,
  rate       number,
  note       clob
);

create table avers_demo.demo_payment (
  id          number(12) primary key,
  account_id  number(10) not null references avers_demo.demo_account (id),
  paid_at     date not null,
  amount      number(12, 2) not null,
  source      varchar2(20),
  created_ts  timestamp default systimestamp
);
create index avers_demo.demo_payment_acc_ix on avers_demo.demo_payment (account_id, paid_at);

create sequence avers_demo.demo_payment_seq start with 100000;

create or replace view avers_demo.demo_month_total as
  select trunc(paid_at, 'MM') month, source, sum(amount) total
    from avers_demo.demo_payment group by trunc(paid_at, 'MM'), source;

create or replace trigger avers_demo.demo_payment_bi
  before insert on avers_demo.demo_payment for each row
begin
  if :new.id is null then
    select avers_demo.demo_payment_seq.nextval into :new.id from dual;
  end if;
end;
/

insert into avers_demo.demo_street (id, name, kind)
  select level, 'Учебная улица №' || level || decode(mod(level, 3), 0, ' (ёлочная)', ''), decode(mod(level, 2), 0, 'ул', 'пр')
    from dual connect by level <= 50;

insert into avers_demo.demo_account (id, street_id, house, full_name, opened, rate, note)
  select level, mod(level, 50) + 1, to_char(mod(level, 120) + 1) || decode(mod(level, 7), 0, 'Д', ''),
         'Абонент Учебный ' || level, date '2005-01-01' + mod(level * 37, 7000),
         decode(mod(level, 5), 0, null, 85 + mod(level, 4) * 12.5),
         decode(mod(level, 10), 0, to_clob('Учебная заметка ' || level), null)
    from dual connect by level <= 2000;

insert into avers_demo.demo_payment (id, account_id, paid_at, amount, source)
  select level, mod(level, 2000) + 1, date '2025-01-01' + mod(level, 600),
         round(50 + mod(level * 7919, 50000) / 100, 2),
         decode(mod(level, 4), 0, 'Почта', 1, 'Сбербанк', 2, 'ЕРЦ', 'Касса')
    from dual connect by level <= 20000;
commit;

begin
  dbms_stats.gather_schema_stats('AVERS_DEMO');
end;
/
exit
