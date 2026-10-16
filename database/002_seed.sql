-- Safe baseline seed: creates one department only.
-- Do NOT seed accounts with a known password hash. Create the first SuperAdmin through the
-- Bootstrap environment variables documented in docs/BOOTSTRAP-ADMIN.md.
USE MftDb;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE Name=N'مهندسی کامپیوتر')
BEGIN
    INSERT dbo.Departments(Id,Name,ManagerName,DegreeLevel,IsActive)
    VALUES(NEWID(),N'مهندسی کامپیوتر',NULL,N'کارشناسی',1);
END;
GO
