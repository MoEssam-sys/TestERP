SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Branches', N'RowVersion') IS NULL ALTER TABLE dbo.Branches ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.Brands', N'RowVersion') IS NULL ALTER TABLE dbo.Brands ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.Customers', N'RowVersion') IS NULL ALTER TABLE dbo.Customers ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.Products', N'RowVersion') IS NULL ALTER TABLE dbo.Products ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.ProductUnits', N'RowVersion') IS NULL ALTER TABLE dbo.ProductUnits ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.Suppliers', N'RowVersion') IS NULL ALTER TABLE dbo.Suppliers ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.Categories', N'RowVersion') IS NULL ALTER TABLE dbo.Categories ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.Units', N'RowVersion') IS NULL ALTER TABLE dbo.Units ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.Warehouses', N'RowVersion') IS NULL ALTER TABLE dbo.Warehouses ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.SalesInvoices', N'RowVersion') IS NULL ALTER TABLE dbo.SalesInvoices ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.SalesInvoiceDetails', N'RowVersion') IS NULL ALTER TABLE dbo.SalesInvoiceDetails ADD RowVersion rowversion NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoices_Amounts')
    ALTER TABLE dbo.SalesInvoices WITH CHECK ADD CONSTRAINT CK_SalesInvoices_Amounts
    CHECK (TotalAmount >= 0 AND DiscountAmount >= 0 AND TaxAmount >= 0 AND NetAmount >= 0);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoiceDetails_Amounts')
    ALTER TABLE dbo.SalesInvoiceDetails WITH CHECK ADD CONSTRAINT CK_SalesInvoiceDetails_Amounts
    CHECK (Quantity > 0 AND UnitPrice >= 0 AND Discount >= 0 AND Tax >= 0 AND LineTotal >= 0);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SalesInvoiceDetails')
      AND name = N'IX_SalesInvoiceDetails_Company_Invoice'
)
    CREATE INDEX IX_SalesInvoiceDetails_Company_Invoice
        ON dbo.SalesInvoiceDetails(CompanyId, InvoiceId, Id);

COMMIT TRANSACTION;
