# Avers.Server

Сервер приложений «Аверса» (ADR-0003): ASP.NET Core 10, Minimal API, EF Core 10 поверх `Avers.Domain` и `Avers.Data`.
Рабочая база — PostgreSQL (Npgsql), учебный режим — SQLite с демо-данными Омска.
DTO для настольного клиента — в `Avers.Contracts`.

## Как запустить

Учебный режим, ничего настраивать не нужно (SQLite-файл `avers-server-demo.db` рядом с проектом, в git не попадает):

```bash
dotnet run --project src/Avers.Server
```

Сервер слушает <http://localhost:5115>. Проверка: <http://localhost:5115/health> отвечает `Healthy`.
Описание API для разработчика: <http://localhost:5115/openapi/v1.json> (только в режиме Development).

На локальном PostgreSQL из `db/docker-compose.yml` (пароль — из своего `.env`, в git его нет):

```bash
docker compose -f db/docker-compose.yml up -d
dotnet user-secrets --project src/Avers.Server set "ConnectionStrings:Avers" "Host=localhost;Database=avers;Username=avers;Password=<пароль>"
dotnet run --project src/Avers.Server -- --Avers:Database:Provider=Postgres
```

## Настройки (`appsettings.json`, раздел `Avers:Database`)

| Ключ | По умолчанию | Development | Смысл |
|---|---|---|---|
| `Provider` | `Postgres` | `Sqlite` | Какая СУБД |
| `EnsureCreated` | `false` | `true` | Создать таблицы, если их нет. До появления миграций EF — только стенд |
| `SeedDemo` | `false` | `true` | Учебные пользователи и абоненты. На рабочем сервере не включать |

Строка подключения — `ConnectionStrings:Avers`. Пароль в `appsettings.json` не писать: user-secrets или
переменная окружения `ConnectionStrings__Avers`.

## API

Вход: `POST /api/auth/login` с `{ "login": "admin", "password": "avers" }` (учебные пользователи) →
`{ "accessToken": "…", "expiresIn": 43200, … }`. Дальше заголовок `Authorization: Bearer <accessToken>`.
Попыток входа — не больше 10 в минуту с одного адреса.

| Метод | Путь | Кто может |
|---|---|---|
| GET | `/health` | все |
| GET | `/api/auth/me` | вошедший |
| GET | `/api/streets`, `/api/streets/{id}/houses` | модуль «Абоненты» (просмотр) |
| GET | `/api/subscribers?mode=Address&street=&house=&apartment=` | то же |
| GET | `/api/subscribers?mode=Account\|InnerCode\|Pzhreu\|Surname&q=` | то же |
| GET | `/api/subscribers/{id}` — карточка с начислено / оплачено / долг | то же |
| GET | `/api/subscribers/{id}/payments?year=2026` | то же |
| POST | `/api/subscribers/{id}/payments` с `{ "year": 2026 }` | администратор, начальник АДС, право правки ЛС |

Права берутся из той же матрицы, что у оболочки (`RoleCatalog`), и проверяются на сервере в каждом запросе.
Пока не решено бухгалтерией и АДС (docs/07-open-questions.md), поведение как в оболочке:
оплата 0 ₽ закрывает месяц, диспетчер карточки абонентов не видит.

Оплата без месяца закрывает первый неоплаченный месяц года суммой тарифа, источник «Касса», дата — сегодня.
Повторная оплата того же месяца — ответ 409, в том числе если два кассира нажали одновременно.
