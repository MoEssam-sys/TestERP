SET XACT_ABORT ON;

IF SCHEMA_ID(N'Security') IS NULL
    EXEC(N'CREATE SCHEMA [Security] AUTHORIZATION [dbo];');

IF COL_LENGTH(N'dbo.Branches', N'CompanyId') IS NULL ALTER TABLE dbo.Branches ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.Brands', N'CompanyId') IS NULL ALTER TABLE dbo.Brands ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.Customers', N'CompanyId') IS NULL ALTER TABLE dbo.Customers ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.Products', N'CompanyId') IS NULL ALTER TABLE dbo.Products ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.ProductUnits', N'CompanyId') IS NULL ALTER TABLE dbo.ProductUnits ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.Suppliers', N'CompanyId') IS NULL ALTER TABLE dbo.Suppliers ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.Categories', N'CompanyId') IS NULL ALTER TABLE dbo.Categories ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.Units', N'CompanyId') IS NULL ALTER TABLE dbo.Units ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.Warehouses', N'CompanyId') IS NULL ALTER TABLE dbo.Warehouses ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.SalesInvoices', N'CompanyId') IS NULL ALTER TABLE dbo.SalesInvoices ADD CompanyId int NULL;
IF COL_LENGTH(N'dbo.SalesInvoiceDetails', N'CompanyId') IS NULL ALTER TABLE dbo.SalesInvoiceDetails ADD CompanyId int NULL;
GO

BEGIN TRANSACTION;

DECLARE @LegacyUniqueObjects table (TableName sysname NOT NULL, ObjectName sysname NOT NULL);
INSERT INTO @LegacyUniqueObjects(TableName, ObjectName)
VALUES
    (N'Branches', N'UQ_Branches_Code'), (N'Branches', N'UQ_Branches_Name'),
    (N'Brands', N'UQ_Brands_Code'), (N'Brands', N'UQ_Brands_Name'),
    (N'Customers', N'UQ_Customers_Code'), (N'Customers', N'UQ_Customers_Name'),
    (N'Products', N'UQ_Products_Code'), (N'Products', N'UQ_Products_Name'),
    (N'ProductUnits', N'UQ_ProductUnits_Product_Unit'),
    (N'Suppliers', N'UQ_Suppliers_Code'), (N'Suppliers', N'UQ_Suppliers_Name'),
    (N'Categories', N'UQ_Categories_Code'), (N'Categories', N'UQ_Categories_Name'),
    (N'Units', N'UQ_Units_Code'), (N'Units', N'UQ_Units_Name'),
    (N'Warehouses', N'UQ_Warehouses_Code'), (N'Warehouses', N'UQ_Warehouses_Name'),
    (N'SalesInvoices', N'UQ_SalesInvoices_Number');

DECLARE @LegacyTableName sysname;
DECLARE @LegacyObjectName sysname;
DECLARE legacy_unique_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName, ObjectName FROM @LegacyUniqueObjects;
OPEN legacy_unique_cursor;
FETCH NEXT FROM legacy_unique_cursor INTO @LegacyTableName, @LegacyObjectName;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @DropLegacySql nvarchar(max) = NULL;
    IF EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.' + @LegacyTableName)
          AND name = @LegacyObjectName
    )
        SET @DropLegacySql = N'ALTER TABLE dbo.' + QUOTENAME(@LegacyTableName) +
            N' DROP CONSTRAINT ' + QUOTENAME(@LegacyObjectName) + N';';
    ELSE IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.' + @LegacyTableName)
          AND name = @LegacyObjectName
    )
        SET @DropLegacySql = N'DROP INDEX ' + QUOTENAME(@LegacyObjectName) +
            N' ON dbo.' + QUOTENAME(@LegacyTableName) + N';';

    IF @DropLegacySql IS NOT NULL
        EXEC sys.sp_executesql @DropLegacySql;

    FETCH NEXT FROM legacy_unique_cursor INTO @LegacyTableName, @LegacyObjectName;
END;
CLOSE legacy_unique_cursor;
DEALLOCATE legacy_unique_cursor;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branches') AND name = N'UQ_Branches_Company_Code')
    CREATE UNIQUE INDEX UQ_Branches_Company_Code ON dbo.Branches(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branches') AND name = N'UQ_Branches_Company_Name')
    CREATE UNIQUE INDEX UQ_Branches_Company_Name ON dbo.Branches(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Brands') AND name = N'UQ_Brands_Company_Code')
    CREATE UNIQUE INDEX UQ_Brands_Company_Code ON dbo.Brands(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Brands') AND name = N'UQ_Brands_Company_Name')
    CREATE UNIQUE INDEX UQ_Brands_Company_Name ON dbo.Brands(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'UQ_Customers_Company_Code')
    CREATE UNIQUE INDEX UQ_Customers_Company_Code ON dbo.Customers(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'UQ_Customers_Company_Name')
    CREATE UNIQUE INDEX UQ_Customers_Company_Name ON dbo.Customers(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'UQ_Products_Company_Code')
    CREATE UNIQUE INDEX UQ_Products_Company_Code ON dbo.Products(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'UQ_Products_Company_Name')
    CREATE UNIQUE INDEX UQ_Products_Company_Name ON dbo.Products(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductUnits') AND name = N'UQ_ProductUnits_Company_Product_Unit')
    CREATE UNIQUE INDEX UQ_ProductUnits_Company_Product_Unit ON dbo.ProductUnits(CompanyId, ProductId, UnitId) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Suppliers') AND name = N'UQ_Suppliers_Company_Code')
    CREATE UNIQUE INDEX UQ_Suppliers_Company_Code ON dbo.Suppliers(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Suppliers') AND name = N'UQ_Suppliers_Company_Name')
    CREATE UNIQUE INDEX UQ_Suppliers_Company_Name ON dbo.Suppliers(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Categories') AND name = N'UQ_Categories_Company_Code')
    CREATE UNIQUE INDEX UQ_Categories_Company_Code ON dbo.Categories(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Categories') AND name = N'UQ_Categories_Company_Name')
    CREATE UNIQUE INDEX UQ_Categories_Company_Name ON dbo.Categories(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Units') AND name = N'UQ_Units_Company_Code')
    CREATE UNIQUE INDEX UQ_Units_Company_Code ON dbo.Units(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Units') AND name = N'UQ_Units_Company_Name')
    CREATE UNIQUE INDEX UQ_Units_Company_Name ON dbo.Units(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Warehouses') AND name = N'UQ_Warehouses_Company_Code')
    CREATE UNIQUE INDEX UQ_Warehouses_Company_Code ON dbo.Warehouses(CompanyId, Code) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Warehouses') AND name = N'UQ_Warehouses_Company_Name')
    CREATE UNIQUE INDEX UQ_Warehouses_Company_Name ON dbo.Warehouses(CompanyId, Name) WHERE CompanyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SalesInvoices') AND name = N'UQ_SalesInvoices_Company_Number')
    CREATE UNIQUE INDEX UQ_SalesInvoices_Company_Number ON dbo.SalesInvoices(CompanyId, Number) WHERE CompanyId IS NOT NULL;

DECLARE @TenantTables table (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @TenantTables(TableName)
VALUES (N'Branches'), (N'Brands'), (N'Customers'), (N'Products'), (N'ProductUnits'),
       (N'Suppliers'), (N'Categories'), (N'Units'), (N'Warehouses'),
       (N'SalesInvoices'), (N'SalesInvoiceDetails');

DECLARE @TableName sysname;
DECLARE tenant_table_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @TenantTables;
OPEN tenant_table_cursor;
FETCH NEXT FROM tenant_table_cursor INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @IndexName sysname = N'UQ_' + @TableName + N'_Company_Id';
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.' + @TableName)
          AND name = @IndexName
          AND has_filter = 1
    )
    BEGIN
        DECLARE @DropFilteredIndexSql nvarchar(max) =
            N'DROP INDEX ' + QUOTENAME(@IndexName) +
            N' ON dbo.' + QUOTENAME(@TableName) + N';';
        EXEC sys.sp_executesql @DropFilteredIndexSql;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.' + @TableName)
          AND name = @IndexName
    )
    BEGIN
        DECLARE @CreateIndexSql nvarchar(max) =
            N'CREATE UNIQUE INDEX ' + QUOTENAME(@IndexName) +
            N' ON dbo.' + QUOTENAME(@TableName) +
            N'(CompanyId, Id);';
        EXEC sys.sp_executesql @CreateIndexSql;
    END;

    FETCH NEXT FROM tenant_table_cursor INTO @TableName;
END;
CLOSE tenant_table_cursor;
DEALLOCATE tenant_table_cursor;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Categories_Parent') ALTER TABLE dbo.Categories DROP CONSTRAINT FK_Categories_Parent;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Categories') ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_Categories;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Products') ALTER TABLE dbo.ProductUnits DROP CONSTRAINT FK_ProductUnits_Products;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Units') ALTER TABLE dbo.ProductUnits DROP CONSTRAINT FK_ProductUnits_Units;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceDetails_Products') ALTER TABLE dbo.SalesInvoiceDetails DROP CONSTRAINT FK_SalesInvoiceDetails_Products;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceDetails_SalesInvoices') ALTER TABLE dbo.SalesInvoiceDetails DROP CONSTRAINT FK_SalesInvoiceDetails_SalesInvoices;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Branches') ALTER TABLE dbo.SalesInvoices DROP CONSTRAINT FK_SalesInvoices_Branches;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Customers') ALTER TABLE dbo.SalesInvoices DROP CONSTRAINT FK_SalesInvoices_Customers;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Warehouses') ALTER TABLE dbo.SalesInvoices DROP CONSTRAINT FK_SalesInvoices_Warehouses;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Warehouses_Branches') ALTER TABLE dbo.Warehouses DROP CONSTRAINT FK_Warehouses_Branches;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Categories_Tenant_Parent')
    ALTER TABLE dbo.Categories WITH CHECK ADD CONSTRAINT FK_Categories_Tenant_Parent FOREIGN KEY (CompanyId, ParentId) REFERENCES dbo.Categories(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Tenant_Categories')
    ALTER TABLE dbo.Products WITH CHECK ADD CONSTRAINT FK_Products_Tenant_Categories FOREIGN KEY (CompanyId, CategoryId) REFERENCES dbo.Categories(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Tenant_Products')
    ALTER TABLE dbo.ProductUnits WITH CHECK ADD CONSTRAINT FK_ProductUnits_Tenant_Products FOREIGN KEY (CompanyId, ProductId) REFERENCES dbo.Products(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Tenant_Units')
    ALTER TABLE dbo.ProductUnits WITH CHECK ADD CONSTRAINT FK_ProductUnits_Tenant_Units FOREIGN KEY (CompanyId, UnitId) REFERENCES dbo.Units(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceDetails_Tenant_Products')
    ALTER TABLE dbo.SalesInvoiceDetails WITH CHECK ADD CONSTRAINT FK_SalesInvoiceDetails_Tenant_Products FOREIGN KEY (CompanyId, ProductId) REFERENCES dbo.Products(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceDetails_Tenant_SalesInvoices')
    ALTER TABLE dbo.SalesInvoiceDetails WITH CHECK ADD CONSTRAINT FK_SalesInvoiceDetails_Tenant_SalesInvoices FOREIGN KEY (CompanyId, InvoiceId) REFERENCES dbo.SalesInvoices(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Tenant_Branches')
    ALTER TABLE dbo.SalesInvoices WITH CHECK ADD CONSTRAINT FK_SalesInvoices_Tenant_Branches FOREIGN KEY (CompanyId, BranchId) REFERENCES dbo.Branches(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Tenant_Customers')
    ALTER TABLE dbo.SalesInvoices WITH CHECK ADD CONSTRAINT FK_SalesInvoices_Tenant_Customers FOREIGN KEY (CompanyId, CustomerId) REFERENCES dbo.Customers(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Tenant_Warehouses')
    ALTER TABLE dbo.SalesInvoices WITH CHECK ADD CONSTRAINT FK_SalesInvoices_Tenant_Warehouses FOREIGN KEY (CompanyId, WarehouseId) REFERENCES dbo.Warehouses(CompanyId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Warehouses_Tenant_Branches')
    ALTER TABLE dbo.Warehouses WITH CHECK ADD CONSTRAINT FK_Warehouses_Tenant_Branches FOREIGN KEY (CompanyId, BranchId) REFERENCES dbo.Branches(CompanyId, Id);

COMMIT TRANSACTION;
GO

IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy' AND schema_id = SCHEMA_ID(N'Security'))
    DROP SECURITY POLICY Security.TenantIsolationPolicy;
GO

CREATE OR ALTER FUNCTION Security.fn_TenantAccessPredicate(@CompanyId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
(
    SELECT 1 AS IsAllowed
    WHERE @CompanyId = TRY_CONVERT(int, SESSION_CONTEXT(N'CompanyId'))
);
GO

CREATE SECURITY POLICY Security.TenantIsolationPolicy
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Branches,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Branches AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Branches AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Brands,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Brands AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Brands AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Customers,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Customers AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Customers AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Products,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Products AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Products AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.ProductUnits,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.ProductUnits AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.ProductUnits AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Suppliers,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Suppliers AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Suppliers AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Categories,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Categories AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Categories AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Units,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Units AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Units AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Warehouses,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Warehouses AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.Warehouses AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.SalesInvoices,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.SalesInvoices AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.SalesInvoices AFTER UPDATE,
ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.SalesInvoiceDetails,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.SalesInvoiceDetails AFTER INSERT,
ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(CompanyId) ON dbo.SalesInvoiceDetails AFTER UPDATE
WITH (STATE = ON, SCHEMABINDING = ON);
GO
