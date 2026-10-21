---
name: avers-verify
description: Проверка решения Аверса перед сдачей работы — сборка, тесты, dotnet format, как в CI. Используй перед каждым коммитом с кодом C#, перед PR и когда суб-агент сдаёт работу.
---

# Проверка перед сдачей

CI (`.github/workflows/ci.yml`) собирает на Windows в Release и падает на любой ошибке, упавшем тесте
или неотформатированном файле. Повторяй то же самое локально, до коммита.

## Шаги

1. Если `dotnet` не найден или нет SDK 10 — в облаке выполни `.claude/hooks/session-start.sh`
   (с `CLAUDE_CODE_REMOTE=true`), на ПК — установи .NET 10 SDK. Без SDK не говори, что «всё собирается».
2. Выполни по порядку, каждую команду до конца:

   ```bash
   dotnet restore Avers.sln
   dotnet build Avers.sln -c Release --no-restore
   dotnet test Avers.sln -c Release --no-build
   dotnet format Avers.sln --verify-no-changes
   ```

3. Если упал `format --verify-no-changes`: выполни `dotnet format Avers.sln`, просмотри `git diff`
   и включи правки в тот же коммит. Стиль задаёт `.editorconfig`, руками его не обходи.
4. Если упал тест: сначала пойми причину (скилл `systematic-debugging`). Тест не удалять, не помечать `Skip`
   и не ослаблять проверку, чтобы стало зелёным.
5. Предупреждения сборки, появившиеся из-за твоего изменения, исправь.

## Отчёт

В ответе или описании PR приведи итоговые строки: `0 Error(s)`, `Passed: N` по каждому тестовому проекту,
результат `format`. Не пиши «проверено», если команда не запускалась в этой сессии.

## Особенности

- `Avers.Desktop` (Avalonia) собирается и на Linux. Окно проверяют тесты `tests/Avers.Desktop.Tests`: главное окно
  открывается без монитора (Avalonia.Headless, xUnit v3), с настоящими привязками таблиц. Поведение экрана
  (выбор дома, оплата, приём заявки) проверяй там, а не только в модели.
- Тесты базы идут на SQLite в памяти (`tests/Avers.Data.Tests/SqliteDatabase.cs`), Docker для них не нужен.
