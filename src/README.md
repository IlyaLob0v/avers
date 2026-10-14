# src

Исходники оболочки перенесены из `D:\Avers\Source` как есть (ветка `import/shell-net8`, 14.10.2026):
`Avers.Domain`, `Avers.Data` (EF Core + SQLite), `Avers.Desktop` (Avalonia 11.2.8). `Avers.sln`,
`Directory.Build.props` и `global.json` лежат в корне репозитория. Целевая раскладка проектов — `docs/03-architecture.md`.

Сейчас: сборка SDK 10.0.402, `TargetFramework` = `net8.0`. Следующий шаг — ADR-0004: .NET 10 + Avalonia 12.
