SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TenantOne int = 1;
DECLARE @TenantTwo int = 2;
DECLARE @TenantOneBrandId uniqueidentifier = NEWID();
DECLARE @TenantTwoBrandId uniqueidentifier = NEWID();
DECLARE @AuditUser uniqueidentifier = NEWID();
DECLARE @TestCode nvarchar(100) = N'__tenant_isolation_test__';
DECLARE @TestName nvarchar(100) = N'__tenant_isolation_test__';

IF @TenantOne = @TenantTwo
    THROW 52000, 'Tenant test requires two different companies.', 1;

BEGIN TRANSACTION;

EXEC sys.sp_set_session_context @key = N'CompanyId', @value = @TenantOne;

INSERT INTO dbo.Brands(Id, CompanyId, Code, Name, IsActive, CreatedAt, CreatedBy)
VALUES(@TenantOneBrandId, @TenantOne, @TestCode, @TestName, 1, SYSUTCDATETIME(), @AuditUser);

IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Id = @TenantOneBrandId)
    THROW 52001, 'Tenant one cannot read its own row.', 1;

EXEC sys.sp_set_session_context @key = N'CompanyId', @value = @TenantTwo;

IF EXISTS (SELECT 1 FROM dbo.Brands WHERE Id = @TenantOneBrandId)
    THROW 52002, 'Tenant two can read tenant one data.', 1;

UPDATE dbo.Brands SET Name = N'forbidden-update' WHERE Id = @TenantOneBrandId;
IF @@ROWCOUNT <> 0
    THROW 52003, 'Tenant two updated tenant one data.', 1;

DELETE FROM dbo.Brands WHERE Id = @TenantOneBrandId;
IF @@ROWCOUNT <> 0
    THROW 52004, 'Tenant two deleted tenant one data.', 1;

INSERT INTO dbo.Brands(Id, CompanyId, Code, Name, IsActive, CreatedAt, CreatedBy)
VALUES(@TenantTwoBrandId, @TenantTwo, @TestCode, @TestName, 1, SYSUTCDATETIME(), @AuditUser);

IF (SELECT COUNT(*) FROM dbo.Brands WHERE Code = @TestCode) <> 1
    THROW 52005, 'Tenant two received rows outside its scope.', 1;

EXEC sys.sp_set_session_context @key = N'CompanyId', @value = @TenantOne;

IF (SELECT COUNT(*) FROM dbo.Brands WHERE Code = @TestCode) <> 1
    THROW 52006, 'Tenant one received rows outside its scope.', 1;

ROLLBACK TRANSACTION;

SELECT N'Tenant isolation verification passed.' AS Result;
