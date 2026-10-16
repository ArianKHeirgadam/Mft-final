# Mft Final

ASP.NET Core 8 API and frontend for department-based exam management.

> Security note: never commit GitHub tokens, database passwords, or JWT signing keys. Revoke any token exposed in chat and use GitHub's connected authorization or a local credential manager.

## Required role behavior

| Role | Allowed actions |
|---|---|
| SuperAdmin | Manage departments and department managers; manage users; manage courses/subjects; inspect all exams, questions, attempts and results |
| Department | Manage teachers, students and courses within its own department; manage department questions and exams as defined by policy |
| Teacher | Create/manage their students, questions/answer keys and exams; view attempts/results for their exams |
| Student | View assigned/published exams and submit answers only; cannot create users, courses, questions, exams, or answer keys |

Every protected endpoint must enforce authorization server-side. Hiding frontend controls is not authorization.

## API source and endpoint contract

See [API-ENDPOINTS.md](API-ENDPOINTS.md) for the intended route contract and authorization matrix.

## Local development

1. Install the .NET 8 SDK and SQL Server.
2. Set connection string and JWT signing key using user-secrets or environment variables; do not commit secrets.
3. Set the bootstrap super-admin credentials using environment variables or user-secrets for first startup only.
4. Run `dotnet build .\\src\\Mft.Api` and `dotnet run --project .\\src\\Mft.Api`.
5. After the first administrator is provisioned, disable bootstrap creation and restart the API.

## Important

The supplied SQL schema has separate `Students` and `Teachers` tables and uses `uniqueidentifier` primary keys. Entity models and EF mappings must match those facts. Courses are added by the additive script `database/004_courses.sql`; run it once against `MftDb` before using course routes. The API does not apply schema changes automatically. See `docs/BOOTSTRAP-ADMIN.md` for the first-admin and local environment setup.
