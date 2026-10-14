SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_ID(N'$(DatabaseName)') IS NOT NULL
    THROW 51200, 'The isolated test database already exists.', 1;

CREATE DATABASE [$(DatabaseName)];
GO

USE [$(DatabaseName)];
GO

CREATE TABLE dbo.Branches
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Branches PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Branches_IsActive DEFAULT 1
);

CREATE TABLE dbo.Brands
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Brands PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Brands_IsActive DEFAULT 1
);

CREATE TABLE dbo.Customers
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT 1
);

CREATE TABLE dbo.Suppliers
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT 1
);

CREATE TABLE dbo.Categories
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    ParentId uniqueidentifier NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1
);

CREATE TABLE dbo.Units
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Units PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Units_IsActive DEFAULT 1
);

CREATE TABLE dbo.Products
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    CategoryId uniqueidentifier NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1
);

CREATE TABLE dbo.ProductUnits
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ProductUnits PRIMARY KEY,
    ProductId uniqueidentifier NOT NULL,
    UnitId uniqueidentifier NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_ProductUnits_IsActive DEFAULT 1
);

CREATE TABLE dbo.Warehouses
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Warehouses PRIMARY KEY,
    Code nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    BranchId uniqueidentifier NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Warehouses_IsActive DEFAULT 1
);

CREATE TABLE dbo.SalesInvoices
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_SalesInvoices PRIMARY KEY,
    Number nvarchar(50) NOT NULL,
    BranchId uniqueidentifier NOT NULL,
    WarehouseId uniqueidentifier NOT NULL,
    CustomerId uniqueidentifier NOT NULL,
    TotalAmount decimal(18, 2) NOT NULL,
    DiscountAmount decimal(18, 2) NOT NULL,
    TaxAmount decimal(18, 2) NOT NULL,
    NetAmount decimal(18, 2) NOT NULL
);

CREATE TABLE dbo.SalesInvoiceDetails
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_SalesInvoiceDetails PRIMARY KEY,
    InvoiceId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NOT NULL,
    Quantity decimal(18, 4) NOT NULL,
    UnitPrice decimal(18, 2) NOT NULL,
    Discount decimal(18, 2) NOT NULL,
    Tax decimal(18, 2) NOT NULL,
    LineTotal decimal(18, 2) NOT NULL
);

INSERT INTO dbo.Brands(Id, Code, Name, IsActive)
VALUES('25a937d3-cee7-f011-bfa1-00155dd77908', N'Code1', N'test', 1);
GO
