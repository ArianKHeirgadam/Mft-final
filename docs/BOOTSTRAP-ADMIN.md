# Provision the first SuperAdmin

The API uses BCrypt password hashes, `Guid` user IDs, and the existing `MftDb` schema. No default administrator password is committed to the repository.

## 1. Open PowerShell at the repository root

Set the SQL Server connection string to the instance where your existing `MftDb` database lives. Example below uses the instance name from the original project configuration; adjust it if your server differs.

```powershell
$env:ConnectionStrings__Default = "Data Source=arianslaptop\serverarian;Initial Catalog=MftDb;Integrated Security=True;Trust Server Certificate=True"
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$env:Jwt__Key = [Convert]::ToBase64String($bytes)
$rng.Dispose()
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Bootstrap__CreateSuperAdmin = "true"
$env:Bootstrap__Username = "arianadmin"
$env:Bootstrap__Password = Read-Host "Enter a unique strong password (at least 12 characters)"
$env:Bootstrap__FullName = "System Administrator"
dotnet run --project .\src\Mft.Api
```

The account is created only if the username does not already exist. The startup routine hashes the password with BCrypt and sets `MustChangePassword=true`. If the username already exists with a non-SuperAdmin role, startup fails safely rather than elevating that account.

## 2. Disable bootstrap after the first successful startup

Stop the API with `Ctrl+C`, then run:

```powershell
$env:Bootstrap__CreateSuperAdmin = "false"
dotnet run --project .\src\Mft.Api
```

Keep the generated `Jwt__Key` private and stable for this environment. If you restart PowerShell later, re-load the same JWT key securely; generating a new key invalidates existing tokens. Store it in .NET user-secrets or an environment-specific secret store for persistent local development.

## Courses schema

Before using `/api/courses`, run `database/004_courses.sql` once against `MftDb`. This is an additive script and is not automatically run by the API. Back up the database before applying schema changes in a production environment.

Never run `database/002_seed.sql` from older project versions that inserted accounts with known demo password hashes. The current version only seeds a department and provisions the administrator through the secure startup flow.
