SET XACT_ABORT ON;

BEGIN TRANSACTION;

ALTER SECURITY POLICY Security.TenantIsolationPolicy WITH (STATE = OFF);

IF EXISTS
(
    SELECT 1 FROM dbo.Branches WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.Brands WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.Customers WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.Products WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.ProductUnits WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.Suppliers WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.Categories WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.Units WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.Warehouses WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.SalesInvoices WHERE CompanyId IS NULL
    UNION ALL SELECT 1 FROM dbo.SalesInvoiceDetails WHERE CompanyId IS NULL
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51002, 'Assign every legacy row to a company before making CompanyId required.', 1;
END;

DROP SECURITY POLICY Security.TenantIsolationPolicy;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Categories_Tenant_Parent') ALTER TABLE dbo.Categories DROP CONSTRAINT FK_Categories_Tenant_Parent;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Tenant_Categories') ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_Tenant_Categories;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Tenant_Products') ALTER TABLE dbo.ProductUnits DROP CONSTRAINT FK_ProductUnits_Tenant_Products;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Tenant_Units') ALTER TABLE dbo.ProductUnits DROP CONSTRAINT FK_ProductUnits_Tenant_Units;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceDetails_Tenant_Products') ALTER TABLE dbo.SalesInvoiceDetails DROP CONSTRAINT FK_SalesInvoiceDetails_Tenant_Products;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceDetails_Tenant_SalesInvoices') ALTER TABLE dbo.SalesInvoiceDetails DROP CONSTRAINT FK_SalesInvoiceDetails_Tenant_SalesInvoices;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Tenant_Branches') ALTER TABLE dbo.SalesInvoices DROP CONSTRAINT FK_SalesInvoices_Tenant_Branches;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Tenant_Customers') ALTER TABLE dbo.SalesInvoices DROP CONSTRAINT FK_SalesInvoices_Tenant_Customers;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoices_Tenant_Warehouses') ALTER TABLE dbo.SalesInvoices DROP CONSTRAINT FK_SalesInvoices_Tenant_Warehouses;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Warehouses_Tenant_Branches') ALTER TABLE dbo.Warehouses DROP CONSTRAINT FK_Warehouses_Tenant_Branches;

DROP INDEX IF EXISTS UQ_Branches_Company_Code ON dbo.Branches;
DROP INDEX IF EXISTS UQ_Branches_Company_Name ON dbo.Branches;
DROP INDEX IF EXISTS UQ_Brands_Company_Code ON dbo.Brands;
DROP INDEX IF EXISTS UQ_Brands_Company_Name ON dbo.Brands;
DROP INDEX IF EXISTS UQ_Customers_Company_Code ON dbo.Customers;
DROP INDEX IF EXISTS UQ_Customers_Company_Name ON dbo.Customers;
DROP INDEX IF EXISTS UQ_Products_Company_Code ON dbo.Products;
DROP INDEX IF EXISTS UQ_Products_Company_Name ON dbo.Products;
DROP INDEX IF EXISTS UQ_ProductUnits_Company_Product_Unit ON dbo.ProductUnits;
DROP INDEX IF EXISTS UQ_Suppliers_Company_Code ON dbo.Suppliers;
DROP INDEX IF EXISTS UQ_Suppliers_Company_Name ON dbo.Suppliers;
DROP INDEX IF EXISTS UQ_Categories_Company_Code ON dbo.Categories;
DROP INDEX IF EXISTS UQ_Categories_Company_Name ON dbo.Categories;
DROP INDEX IF EXISTS UQ_Units_Company_Code ON dbo.Units;
DROP INDEX IF EXISTS UQ_Units_Company_Name ON dbo.Units;
DROP INDEX IF EXISTS UQ_Warehouses_Company_Code ON dbo.Warehouses;
DROP INDEX IF EXISTS UQ_Warehouses_Company_Name ON dbo.Warehouses;
DROP INDEX IF EXISTS UQ_SalesInvoices_Company_Number ON dbo.SalesInvoices;

DROP INDEX IF EXISTS UQ_Branches_Company_Id ON dbo.Branches;
DROP INDEX IF EXISTS UQ_Brands_Company_Id ON dbo.Brands;
DROP INDEX IF EXISTS UQ_Customers_Company_Id ON dbo.Customers;
DROP INDEX IF EXISTS UQ_Products_Company_Id ON dbo.Products;
DROP INDEX IF EXISTS UQ_ProductUnits_Company_Id ON dbo.ProductUnits;
DROP INDEX IF EXISTS UQ_Suppliers_Company_Id ON dbo.Suppliers;
DROP INDEX IF EXISTS UQ_Categories_Company_Id ON dbo.Categories;
DROP INDEX IF EXISTS UQ_Units_Company_Id ON dbo.Units;
DROP INDEX IF EXISTS UQ_Warehouses_Company_Id ON dbo.Warehouses;
DROP INDEX IF EXISTS UQ_SalesInvoices_Company_Id ON dbo.SalesInvoices;
DROP INDEX IF EXISTS UQ_SalesInvoiceDetails_Company_Id ON dbo.SalesInvoiceDetails;

ALTER TABLE dbo.Branches ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.Brands ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.Customers ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.Products ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.ProductUnits ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.Suppliers ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.Categories ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.Units ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.Warehouses ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.SalesInvoices ALTER COLUMN CompanyId int NOT NULL;
ALTER TABLE dbo.SalesInvoiceDetails ALTER COLUMN CompanyId int NOT NULL;

CREATE UNIQUE INDEX UQ_Branches_Company_Id ON dbo.Branches(CompanyId, Id);
CREATE UNIQUE INDEX UQ_Brands_Company_Id ON dbo.Brands(CompanyId, Id);
CREATE UNIQUE INDEX UQ_Customers_Company_Id ON dbo.Customers(CompanyId, Id);
CREATE UNIQUE INDEX UQ_Products_Company_Id ON dbo.Products(CompanyId, Id);
CREATE UNIQUE INDEX UQ_ProductUnits_Company_Id ON dbo.ProductUnits(CompanyId, Id);
CREATE UNIQUE INDEX UQ_Suppliers_Company_Id ON dbo.Suppliers(CompanyId, Id);
CREATE UNIQUE INDEX UQ_Categories_Company_Id ON dbo.Categories(CompanyId, Id);
CREATE UNIQUE INDEX UQ_Units_Company_Id ON dbo.Units(CompanyId, Id);
CREATE UNIQUE INDEX UQ_Warehouses_Company_Id ON dbo.Warehouses(CompanyId, Id);
CREATE UNIQUE INDEX UQ_SalesInvoices_Company_Id ON dbo.SalesInvoices(CompanyId, Id);
CREATE UNIQUE INDEX UQ_SalesInvoiceDetails_Company_Id ON dbo.SalesInvoiceDetails(CompanyId, Id);

CREATE UNIQUE INDEX UQ_Branches_Company_Code ON dbo.Branches(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Branches_Company_Name ON dbo.Branches(CompanyId, Name);
CREATE UNIQUE INDEX UQ_Brands_Company_Code ON dbo.Brands(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Brands_Company_Name ON dbo.Brands(CompanyId, Name);
CREATE UNIQUE INDEX UQ_Customers_Company_Code ON dbo.Customers(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Customers_Company_Name ON dbo.Customers(CompanyId, Name);
CREATE UNIQUE INDEX UQ_Products_Company_Code ON dbo.Products(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Products_Company_Name ON dbo.Products(CompanyId, Name);
CREATE UNIQUE INDEX UQ_ProductUnits_Company_Product_Unit ON dbo.ProductUnits(CompanyId, ProductId, UnitId);
CREATE UNIQUE INDEX UQ_Suppliers_Company_Code ON dbo.Suppliers(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Suppliers_Company_Name ON dbo.Suppliers(CompanyId, Name);
CREATE UNIQUE INDEX UQ_Categories_Company_Code ON dbo.Categories(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Categories_Company_Name ON dbo.Categories(CompanyId, Name);
CREATE UNIQUE INDEX UQ_Units_Company_Code ON dbo.Units(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Units_Company_Name ON dbo.Units(CompanyId, Name);
CREATE UNIQUE INDEX UQ_Warehouses_Company_Code ON dbo.Warehouses(CompanyId, Code);
CREATE UNIQUE INDEX UQ_Warehouses_Company_Name ON dbo.Warehouses(CompanyId, Name);
CREATE UNIQUE INDEX UQ_SalesInvoices_Company_Number ON dbo.SalesInvoices(CompanyId, Number);

ALTER TABLE dbo.Categories WITH CHECK ADD CONSTRAINT FK_Categories_Tenant_Parent FOREIGN KEY (CompanyId, ParentId) REFERENCES dbo.Categories(CompanyId, Id);
ALTER TABLE dbo.Products WITH CHECK ADD CONSTRAINT FK_Products_Tenant_Categories FOREIGN KEY (CompanyId, CategoryId) REFERENCES dbo.Categories(CompanyId, Id);
ALTER TABLE dbo.ProductUnits WITH CHECK ADD CONSTRAINT FK_ProductUnits_Tenant_Products FOREIGN KEY (CompanyId, ProductId) REFERENCES dbo.Products(CompanyId, Id);
ALTER TABLE dbo.ProductUnits WITH CHECK ADD CONSTRAINT FK_ProductUnits_Tenant_Units FOREIGN KEY (CompanyId, UnitId) REFERENCES dbo.Units(CompanyId, Id);
ALTER TABLE dbo.SalesInvoiceDetails WITH CHECK ADD CONSTRAINT FK_SalesInvoiceDetails_Tenant_Products FOREIGN KEY (CompanyId, ProductId) REFERENCES dbo.Products(CompanyId, Id);
ALTER TABLE dbo.SalesInvoiceDetails WITH CHECK ADD CONSTRAINT FK_SalesInvoiceDetails_Tenant_SalesInvoices FOREIGN KEY (CompanyId, InvoiceId) REFERENCES dbo.SalesInvoices(CompanyId, Id);
ALTER TABLE dbo.SalesInvoices WITH CHECK ADD CONSTRAINT FK_SalesInvoices_Tenant_Branches FOREIGN KEY (CompanyId, BranchId) REFERENCES dbo.Branches(CompanyId, Id);
ALTER TABLE dbo.SalesInvoices WITH CHECK ADD CONSTRAINT FK_SalesInvoices_Tenant_Customers FOREIGN KEY (CompanyId, CustomerId) REFERENCES dbo.Customers(CompanyId, Id);
ALTER TABLE dbo.SalesInvoices WITH CHECK ADD CONSTRAINT FK_SalesInvoices_Tenant_Warehouses FOREIGN KEY (CompanyId, WarehouseId) REFERENCES dbo.Warehouses(CompanyId, Id);
ALTER TABLE dbo.Warehouses WITH CHECK ADD CONSTRAINT FK_Warehouses_Tenant_Branches FOREIGN KEY (CompanyId, BranchId) REFERENCES dbo.Branches(CompanyId, Id);

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

COMMIT TRANSACTION;
