-- =====================================================================
-- Аверс: выгрузка СТРУКТУРЫ схемы JACK для проекта миграции.
-- Только чтение: в базе ничего не меняется. Данные абонентов не выгружаются.
--
-- Запуск (Windows, из папки со скриптами):
--   set NLS_LANG=AMERICAN_AMERICA.AL32UTF8
--   sqlplus system@avers102 @00-run-all.sql
-- Результат: файлы jack_*.csv и jack_*.sql в текущей папке.
-- Подробно — README.md рядом.
-- =====================================================================
whenever sqlerror continue
whenever oserror continue
set verify off feedback off termout on echo off

-- Схема, которую выгружаем
define owner = JACK

alter session set nls_numeric_characters = '.,';
alter session set nls_date_format = 'YYYY-MM-DD HH24:MI:SS';

prompt === 1/3 Инвентаризация (версия, объекты, таблицы, столбцы, ключи, объёмы)
@@01-inventory.sql
prompt === 2/3 Исходный код PL/SQL, представления, триггеры, задания
@@02-plsql-source.sql
prompt === 3/3 DDL всей схемы (dbms_metadata)
@@03-ddl.sql

prompt
prompt Готово. Передайте все файлы jack_*.csv и jack_*.sql разработчикам.
prompt (точные числа строк — отдельный скрипт 04-row-counts.sql, запускать вне рабочего времени)
exit
