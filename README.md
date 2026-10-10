# Task Manager Backend

ASP.NET Core 8 API, EF Core, PostgreSQL and JWT login. Registration endpoint is intentionally absent; startup creates the demo user if it does not exist.

Demo credentials: `demo` / `demo123`.

## Local run

This project reads configuration from ASP.NET Core configuration providers. The `.env.production` file is **not automatically read by `dotnet run`**; it is intended to be installed as `/etc/taskmanager.env` on the Linux VPS and loaded by `systemd`. For local development use .NET User Secrets or temporary PowerShell environment variables.

```powershell
dotnet restore
dotnet run
```

## Secrets

- `.env.production` contains the production PostgreSQL password and JWT signing key. Never upload it to GitHub.
- `.gitignore` ignores `.env.*`; `.env.example` contains placeholders only.
- The production database role expected by the file is `taskmanager`, with database `taskmanager`.

See `DEPLOY_VPS.md` for the full Windows-to-VPS and database backup/restore process.
