Lottery Registration Service

This is a scaffolded .NET 8 solution with Clean Architecture layers (domain, application, infra, api). It uses PostgreSQL (EF Core) and Redis streams for background processing.

Quick start:

1. Start Postgres and Redis using Docker Compose:

```bash
docker-compose up -d
```

2. Configure connection string in `src/Lottery.Api/appsettings.json` or environment variables.

3. Build and run (requires .NET 8 SDK):

```bash
cd src
dotnet build Lottery.sln
dotnet run --project Lottery.Api
```

4. Swagger UI: `http://localhost:5000/swagger` (port depends on Kestrel defaults).

Notes:
- Use `dotnet ef` to create migrations and update the DB.
- Worker reads Redis stream `lottery:requests` and processes each message asynchronously with configurable delay.
