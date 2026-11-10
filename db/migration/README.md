# db/migration — скрипты переноса Oracle JACK → PostgreSQL

Порядок — `docs/04-db-migration-plan.md`. Каждый скрипт повторяемый. Нумерация: `NNN-описание.sql`.

- `reconcile/` — сверка копии JACK (отпечатки таблиц Oracle и PostgreSQL, кириллица, суммы бухгалтерии).
  Запускается из `db/stand/stand.sh reconcile`; автотесты — `reconcile/test/run-tests.sh` (идут в CI).
- `mapping.md` — соответствие полей JACK и новой модели.
- `transfer/` — перенос `legacy → таблицы Аверса` и сверка после него:
  - `transfer.sql` — все сущности в одной транзакции, только в пустую базу Аверса; ключи JACK становятся ключами Аверса;
  - `check.sql` — сравнивает legacy и Аверс: число записей, начисления и оплаты по месяцам, долг каждого абонента;
    при расхождении завершается ошибкой, итоги — в `recon.transfer_check`;
  - `test/legacy-sample.sql` — учебная копия JACK для автотеста `tests/Avers.Server.Tests/JackTransferTests.cs`
    (идёт в CI на PostgreSQL).

  Начисления JACK не хранит; их заранее считает его же функция на стенде (`db/stand/oracle/accruals.sql`,
  `stand.sh accruals`). Пробный перенос на стенде — `stand.sh transfer`, перенос в день переезда —
  [docs/12-cutover-plan.md](../../docs/12-cutover-plan.md). Новую модель (`src/Avers.Data`) скрипты не меняют, только
  заполняют; если модель оплат поменяется (ADR-0010), `transfer.sql` и `check.sql` правятся вместе с ней.

Стенд и конфигурация Ora2Pg — в `db/stand/`.
