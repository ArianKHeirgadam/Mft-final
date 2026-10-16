# Bootstrap the first SuperAdmin safely

The current SQL schema uses `uniqueidentifier` for `Users.Id` and stores roles as `nvarchar(30)`. Do not manually insert a plain-text password into `Users.PasswordHash`.

The API's startup bootstrap code must be built from the schema-aligned source version before using these settings. The previous `int`-key build is not compatible with the supplied database.

## Environment variables

Set these only in a local PowerShell session or user-secrets, never commit real credentials to `appsettings.json`:

```powershell
$env:Bootstrap__CreateSuperAdmin = "true"
$env:Bootstrap__Username = "arianadmin"
$env:Bootstrap__Password = "REPLACE_WITH_A_UNIQUE_STRONG_PASSWORD"
$env:Bootstrap__FullName = "System Administrator"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\src\Mft.API
```

Only run this after confirming the schema-aligned startup seed checks whether the username already exists and hashes the password with BCrypt. After the first successful startup, stop the API and set:

```powershell
$env:Bootstrap__CreateSuperAdmin = "false"
```

Then restart the API. Do not expose the temporary password in Git, logs, screenshots, or chat. If startup fails with SQL column errors, do not attempt repeated seeding; correct the entity mappings first.

## Recovery

If the seed does not create the account, check the API console for the startup exception and verify the configured `ConnectionStrings__DefaultConnection` points to the intended `MftDb` database. Do not run destructive migrations or delete existing users to recover access.
