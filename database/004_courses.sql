-- Additive, idempotent upgrade for the existing MftDb schema.
-- Review and back up the database before applying in production.
USE MftDb;
GO
IF OBJECT_ID(N'dbo.Courses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Courses
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Courses PRIMARY KEY,
        DepartmentId uniqueidentifier NOT NULL,
        Name nvarchar(200) NOT NULL,
        Code nvarchar(50) NOT NULL,
        Description nvarchar(max) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Courses_IsActive DEFAULT (1),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_Courses_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Courses_Departments FOREIGN KEY (DepartmentId)
            REFERENCES dbo.Departments(Id)
    );
    CREATE UNIQUE INDEX UX_Courses_DepartmentId_Code ON dbo.Courses(DepartmentId, Code);
END;
GO
