SET NOCOUNT ON;

DECLARE @Tables table (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @Tables(TableName)
VALUES
    (N'Branches'), (N'Brands'), (N'Customers'), (N'Products'),
    (N'ProductUnits'), (N'Suppliers'), (N'Categories'), (N'Units'),
    (N'Warehouses'), (N'SalesInvoices'), (N'SalesInvoiceDetails');

IF EXISTS
(
    SELECT 1
    FROM @Tables AS expected
    WHERE COL_LENGTH(N'dbo.' + expected.TableName, N'RowVersion') IS NULL
)
    THROW 51100, 'Every tenant business table must have RowVersion.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoices_Amounts')
    THROW 51101, 'The sales invoice amount constraint is missing.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoiceDetails_Amounts')
    THROW 51102, 'The sales invoice line amount constraint is missing.', 1;

DECLARE @ConcurrencyId uniqueidentifier = NEWID();
DECLARE @OriginalVersion binary(8);
EXEC sys.sp_set_session_context @key = N'CompanyId', @value = 1;

INSERT INTO dbo.Brands(CompanyId, Id, Code, Name, IsActive)
VALUES(1, @ConcurrencyId, N'__concurrency_test__', N'Concurrency test', 1);

SELECT @OriginalVersion = RowVersion
FROM dbo.Brands
WHERE CompanyId = 1 AND Id = @ConcurrencyId;

UPDATE dbo.Brands
SET Name = N'Current update'
WHERE CompanyId = 1 AND Id = @ConcurrencyId AND RowVersion = @OriginalVersion;
IF @@ROWCOUNT <> 1
    THROW 51103, 'The current row version did not update exactly one row.', 1;

UPDATE dbo.Brands
SET Name = N'Stale update'
WHERE CompanyId = 1 AND Id = @ConcurrencyId AND RowVersion = @OriginalVersion;
IF @@ROWCOUNT <> 0
    THROW 51104, 'A stale row version overwrote newer data.', 1;

DELETE FROM dbo.Brands WHERE CompanyId = 1 AND Id = @ConcurrencyId;

SELECT N'Repository hardening database contract passed.' AS Result;
