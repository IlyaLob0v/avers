# db/migration — скрипты переноса Oracle JACK → PostgreSQL

Порядок — `docs/04-db-migration-plan.md`. Каждый скрипт повторяемый. Нумерация: `NNN-описание.sql`.

- `reconcile/` — сверка переноса (отпечатки таблиц Oracle и PostgreSQL, кириллица, суммы бухгалтерии).
  Запускается из `db/stand/stand.sh reconcile`; автотесты — `reconcile/test/run-tests.sh` (идут в CI).
- Будут: `mapping.md` (старое поле → новое) и скрипты преобразования `legacy → новая модель` по сущностям —
  после того как придёт DDL схемы JACK. Новую модель (`src/Avers.Data`) эти скрипты не меняют, только заполняют.

Стенд и конфигурация Ora2Pg — в `db/stand/`.
