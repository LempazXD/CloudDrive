# CloudDrive

**CloudDrive** — облачное хранилище файлов: сервис, позволяющий загружать, хранить и организовывать файлы в папках, а также управлять доступом к ним.

Приложение построено как **REST API на ASP.NET Core (.NET 10)**, использует **PostgreSQL** в качестве базы данных и отдаёт ответы в формате [ProblemDetails (RFC 9457)](https://www.rfc-editor.org/rfc/rfc9457) с локализацией сообщений об ошибках (`ru` / `en`). Интерактивная документация API — через [Scalar](https://scalar.com/) поверх OpenAPI.

Архитектурно: **модульный монолит**: композиционный корень — `src/Bootstrapper/CloudDrive.Api`, бизнес-функциональность выносится в независимые модули (`src/Modules/`), а общие примитивы — в `src/Shared/`.

> ⚠️ **Статус: ранняя стадия.** Заложены инфраструктура и кросс-срезные механизмы (обработка ошибок, локализация, health-checks, подключение к БД, OpenAPI, логирование и трассировка через Seq); модуль **Auth** (регистрация, вход, JWT + refresh-токены) реализован.

## Технологии

| Категория | Технологии |
|---|---|
| Backend | ASP.NET Core Minimal API (.NET 10) |
| База данных | PostgreSQL, EF Core, Npgsql |
| Аутентификация | ASP.NET Core Identity, JWT |
| Ошибки | ProblemDetails (RFC 9457) с локализацией (`ru` / `en`) |
| Документация API | OpenAPI, [Scalar](https://scalar.com/) |
| Логирование | Serilog → [Seq](https://datalust.co/seq) |
| Трассировка и метрики | OpenTelemetry (OTLP → Seq) |
| Health checks | `AspNetCore.HealthChecks.NpgSql` |
| Тестирование | xUnit, NSubstitute |
| Контейнеризация | Docker, Docker Compose |

## Требования

| Инструмент | Зачем | Проверка |
|---|---|---|
| **.NET 10 SDK** | сборка и запуск API на хосте | `dotnet --version` |
| **Docker Desktop** | PostgreSQL (и/или запуск API в контейнере) | `docker version` |

## Первоначальная настройка

### 1. Файл `.env` (для Docker)

`docker compose` читает `.env` из корня репозитория. Скопируйте шаблон и при необходимости задайте пароль:

```bash
cp .env.example .env
```

### 2. User-secrets (для `dotnet run` на хосте)

Строка подключения для запуска API напрямую хранится в user-secrets. Порт `5433` указывает на PostgreSQL, поднятый в Docker (см. способ B); для **локально установленного** PostgreSQL используйте `5432`.

```bash
dotnet user-secrets set "ConnectionStrings:CloudDrive" \
  "Host=localhost;Port=5433;Database=clouddrive;Username=postgres;Password=postgres" \
  --project src/Bootstrapper/CloudDrive.Api
```

Ключ подписи JWT — Base64-строка, декодирующая не менее чем в 32 байта. Без него приложение упадёт при старте:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<base64>" --project src/Bootstrapper/CloudDrive.Api
```

## Запуск

### Способ A — всё в Docker

Поднимает PostgreSQL **и** API одной командой. Не требует установленного .NET SDK и user-secrets.

```bash
docker compose up --build
```

- API: <http://localhost:8080>
- Scalar UI: <http://localhost:8080/scalar>
- PostgreSQL: `localhost:5433` (внутри сети контейнеров — `clouddrive.database:5432`)
- Seq (логи, трейсы, метрики): <http://localhost:5341>

> Scalar UI (`/scalar`) и документ OpenAPI доступны только в окружении Development (`app.Environment.IsDevelopment()`). В `docker-compose.yaml` для сервиса `clouddrive.api` задан `ASPNETCORE_ENVIRONMENT=Development`, поэтому в локальном стеке они доступны. Сам образ окружение не фиксирует (по умолчанию Production) — при развёртывании в другом окружении Scalar/OpenAPI, а также Developer Exception Page, включаться не будут.

### Способ B — разработка: API на хосте + PostgreSQL и Seq в Docker

БД и Seq поднимаются в контейнерах, API запускается на хосте через `dotnet run`. Требует оба user-secret'а из шага 2 (строку подключения — с портом `5433`, и ключ подписи JWT).

```bash
# 1. поднять БД и Seq, дождаться готовности
docker compose up -d clouddrive.database clouddrive.seq

# 2. запустить API на хосте
dotnet run --project src/Bootstrapper/CloudDrive.Api
```

- API: <http://localhost:5166> (профиль `http`; профиль `https` добавляет <https://localhost:7217>)
- Scalar UI: <http://localhost:5166/scalar>
- PostgreSQL: `localhost:5433`
- Seq (логи, трейсы, метрики): <http://localhost:5341>

> Альтернатива — **локально установленный** PostgreSQL вместо контейнера: запустите его на `localhost:5432` и укажите порт `5432` в user-secret (шаг 2).
