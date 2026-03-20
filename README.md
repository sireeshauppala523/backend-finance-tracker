# Backend Finance Tracker

ASP.NET Core 8 Web API for the Personal Finance Tracker application.

## Stack

- C# / ASP.NET Core 8
- Entity Framework Core
- PostgreSQL / Azure Database for PostgreSQL Flexible Server
- JWT authentication

## Main files

- [Program.cs](d:/personalFinanceTracker/backend-finance-tracker/Program.cs)
- [appsettings.json](d:/personalFinanceTracker/backend-finance-tracker/appsettings.json)
- [appsettings.Development.json](d:/personalFinanceTracker/backend-finance-tracker/appsettings.Development.json)
- [Containerfile](d:/personalFinanceTracker/backend-finance-tracker/Containerfile)

## Local run

```powershell
cd backend-finance-tracker
dotnet restore
dotnet run
```

Default local URL from [launchSettings.json](d:/personalFinanceTracker/backend-finance-tracker/Properties/launchSettings.json):

- `http://localhost:5000`

## Local configuration

Use one of these:

1. [appsettings.Development.json](d:/personalFinanceTracker/backend-finance-tracker/appsettings.Development.json)
2. environment variables
3. .NET user secrets

Recommended local user-secrets setup:

```powershell
cd backend-finance-tracker
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=personal_finance_tracker;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
dotnet user-secrets set "Jwt:Key" "YOUR_LONG_RANDOM_LOCAL_SECRET_KEY"
```

## Required Azure settings

Set these in the backend Azure Web App configuration:

- `ASPNETCORE_ENVIRONMENT=Development` for Dev/Test plan, or `Production` later if needed
- `ConnectionStrings__DefaultConnection=Host=your-financeadmin.postgres.database.azure.com;Port=5432;Database=finance_tracker;Username=finance_admin;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true`
- `Jwt__Issuer=PersonalFinanceTracker`
- `Jwt__Audience=PersonalFinanceTrackerClient`
- `Jwt__Key=YOUR_LONG_RANDOM_SECRET`
- `Cors__AllowedOrigins__0=https://YOUR_FRONTEND_HOST`
- `WEBSITES_PORT=8080`

## Database notes

The app now:

- uses EF Core migrations when they exist
- falls back to `EnsureCreated()` if the repo has no generated migrations yet
- seeds default categories on startup

A local .NET tool manifest for EF Core is included at [dotnet-tools.json](d:/personalFinanceTracker/backend-finance-tracker/.config/dotnet-tools.json).

Before creating migrations on your machine:

```powershell
cd d:\personalFinanceTracker\backend-finance-tracker
dotnet tool restore
```

Then you can generate migrations with:

```powershell
cd backend-finance-tracker
dotnet ef migrations add InitialCreate
```

## Podman container

Build locally with Podman:

```powershell
cd backend-finance-tracker
podman build -t backend-finance-tracker:local -f Containerfile .
```

Run locally:

```powershell
podman run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=...;Username=...;Password=..." \
  -e Jwt__Issuer="PersonalFinanceTracker" \
  -e Jwt__Audience="PersonalFinanceTrackerClient" \
  -e Jwt__Key="YOUR_LONG_RANDOM_SECRET" \
  backend-finance-tracker:local
```

## Azure pipeline

Shared pipeline file:

- [azure-pipelines.yml](d:/personalFinanceTracker/backend-finance-tracker/azure-pipelines.yml)

It builds this API, runs backend tests, builds a Podman image, pushes to ACR, and deploys to Azure Web App for Containers.


