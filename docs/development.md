# Разработка

Как собрать проект, запустить тесты и внести изменение. Устройство системы описано в
[03-architecture.md](03-architecture.md), решения с обоснованием — в [adr/](adr/).

## Что нужно

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (версия закреплена в `global.json`);
- редактор: VS Code (настройки и задачи лежат в `.vscode/`), Visual Studio или Rider;
- Docker — только для локального PostgreSQL и стенда миграции.

## Сборка и тесты

```sh
dotnet build Avers.sln       # сборка
dotnet test Avers.sln        # все тесты
dotnet format Avers.sln      # форматирование по .editorconfig
```

В VS Code те же команды есть в задачах, сборка — `Ctrl+Shift+B`.

Серверные тесты по умолчанию идут на SQLite. Чтобы прогнать их на PostgreSQL:

```sh
docker compose -f db/docker-compose.yml up -d
AVERS_TEST_POSTGRES="Host=localhost;Username=avers;Password=<пароль из .env>" dotnet test tests/Avers.Server.Tests
```

Тесты переноса из старого Аверса (`JackTransferTests`) работают только на PostgreSQL. Без `AVERS_TEST_POSTGRES`
они отмечены пропущенными (атрибут `[PostgresFact]`), в CI их прогоняет задача `server-postgres`.

Работу рабочего места через сервер проверяет `ServerModeTests`: настоящий сервер в памяти процесса и
`ServerStore`. Новый запрос в `ServerStore` сопровождается тестом там же.

## Запуск

| Что | Команда |
|---|---|
| Рабочее место с учебной базой | `dotnet run --project src/Avers.Desktop` |
| Сервер с учебной базой | `dotnet run --project src/Avers.Server` (адрес `http://localhost:5115`, проверка — `/health`) |
| Рабочее место через сервер | `dotnet run --project src/Avers.Desktop -- --server=http://localhost:5115` |
| Сервер на локальном PostgreSQL | см. [src/Avers.Server/README.md](../src/Avers.Server/README.md) |

Описание API в режиме Development: `http://localhost:5115/openapi/v1.json`.

## Структура репозитория

```text
src/
  Avers.Domain/      сущности и правила: абоненты, дома, оплаты, долг, заявки, роли, сроки юристов
  Avers.Data/        EF Core: PostgreSQL на сервере, SQLite в учебном режиме; миграции; учебные данные
  Avers.Contracts/   DTO API, общие для сервера и рабочего места
  Avers.Server/      сервер приложений ASP.NET Core и страница для телефона (wwwroot/m)
  Avers.Desktop/     рабочее место на Avalonia: экраны, печатные формы, отчёты
tests/               xUnit: домен, данные, сервер, интерфейс без монитора (Avalonia.Headless)
tools/Avers.Screenshots/   снимки экранов без монитора
db/                  выгрузка схемы JACK, стенд Oracle Free + Ora2Pg, сверка после переноса
deploy/              установка сервера (server), терминальный сервер Linux (xrdp) и Windows (rds)
docs/                документация, ADR
```

Зависимости слоёв: `Domain` ни от чего не зависит, `Data` зависит от `Domain`, `Server` и `Desktop` — от `Data`
и `Contracts`. Правило, которое нужно и серверу, и рабочему месту, пишется один раз в `Domain` или `Data`.

## Изменения в базе

Модель меняется в `Avers.Domain` и `AversDbContext`, затем добавляется миграция (пишется под PostgreSQL,
сама база для этого не нужна):

```sh
dotnet ef migrations add <Название> --project src/Avers.Data --output-dir Migrations
```

На PostgreSQL сервер применяет миграции при запуске (настройка `Migrate`, см.
[src/Avers.Server/README.md](../src/Avers.Server/README.md)). Учебная база SQLite создаётся без миграций. Если старое рабочее место может испортить данные новой версии
(например, затрёт пустым значением новое поле), поднимите `DesktopClient.Version` в `Avers.Contracts`:
сервер ответит старой программе «Обновите программу».

## Как вносить изменения

1. Ветка от `main`: `feature/<кратко>`, `fix/<кратко>` или `docs/<кратко>`.
2. Для ошибки сначала тест, который её воспроизводит, потом исправление.
3. Коммиты по-русски в виде `<область>: <что сделано>`, например `оболочка: печать наряд-заказа`.
   Области: `оболочка`, `сервер`, `домен`, `миграция`, `1с`, `отчёты`, `документация`, `инфраструктура`.
4. Перед pull request: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.
5. В том же pull request обновите документацию:
   - [CHANGELOG.md](../CHANGELOG.md) — строка о том, что изменилось для пользователя;
   - [руководство пользователя](user-guide/README.md), если поменялись экраны или порядок работы;
   - [10-traceability.md](10-traceability.md) и [05-roadmap.md](05-roadmap.md), если требование сделано;
   - ADR в [adr/](adr/), если принято значимое решение (выбор технологии, формат данных, способ интеграции).
6. Слияние в `main` — после зелёного CI и согласия владельца репозитория.

## Обязательные правила

- **Боевые базы только читаются.** Писать в Oracle JACK нельзя.
- **Персональные данные и секреты не попадают в git.** Выгрузки — в `db/legacy/dumps/` (в `.gitignore`),
  пароли — в `.env` по образцу `.env.example` или в user-secrets.
- **Деньги — `decimal`.** Суммы сверяются с бумажной сверкой бухгалтерии.
- **Права проверяет сервер.** Рабочее место только прячет недоступные кнопки.
- Термины предметной области в коде берутся из [глоссария](02-requirements.md#глоссарий), без транслита.

## Проверки CI

`.github/workflows/ci.yml` на каждый pull request:

- сборка, тесты и проверка форматирования на Windows, пробный запуск `Avers.exe`;
- сборка рабочего места и сервера под Linux с пробным запуском;
- серверные тесты на PostgreSQL 18;
- проверка shell-скриптов установки и миграции (shellcheck) и тесты сверки данных после переноса.

Готовые сборки лежат в артефактах запуска: `Avers-win-x64`, `Avers-linux-x64` (вместе с файлами xrdp)
и `Avers-server-linux-x64` (вместе с установщиком сервера).

## Снимки экранов

```sh
dotnet run --project tools/Avers.Screenshots -- shots                               # admin, основные экраны
dotnet run --project tools/Avers.Screenshots -- shots sokolova Desktop Disp         # пользователь и модули
dotnet run --project tools/Avers.Screenshots -- shots admin Abon --size=1366x768    # размер окна
```

Снимки для документации лежат в [docs/images/](images/). Экраны рассчитаны на монитор 1366×768.
