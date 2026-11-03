-- Run ONLY against an expendable TEST database selected explicitly in SSMS.
-- Creates test-only tables inside a dedicated 'di_test' schema. Never run in production.
IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 50110, 'Database Inspector fixture requires a dedicated non-system TEST database.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'di_test')
    EXEC(N'CREATE SCHEMA di_test');
GO
CREATE TABLE di_test.Customers (Id int NOT NULL PRIMARY KEY, Name nvarchar(80) NOT NULL);
CREATE TABLE di_test.Orders (
    Id int NOT NULL PRIMARY KEY,
    CustomerId int NOT NULL,
    CONSTRAINT FK_DI_Orders_Customers FOREIGN KEY(CustomerId) REFERENCES di_test.Customers(Id));
CREATE TABLE di_test.Products (Id int NOT NULL PRIMARY KEY, Name nvarchar(80) NOT NULL);
CREATE TABLE di_test.OrderItems (
    OrderId int NOT NULL, ProductId int NOT NULL,
    Quantity int NOT NULL DEFAULT(1),
    CONSTRAINT PK_DI_OrderItems PRIMARY KEY(OrderId, ProductId),
    CONSTRAINT FK_DI_OrderItems_Orders FOREIGN KEY(OrderId) REFERENCES di_test.Orders(Id),
    CONSTRAINT FK_DI_OrderItems_Products FOREIGN KEY(ProductId) REFERENCES di_test.Products(Id));
CREATE TABLE di_test.CompositeParent (
    A int NOT NULL, B int NOT NULL,
    CONSTRAINT PK_DI_CompositeParent PRIMARY KEY(A,B));
CREATE TABLE di_test.CompositeChild (
    Id int NOT NULL PRIMARY KEY, A int NOT NULL, B int NOT NULL,
    CONSTRAINT FK_DI_Composite FOREIGN KEY(A,B) REFERENCES di_test.CompositeParent(A,B));
CREATE TABLE di_test.SelfLink (
    Id int NOT NULL PRIMARY KEY, ParentId int NULL,
    CONSTRAINT FK_DI_SelfLink FOREIGN KEY(ParentId) REFERENCES di_test.SelfLink(Id));
GO
-- Expected: Customers PK -> Orders; Orders PK -> OrderItems;
-- OrderItems FK -> checkbox chooser (Orders and Products);
-- CompositeChild FK -> auto-expand CompositeParent (one COMPOSITE FK constraint);
-- SelfLink PK/FK -> one vertex with loop, never infinite recursion.
