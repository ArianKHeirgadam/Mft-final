USE MftDb;
IF COL_LENGTH('Users','Mobile') IS NULL ALTER TABLE Users ADD Mobile nvarchar(30) NOT NULL CONSTRAINT DF_Users_Mobile DEFAULT N'';
IF COL_LENGTH('Users','MustChangePassword') IS NULL ALTER TABLE Users ADD MustChangePassword bit NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0;
IF COL_LENGTH('Users','DepartmentId') IS NULL BEGIN ALTER TABLE Users ADD DepartmentId uniqueidentifier NULL; ALTER TABLE Users ADD CONSTRAINT FK_Users_Department FOREIGN KEY(DepartmentId) REFERENCES Departments(Id) ON DELETE NO ACTION; CREATE INDEX IX_Users_DepartmentId ON Users(DepartmentId); END