# CloudDrive

Модульный монолит на **.NET 10**. Композиционный корень — `src/Bootstrapper/CloudDrive.Api`.

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

Строка подключения для запуска API напрямую хранится в user-secrets.

```bash
dotnet user-secrets set "ConnectionStrings:CloudDrive" \
  "Host=localhost;Port=5432;Database=clouddrive;Username=postgres;Password=postgres" \
  --project src/Bootstrapper/CloudDrive.Api
```

Проверить:

```bash
dotnet user-secrets list --project src/Bootstrapper/CloudDrive.Api
```

## Запуск


### Способ A — всё в Docker (самый простой)

Поднимает PostgreSQL **и** API одной командой. Не требует установленного .NET SDK и user-secrets.

```bash
docker compose up --build
```

- API: <http://localhost:8080>
- PostgreSQL: `localhost:5433` (внутри сети контейнеров — `clouddrive.database:5432`)

### Способ B — разработка: API на хосте + локальный PostgreSQL

API запускается на хосте через `dotnet run`, БД — **локально установленный** PostgreSQL (по умолчанию `localhost:5432`).

```bash
# убедитесь, что локальный PostgreSQL запущен, затем:
dotnet run --project src/Bootstrapper/CloudDrive.Api
```

> Не хотите ставить PostgreSQL локально? Поднимите его в Docker и укажите в user-secrets порт **5433** вместо 5432:
> ```bash
> docker compose up -d clouddrive.database
> ```
> 