# src

| Проект | Что внутри |
|---|---|
| `Avers.Domain` | Сущности и правила: абоненты, дома, оплаты, долг, заявки, роли |
| `Avers.Data` | EF Core: PostgreSQL на сервере, SQLite в учебном режиме; учебные данные Омска |
| `Avers.Contracts` | DTO API, общие для сервера и Desktop |
| `Avers.Server` | Сервер приложений ASP.NET Core 10, см. [Avers.Server/README.md](Avers.Server/README.md) |
| `Avers.Desktop` | Рабочее место на Avalonia 12 |

Версии (ADR-0004): `net10.0`, Avalonia 12.1.4 (DataGrid 12.1.2), EF Core 10.0.13, CommunityToolkit.Mvvm 8.4.2.
`Avers.sln`, `Directory.Build.props` и `global.json` лежат в корне репозитория. Целевая раскладка — `docs/03-architecture.md`.

Исходный вид оболочки (Avalonia 11, .NET 8, перенесён из `D:\Avers\Source` 14.10.2026) — ветка `import/shell-net8`.
