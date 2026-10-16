# Аверс 2.0

Новая система учёта антенной службы (Омск): абоненты и платежи, заявки, паспорта домов и схемы СКПТ,
договоры, домофон, должники и претензии, отчёты для бухгалтерии, обмен с 1С.

## С чего начать

| Кому | Что читать |
|---|---|
| Всем | [docs/00-index.md](docs/00-index.md) — оглавление |
| Руководителю | [docs/05-roadmap.md](docs/05-roadmap.md) — этапы и что нужно от предприятия |
| Разработчику | [CLAUDE.md](CLAUDE.md), [docs/03-architecture.md](docs/03-architecture.md), [docs/adr/](docs/adr/) |
| Инженеру БД | [docs/04-db-migration-plan.md](docs/04-db-migration-plan.md) |

## Структура репозитория

```
.claude/            настройки Claude Code и суб-агенты (оркестратор, аналитик, архитектор, БД, разработчик, документатор)
.vscode/            рекомендуемые расширения, задачи сборки, запуск
.github/workflows/  CI: сборка, тесты, сборки для Windows и Linux
docs/               документация, ADR
src/                исходники (.NET 10): Domain, Data, Contracts, Server, Desktop
tests/              тесты
db/                 docker-compose стенда, инвентаризация старой базы, скрипты миграции
deploy/             терминальный сервер для Windows 7 (xrdp, запасной вариант RemoteApp)
tools/              снимки экранов без монитора, хуки
```

## Среда разработчика

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. VS Code — при открытии папки согласитесь установить рекомендуемые расширения.
3. Docker Desktop — для локального PostgreSQL (`db/docker-compose.yml`).
4. Git.
