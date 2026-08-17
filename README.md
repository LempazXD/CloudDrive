# CloudDrive

**CloudDrive** — облачное хранилище файлов: сервис, позволяющий загружать, хранить и организовывать файлы в папках, а также управлять доступом к ним.

Приложение построено как **REST API на ASP.NET Core (.NET 10)**, использует **PostgreSQL** в качестве базы данных и отдаёт ответы в формате [ProblemDetails (RFC 9457)](https://www.rfc-editor.org/rfc/rfc9457) с локализацией сообщений об ошибках (`ru` / `en`). Интерактивная документация API — через [Scalar](https://scalar.com/) поверх OpenAPI.

Архитектурно: **модульный монолит**: композиционный корень — `src/Bootstrapper/CloudDrive.Api`, бизнес-функциональность выносится в независимые модули (`src/Modules/`), а общие примитивы — в `src/Shared/`. Реализованы модули **Auth** (регистрация с подтверждением email, вход, JWT + refresh-токены) и **Files** (папки, загрузка и скачивание файлов через presigned URL в S3-совместимое объектное хранилище).

## Стек

| Категория | Стек |
|---|---|
| Backend | ASP.NET Core Minimal API (.NET 10) |
| База данных | PostgreSQL, EF Core, Npgsql |
| Объектное хранилище | SeaweedFS (S3-совместимое), AWSSDK.S3 |
| Аутентификация | ASP.NET Core Identity, JWT |
| Email | MailKit, [Mailpit](https://mailpit.axllent.org/) (локальный перехват писем) |
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

Объектное хранилище (SeaweedFS) — endpoint, ключи доступа и имя бакета. Порт `8333` указывает на SeaweedFS, поднятый в Docker (см. способ B):

```bash
dotnet user-secrets set "ObjectStorage:Endpoint" "http://localhost:8333" --project src/Bootstrapper/CloudDrive.Api
dotnet user-secrets set "ObjectStorage:AccessKey" "<access-key>" --project src/Bootstrapper/CloudDrive.Api
dotnet user-secrets set "ObjectStorage:SecretKey" "<secret-key>" --project src/Bootstrapper/CloudDrive.Api
dotnet user-secrets set "ObjectStorage:Bucket" "<bucket-name>" --project src/Bootstrapper/CloudDrive.Api
```

## Запуск

### Способ A — всё в Docker

Поднимает PostgreSQL, объектное хранилище **и** API одной командой. Не требует установленного .NET SDK и user-secrets.

```bash
docker compose up --build
```

- API: <http://localhost:8080>
- Scalar UI: <http://localhost:8080/scalar>
- PostgreSQL: `localhost:5433` (внутри сети контейнеров — `clouddrive.database:5432`)
- Объектное хранилище (S3 API): `localhost:8333` (внутри сети контейнеров — `clouddrive.storage:8333`)
- Seq (логи, трейсы, метрики): <http://localhost:5341>
- Mailpit (письма с кодом подтверждения регистрации): <http://localhost:8025>

> Scalar UI (`/scalar`) и документ OpenAPI доступны только в окружении Development (`app.Environment.IsDevelopment()`). В `docker-compose.yaml` для сервиса `clouddrive.api` задан `ASPNETCORE_ENVIRONMENT=Development`, поэтому в локальном стеке они доступны. Сам образ окружение не фиксирует (по умолчанию Production) — при развёртывании в другом окружении Scalar/OpenAPI, а также Developer Exception Page, включаться не будут.

### Способ B — разработка: API на хосте + PostgreSQL, Seq и объектное хранилище в Docker

БД, Seq, объектное хранилище и локальный перехватчик почты поднимаются в контейнерах, API запускается на хосте через `dotnet run`. Требует все user-secret'ы из шага 2 (строку подключения — с портом `5433`, ключ подписи JWT, и настройки объектного хранилища — с портом `8333`); для почты (Mailpit) user-secrets не нужны — `Smtp:Host`/`Smtp:Port` по умолчанию указывают на неё в `appsettings.Development.json`.

```bash
# 1. поднять БД, Seq, объектное хранилище и Mailpit, дождаться готовности
docker compose up -d clouddrive.database clouddrive.seq clouddrive.storage clouddrive.mail

# 2. запустить API на хосте
dotnet run --project src/Bootstrapper/CloudDrive.Api
```

- API: <http://localhost:5166> (профиль `http`; профиль `https` добавляет <https://localhost:7217>)
- Scalar UI: <http://localhost:5166/scalar>
- PostgreSQL: `localhost:5433`
- Объектное хранилище (S3 API): `localhost:8333`
- Seq (логи, трейсы, метрики): <http://localhost:5341>
- Mailpit (письма с кодом подтверждения регистрации): <http://localhost:8025>

> Альтернатива — **локально установленный** PostgreSQL вместо контейнера: запустите его на `localhost:5432` и укажите порт `5432` в user-secret (шаг 2).
