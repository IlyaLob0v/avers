-- Учебная копия JACK для автотеста переноса: те же таблицы и столбцы, что в схеме legacy после Ora2Pg,
-- типы — как их выдаёт Ora2Pg (NUMBER(p,0) → integer/bigint, DATE → timestamp(0)). Все данные выдуманы.
drop schema if exists legacy cascade;
create schema legacy;

create table legacy.nametype (nametypeid smallint primary key, nametype varchar(15));
create table legacy.streets (streetid integer primary key, streetname varchar(100) not null, nametypeid smallint not null);
create table legacy.jurpers (juridicalpersonid integer primary key, name varchar(160), shortname varchar(15), buildingid integer);
create table legacy.owner (ownerid smallint primary key, juridicalpersonid integer not null);
create table legacy.jobtitle (jobtitleid smallint primary key, jobtitlename varchar(15));
create table legacy.personnel_department (persid integer primary key, surname varchar(50), name varchar(25),
    patronymicname varchar(25), phone varchar(20));
create table legacy.workman (workmanid smallint primary key, persid integer not null, jobtitleid smallint not null,
    sackingdate timestamp(0));
create table legacy.servarea (servareaid smallint primary key, responsibleperformerid smallint not null,
    servareaname varchar(100) not null);
create table legacy.building (buildingid integer primary key, streetid integer not null, buildingno varchar(5) not null,
    servareaid bigint, floorsnumber smallint, flatsnumber smallint, entrynumber smallint, stemscount smallint,
    ownerid smallint, note varchar(250), antennscount smallint);
create table legacy.physpers (physicalpersonid integer primary key, surname varchar(50), name varchar(25),
    patronymicname varchar(25), buildingid integer not null, flatno smallint not null, inventno varchar(8),
    phone varchar(20), note varchar(300), section smallint);
create table legacy.physpers_score (physicalpersonid integer not null, scoredate timestamp(0), pgru_index smallint not null,
    pgru_score integer not null);
create table legacy.contract (contractid integer primary key, physicalpersonid integer not null,
    begindate timestamp(0) not null, enddate timestamp(0), note varchar(250));
create table legacy.social (socialpositionid smallint primary key, socialpositionname varchar(75) not null);
create table legacy.privroll (physicalpersonid integer not null, socialpositionid smallint not null,
    dateofupdate timestamp(0), enddate timestamp(0));
create table legacy.connlist (physicalpersonid integer not null, checkdate timestamp(0) not null, connectcount smallint,
    conntypeid smallint not null);
create table legacy.pay_type (paytypeid smallint primary key, paytype varchar(25));
create table legacy.abn_pay (payid bigint primary key, paytypeid smallint not null, physicalpersonid integer not null,
    payfordate timestamp(0) not null, paydate timestamp(0) not null, summpay numeric(8, 2) not null,
    username varchar(30), conntypeid smallint not null);
create table legacy.post_pay_attr (physicalpersonid integer not null, pgru_index smallint not null, pgru_score integer not null,
    period varchar(4) not null, summpay numeric(8, 2) not null, paydate timestamp(0) not null, importdate timestamp(0) not null,
    n_os bigint not null, n_kass bigint not null, n_oper bigint not null, n_kvit bigint not null, payid bigint not null);
create table legacy.wrktypes (worktypeid smallint primary key, worktypename varchar(25) not null);
create table legacy.demands (demandid bigint primary key, worktypeid smallint, physicalpersonid integer not null,
    receivingdate timestamp(0), executiondate timestamp(0), userid varchar(40), faultdescription varchar(180),
    phone varchar(20), note varchar(200), pgrureportnote varchar(100), iscanceled smallint, demandcode bigint,
    checkconnect smallint);
create table legacy.ppr_table (ppr_id integer primary key, buildingid integer not null, plandate timestamp(0) not null,
    factdate timestamp(0));
-- Таблица начислений, которую на стенде строит db/stand/oracle/accruals.sql функцией JACK
create table legacy.avers_accrual (physicalpersonid integer not null, month timestamp(0) not null, conntypeid smallint not null,
    amount numeric not null);

insert into legacy.nametype values (1, 'ул.'), (2, 'пр.');
insert into legacy.streets values (10, 'Учебная', 1), (11, 'Ленина', 1), (12, 'Ленина ', 2);
insert into legacy.building values
    (100, 10, '19Д', 1, 9, 72, 2, 4, 1, 'Ключ у старшего по дому', 2),
    (101, 11, '5', 1, null, null, null, null, 1, null, null),
    (102, 12, '72/1', 1, 5, 40, 3, 2, 1, '', 1);
insert into legacy.jurpers values (500, 'ООО «Учебная управляющая компания»', 'УУК', 102);
insert into legacy.owner values (1, 500);
insert into legacy.jobtitle values (1, 'Радиомеханик');
insert into legacy.personnel_department values
    (1, 'Механиков', 'Пётр', 'Ильич', '8-900-000-0001'),
    (2, 'Уволенный', 'Иван', null, null);
insert into legacy.workman values (7, 1, 1, null), (8, 2, 1, '2020-05-01');
insert into legacy.servarea values (1, 7, 'Участок № 1 (учебный)');

insert into legacy.physpers values
    (1001, 'Иванова ', 'Мария', 'Петровна', 100, 12, 'А-17', '40-00-01', 'Звонить вечером', 0),
    (1002, 'Петров', 'Олег', null, 100, 13, null, null, null, 2),
    (1003, 'Сидоров', 'Ким', 'Ли', 102, 7, null, null, null, 0),
    (1004, null, null, null, 101, 0, null, null, null, 0);   -- пустой абонент для заявки на дом
insert into legacy.physpers_score values
    (1001, '2019-01-01', 11, 1111),
    (1001, '2024-03-01', 12, 2222);
insert into legacy.contract values
    (9001, 1001, '2010-02-01', null, null),
    (9000, 1001, '2001-01-01', '2009-12-31', null),
    (9002, 1003, '2015-06-01', '2026-03-31', null);
insert into legacy.social values (1, 'Коллективная оплата'), (2, 'Ветеран труда'), (3, 'Юридическое лицо');
insert into legacy.privroll values (1001, 1, '2020-01-01', null), (1002, 2, '2018-01-01', null), (1003, 3, '2015-06-01', null);
insert into legacy.connlist values (1001, '1996-05-01', 1, 1), (1002, '2026-01-01', 1, 1), (1002, '2026-01-01', 1, 46),
                                   (1003, '2025-12-01', 1, 1);

-- Начисления: 1001 — 150 ₽ весь 2026 год; 1002 — два типа отвода в одном месяце (складываются) и дробная сумма;
-- 1003 — только декабрь 2025.
insert into legacy.avers_accrual
select 1001, make_date(2026, m, 1), 1, 150 from generate_series(1, 12) m;
insert into legacy.avers_accrual values
    (1002, '2026-01-01', 1, 75.004), (1002, '2026-01-01', 46, 30), (1002, '2026-02-01', 1, 75.004),
    (1003, '2025-12-01', 1, 150);

insert into legacy.pay_type values (1, 'Касса'), (2, 'Почта');
insert into legacy.abn_pay values
    (1, 1, 1001, '2026-01-01', '2026-01-05', 100.00, 'KASSIR1', 1),
    (2, 2, 1001, '2026-01-01', '2026-01-20', 50.00, 'KASSIR2', 1),   -- второй платёж за тот же месяц
    (3, 1, 1001, '2026-02-01', '2026-02-10', 150.00, 'KASSIR1', 1),  -- через ПЖРЭУ (есть строка в post_pay_attr)
    (4, 1, 1001, '2026-03-01', '2026-03-10', 0.00, 'KASSIR1', 1),    -- платёж в 0 рублей
    (5, 1, 1001, '1997-06-01', '1997-06-10', 15000.00, 'KASSIR1', 1),-- до деноминации, не переносится
    (6, 1, 1002, '2026-01-01', '2026-01-15', 105.00, 'KASSIR1', 1),
    (7, 1, 1003, '2026-04-01', '2026-03-28', 20.00, 'KASSIR1', 1);   -- оплата вперёд, начисления за месяц нет
insert into legacy.post_pay_attr values (1001, 12, 2222, '0226', 150.00, '2026-02-10', '2026-02-12', 3, 4, 5, 6, 3);

insert into legacy.wrktypes values (1, 'Антенна'), (2, 'Кабель'), (3, 'Прочее');
insert into legacy.demands values
    (70001, 1, 1001, '2026-08-03 09:15:00', null, 'DISP1', 'Нет сигнала', '40-00-01', null, null, 0, 16, 0),
    (70002, 2, 1002, '2026-08-04 10:00:00', '2026-08-05', 'DISP1', 'Обрыв кабеля', null, 'Заменили кабель', 'Отчёт ПЖРЭУ', 0, 2, 0),
    (70003, 3, 1003, '2026-08-06 11:30:00', null, 'DISP2', 'Помехи', null, null, null, 1, 4, 0),
    (70004, 1, 1004, '2026-08-07 12:00:00', null, 'DISP2', 'Не работает усилитель в подъезде', null, null, null, 0, 8, 0),
    (70005, 1, 1001, '2026-08-08 00:00:00', null, 'DISP1', null, null, null, null, 0, 0, 1);  -- проверка подключения
insert into legacy.ppr_table values (1, 100, '2026-03-15', '2026-03-20'), (2, 100, '2026-05-15', null), (3, 102, '2099-01-15', null);
