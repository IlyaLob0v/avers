# src

Исходники оболочки перенесены из `D:\Avers\Source` как есть (ветка `import/shell-net8`, 14.10.2026):
`Avers.Domain`, `Avers.Data` (EF Core + SQLite), `Avers.Desktop` (Avalonia 11.2.8). `Avers.sln`,
`Directory.Build.props` и `global.json` лежат в корне репозитория. Целевая раскладка проектов — `docs/03-architecture.md`.

Сейчас (ADR-0004): `net10.0`, Avalonia 12.1.4 (DataGrid 12.1.2), EF Core 10.0.13, CommunityToolkit.Mvvm 8.4.2.

Сервер приложений — `Avers.Server` (ASP.NET Core 10, PostgreSQL через Npgsql), DTO — `Avers.Contracts`; см. `src/Avers.Server/README.md`.
