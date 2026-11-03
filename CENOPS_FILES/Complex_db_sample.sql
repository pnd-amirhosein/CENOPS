/*
 CENOPS Database Inspector — GraphLab (SQL Server 2019+)
 ------------------------------------------------------
 A dedicated TEST database. No production schema is modified.
 Run from SSMS with CREATE DATABASE rights. Then add an *external*
 named SQL Server connection in CENOPS configuration pointing to
 CENOPS_GraphLab. Do NOT replace or use the CENOPS Default connection.

 Contains 34 tables (multiple schemas), composite FK, self-reference,
 multiple FKs to the same parent, intersecting dependency chains,
 declared cycles, unique/computed/nullable columns, mixed indexes,
 isolation (no FK) and FK references between schemas.

 Idempotence: the script intentionally refuses to overwrite existing
 lab tables, avoiding destructive recreation. Drop the test DB yourself
 when finished: USE master; DROP DATABASE CENOPS_GraphLab;
*/
USE [master];
GO
IF DB_ID(N'CENOPS_GraphLab') IS NULL
    CREATE DATABASE [CENOPS_GraphLab];
GO
USE [CENOPS_GraphLab];
GO
IF OBJECT_ID(N'core.Tenant',N'U') IS NOT NULL
    THROW 51020, 'GraphLab tables already exist. Create a fresh lab DB or inspect the existing one.', 1;
GO
CREATE SCHEMA [core];
GO
CREATE SCHEMA [hr];
GO
CREATE SCHEMA [sales];
GO
CREATE SCHEMA [inventory];
GO
CREATE SCHEMA [logistics];
GO
CREATE SCHEMA [audit];
GO

-- ROOTS & TENANCY
CREATE TABLE core.Tenant (
 TenantId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
 TenantCode VARCHAR(24) NOT NULL UNIQUE,
 DisplayName NVARCHAR(160) NOT NULL,
 IsEnabled BIT NOT NULL CONSTRAINT DF_Tenant_Enabled DEFAULT(1),
 CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Tenant_Created DEFAULT SYSUTCDATETIME()
);
GO
CREATE TABLE core.Region (
 RegionId INT IDENTITY(1,1) PRIMARY KEY,
 RegionName NVARCHAR(80) NOT NULL UNIQUE,
 ParentRegionId INT NULL,
 CONSTRAINT FK_Region_Parent FOREIGN KEY(ParentRegionId) REFERENCES core.Region(RegionId)
);
GO
CREATE TABLE core.Currency (
 CurrencyCode CHAR(3) NOT NULL PRIMARY KEY,
 Name NVARCHAR(60) NOT NULL,
 NumericCode CHAR(3) NULL
);
GO
CREATE TABLE core.ExchangeRate (
 CurrencyCode CHAR(3) NOT NULL,
 EffectiveDate DATE NOT NULL,
 Rate DECIMAL(19,6) NOT NULL,
 CONSTRAINT PK_ExchangeRate PRIMARY KEY(CurrencyCode, EffectiveDate),
 CONSTRAINT FK_ExchangeRate_Currency FOREIGN KEY(CurrencyCode) REFERENCES core.Currency(CurrencyCode),
 CONSTRAINT CK_ExchangeRate_Positive CHECK(Rate > 0)
);
GO
CREATE TABLE core.Address (
 AddressId BIGINT IDENTITY(1,1) PRIMARY KEY,
 RegionId INT NOT NULL,
 PostalCode VARCHAR(20) NULL,
 AddressLine NVARCHAR(240) NOT NULL,
 CONSTRAINT FK_Address_Region FOREIGN KEY(RegionId) REFERENCES core.Region(RegionId)
);
GO
CREATE TABLE core.Tag (
 TagId INT IDENTITY(1,1) PRIMARY KEY,
 TenantId INT NOT NULL,
 TagName NVARCHAR(80) NOT NULL,
 CONSTRAINT UQ_Tag_TenantName UNIQUE(TenantId,TagName),
 CONSTRAINT FK_Tag_Tenant FOREIGN KEY(TenantId) REFERENCES core.Tenant(TenantId)
);
GO

-- HR: TWO independent references to the same parent, plus a circular link.
CREATE TABLE hr.Department (
 DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
 TenantId INT NOT NULL,
 DepartmentName NVARCHAR(100) NOT NULL,
 ManagerEmployeeId INT NULL,
 CONSTRAINT FK_Department_Tenant FOREIGN KEY(TenantId) REFERENCES core.Tenant(TenantId)
);