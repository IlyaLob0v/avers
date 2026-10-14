# 06. Что мне нужно и что можно интегрировать

Статус: 14.10.2026. Рекомендации для работы Claude (облачная сессия и Claude Code на ПК разработчика).

## A. Что нужно от вас (без этого работа встанет)

| # | Что | Зачем | Как передать |
|---|---|---|---|
| A1 | **Исходники оболочки** `D:\Avers\Source` и старого Аверса (Delphi, Java) | Без кода оболочки не перевести её на .NET 10 до 10.11.2026; старый код — источник реальной логики | Лучше всего — приватный репозиторий GitHub (см. Б1). Или zip-архив в файлы проекта |
| A2 | **Выгрузка схемы JACK**: DDL + PL/SQL (`db/legacy/01-inventory.sql`, `02-plsql-source.sql`) | Схема в PDF — догадки; без DDL нельзя проектировать миграцию | Результаты запросов в CSV/TXT в файлы проекта (без персональных данных) |
| A3 | **Ответы бухгалтерии** по опроснику и образцы реестров (обезличенные) | Отгрузка, источники оплат, 1С | Опросник: https://claude.ai/code/artifact/5bc4713c-f3fe-47c6-a180-8a4a2bfc55d2 |
| A4 | Решения по вопросам Q-01…Q-15 | См. `docs/07-open-questions.md` | Ответом в треде |

## Б. Интеграции — по приоритету

### Б1. GitHub (нужно сразу)
- **Что:** приватный репозиторий `avers`, подключённый к проекту Claude.
- **Зачем:** единственный нормальный путь передать код из `D:\` в облако, раз доступ к ПК не используем; история изменений,
  pull request с ревью, CI. Сейчас репозиторий лежит только в файлах проекта (`/mnt/project-files/avers`).
- **Как:** подключить GitHub (https://claude.ai/connect-github), создать репозиторий; я перенесу туда текущую историю
  и открою PR с переводом на .NET 10, как только появится код оболочки.

### Б2. CI — GitHub Actions (вместе с Б1)
- Сборка и тесты на каждый PR (`.github/workflows/ci.yml` уже подготовлен), публикация win-x64 сборки как артефакта.
- Dependabot для NuGet, CodeQL для C# — бесплатно для приватных репозиториев с ограничениями плана GitHub.

### Б3. .NET SDK в облачной среде Claude
- **Зачем:** чтобы я сам собирал и тестировал код в облаке. Сейчас .NET в контейнере нет.
- **Как:** SessionStart-хук, который ставит .NET 10 SDK при старте сессии (скилл `session-start-hook`), сеть среды
  должна пускать на `builds.dotnet.microsoft.com` и `api.nuget.org` (nuget.org уже доступен).

### Б4. MCP-серверы (подключаются в `.mcp.json` проекта; образец — `.mcp.json.example`)

| Сервер | Зачем | Где запускать |
|---|---|---|
| **Microsoft Learn** — уже подключён | Актуальная документация .NET, EF Core, ASP.NET Core | Есть |
| **Context7** | Свежая документация библиотек (Avalonia 12, Npgsql, ClosedXML, Velopack) вместо памяти модели | Облако и ПК |
| **Postgres MCP Pro** (`crystaldba/postgres-mcp`), режим `restricted` | Запросы, планы, индексы на стенде PostgreSQL — только чтение | ПК разработчика / сервер стенда (облако не видит вашу сеть) |
| **Oracle SQLcl MCP** (`sql -mcp`, SQLcl 25.2+) | Разбор схемы JACK на восстановленной копии в Oracle Free | ПК / стенд; к боевому 10.2, скорее всего, не подключится (новый JDBC) |
| **MCP для 1С** (например, сервер метаданных конфигурации, обзоры на infostart.ru) | Понимать структуру базы 1С при настройке обмена | Позже, на тестовой копии 1С; выбирать после ответа администратора 1С |

### Б5. Скиллы
- Уже доступны и прописаны в `orchestrator.md`: `engineering:*` (architecture, system-design, code-review, testing-strategy,
  debug, documentation, deploy-checklist, tech-debt), `data:*` (write-query, validate-data, explore-data, data-context-extractor).
- Предлагаю создать **скиллы проекта** после выгрузки JACK:
  - `avers-domain` — глоссарий, реальная схема JACK, правила расчёта долга и отгрузки;
  - `avers-reconciliation` — как сверять цифры с бухгалтерией (август 2026 как эталон);
  - `avers-1c-exchange` — формат обмена с 1С, когда он будет выбран.

### Б6. Хуки Claude Code
- `tools/hooks/guard-prod.sh` (подключён в `.claude/settings.json`): блокирует команды, которые пишут в боевые базы
  (`servora`, `192.168.37.*` + INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE).
- Позже: автоформат `dotnet format` после правки `.cs`.

### Б7. Коннекторы claude.ai
- **Claude Docs** (подключён) — документы для сотрудников (опросники, инструкции), которые можно комментировать.
- **Gmail** (подключён) — черновики писем бухгалтерии/ИТ с вопросами; отправляю только по вашей команде.
- **Notion** (подключён) — если хотите вести задачи и протоколы встреч там.
- Jira/Linear/Asana — не нужны, пока команда маленькая: хватит GitHub Issues.

### Б8. Для рабочих мест и сервера (не для Claude, а для системы)
- **Velopack** — автообновление настольного приложения.
- **Сертификат подписи кода** — уберёт предупреждение SmartScreen при запуске Avers.exe.
- **VPN** для телефонов радиомехаников (WireGuard/OpenVPN) вместо публикации сервера в интернет.
- **Резервное копирование PostgreSQL** — pgBackRest или `pg_dump` по расписанию.

## Источники

- .NET support policy: https://dotnet.microsoft.com/en-US/platform/support/policy/dotnet-core
- Avalonia 12: https://avaloniaui.net/blog/avalonia-12
- Ora2Pg 25: https://www.postgresql.org/about/news/ora2pg-v250-has-been-released-3063/
- Субагенты Claude Code: https://code.claude.com/docs/en/sub-agents.md
- SQLcl MCP: https://docs.oracle.com/en/database/oracle/sql-developer-command-line/26.1/sqcug/starting-and-managing-sqlcl-mcp-server.html
- Postgres MCP Pro: https://playbooks.com/mcp/crystaldba-postgres
- MCP для 1С (обзор): https://infostart.ru/1c/articles/2720241/
