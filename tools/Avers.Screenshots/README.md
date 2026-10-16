# Avers.Screenshots

Снимки экранов Аверса без монитора (Avalonia Headless + Skia): учебная база во временном файле,
вход под учебным пользователем, переход по модулям, PNG в указанную папку.

```bash
dotnet run --project tools/Avers.Screenshots -- shots              # admin, основные экраны
dotnet run --project tools/Avers.Screenshots -- shots sokolova Desktop Disp
dotnet run --project tools/Avers.Screenshots -- shots admin Abon --size=1366x768   # размер окна
```

Подробнее — скилл `.claude/skills/avers-ui`.
