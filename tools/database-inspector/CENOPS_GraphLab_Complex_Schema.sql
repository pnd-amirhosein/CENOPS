/*
  Glasswork 2.49 — CENOPS GraphLab: DEEP TRADE / CUSTOMS / RELEASE GRAPH
  SQL Server 2019+ · schema only · deliberately challenging relationships.

  43 tables / 8 schemas with 20+ hop chain, multi-parent fan-out,
  repeated references to the same parent, composite FKs, self references,
  cross-schema dependencies and two deliberate cycles.

  Separate from the earlier CENOPS_GraphLab and from CENOPS Default.
  Safe to run once in SSMS with CREATE DATABASE / DDL permissions.

  PATH TO TEST (press PK repeatedly for children to the right):
  core.Tenant -> party.Company -> trade.PurchaseOrder -> trade.OrderLine
  -> trade.ShipmentBooking -> shipping.Shipment -> shipping.Container
  -> shipping.ContainerLine -> customs.Declaration -> customs.DeclarationLine
  -> customs.Inspection -> customs.InspectionFinding -> customs.CorrectionCase
  -> customs.CorrectionTask -> finance.DutyAssessment
  -> finance.PaymentInstruction -> finance.PaymentTransaction
  -> finance.BankReceipt -> release.ReleasePermit -> release.GatePass
  -> release.GateEvent -> audit.EventJournal -> audit.ArchiveItem

  FK explores this same chain in reverse, on the LEFT.
  Re-run: aborts if tables exist; it never drops or overwrites them.
*/
USE [master];
GO
IF DB_ID(N'CENOPS_GraphLab_Deep') IS NULL CREATE DATABASE [CENOPS_GraphLab_Deep];
GO
USE [CENOPS_GraphLab_Deep];
GO
IF OBJECT_ID(N'core.Tenant', N'U') IS NOT NULL
    THROW 51020, 'GraphLab already exists: nothing modified. Use a fresh lab database.', 1;
GO
IF SCHEMA_ID(N'audit') IS NULL EXEC(N'CREATE SCHEMA [audit]');
GO
IF SCHEMA_ID(N'core') IS NULL EXEC(N'CREATE SCHEMA [core]');
GO
IF SCHEMA_ID(N'customs') IS NULL EXEC(N'CREATE SCHEMA [customs]');
GO
IF SCHEMA_ID(N'finance') IS NULL EXEC(N'CREATE SCHEMA [finance]');
GO
IF SCHEMA_ID(N'party') IS NULL EXEC(N'CREATE SCHEMA [party]');
GO
IF SCHEMA_ID(N'release') IS NULL EXEC(N'CREATE SCHEMA [release]');
GO
IF SCHEMA_ID(N'shipping') IS NULL EXEC(N'CREATE SCHEMA [shipping]');
GO
IF SCHEMA_ID(N'trade') IS NULL EXEC(N'CREATE SCHEMA [trade]');
GO

-- Tables first, declared FK constraints later to support deliberate cycles.

CREATE TABLE [core].[Tenant] (
    [TenantId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Tenant] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Tenant_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Tenant_Created] DEFAULT(SYSUTCDATETIME())
);
GO
CREATE TABLE [core].[Region] (
    [RegionId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Region] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Region_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Region_Created] DEFAULT(SYSUTCDATETIME()),
    [ParentRegionId] INT NULL
);
GO
CREATE TABLE [core].[Address] (
    [AddressId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Address] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Address_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Address_Created] DEFAULT(SYSUTCDATETIME()),
    [RegionId] INT NULL,
    [BillingRegionId] INT NULL
);
GO
CREATE TABLE [core].[Currency] (
    [CurrencyId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Currency] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Currency_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Currency_Created] DEFAULT(SYSUTCDATETIME())
);
GO
CREATE TABLE [core].[ExchangeRate] (
    [ExchangeRateId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ExchangeRate] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_ExchangeRate_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ExchangeRate_Created] DEFAULT(SYSUTCDATETIME()),
    [CurrencyId] INT NULL,
    [TenantId] INT NULL
);
GO
CREATE TABLE [core].[Department] (
    [DepartmentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Department] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Department_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Department_Created] DEFAULT(SYSUTCDATETIME()),
    [TenantId] INT NULL,
    [ParentDepartmentId] INT NULL,
    [ManagerCompanyId] INT NULL
);
GO
CREATE TABLE [party].[Company] (
    [CompanyId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Company] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Company_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Company_Created] DEFAULT(SYSUTCDATETIME()),
    [TenantId] INT NULL,
    [RegionId] INT NULL,
    [HeadOfficeAddressId] INT NULL,
    [ParentCompanyId] INT NULL
);
GO
CREATE TABLE [party].[Contact] (
    [ContactId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Contact] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Contact_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Contact_Created] DEFAULT(SYSUTCDATETIME()),
    [CompanyId] INT NULL,
    [DepartmentId] INT NULL,
    [AddressId] INT NULL
);
GO
CREATE TABLE [party].[License] (
    [LicenseId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_License] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_License_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_License_Created] DEFAULT(SYSUTCDATETIME()),
    [CompanyId] INT NULL,
    [IssuedByCompanyId] INT NULL,
    [CurrencyId] INT NULL
);
GO
CREATE TABLE [party].[BankAccount] (
    [BankAccountId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BankAccount] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_BankAccount_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_BankAccount_Created] DEFAULT(SYSUTCDATETIME()),
    [CompanyId] INT NULL,
    [CurrencyId] INT NULL,
    [AddressId] INT NULL
);
GO
CREATE TABLE [party].[Agent] (
    [AgentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Agent] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Agent_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Agent_Created] DEFAULT(SYSUTCDATETIME()),
    [CompanyId] INT NULL,
    [ContactId] INT NULL,
    [LicenseId] INT NULL
);
GO
CREATE TABLE [party].[CompanyBranch] (
    [CompanyBranchId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_CompanyBranch] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_CompanyBranch_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_CompanyBranch_Created] DEFAULT(SYSUTCDATETIME()),
    [CompanyId] INT NULL,
    [AddressId] INT NULL,
    [ManagerContactId] INT NULL
);
GO
CREATE TABLE [trade].[Product] (
    [ProductId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Product] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Product_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Product_Created] DEFAULT(SYSUTCDATETIME()),
    [TenantId] INT NULL,
    [OwnerCompanyId] INT NULL,
    [DefaultCurrencyId] INT NULL
);
GO
CREATE TABLE [trade].[PurchaseOrder] (
    [PurchaseOrderId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PurchaseOrder] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_PurchaseOrder_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_PurchaseOrder_Created] DEFAULT(SYSUTCDATETIME()),
    [BuyerCompanyId] INT NULL,
    [SellerCompanyId] INT NULL,
    [CurrencyId] INT NULL,
    [TenantId] INT NULL,
    OrderNumber INT NOT NULL
);
GO
CREATE TABLE [trade].[OrderLine] (
    [OrderLineId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_OrderLine] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_OrderLine_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_OrderLine_Created] DEFAULT(SYSUTCDATETIME()),
    [PurchaseOrderId] INT NULL,
    [ProductId] INT NULL,
    [OriginCountryId] INT NULL,
    OrderNumber INT NULL,
    OrderTenantId INT NULL
);
GO
CREATE TABLE [trade].[ImportPermit] (
    [ImportPermitId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ImportPermit] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_ImportPermit_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ImportPermit_Created] DEFAULT(SYSUTCDATETIME()),
    [PurchaseOrderId] INT NULL,
    [ImportingCompanyId] INT NULL,
    [IssuingLicenseId] INT NULL,
    [PrimaryDocumentId] INT NULL
);
GO
CREATE TABLE [trade].[ComplianceDocument] (
    [ComplianceDocumentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ComplianceDocument] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_ComplianceDocument_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ComplianceDocument_Created] DEFAULT(SYSUTCDATETIME()),
    [ImportPermitId] INT NULL,
    [ProductId] INT NULL,
    [IssuedByAgentId] INT NULL,
    [ParentDocumentId] INT NULL
);
GO
CREATE TABLE [trade].[ShipmentBooking] (
    [ShipmentBookingId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ShipmentBooking] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_ShipmentBooking_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ShipmentBooking_Created] DEFAULT(SYSUTCDATETIME()),
    [OrderLineId] INT NULL,
    [ImportPermitId] INT NULL,
    [InsurancePolicyId] INT NULL,
    [CarrierId] INT NULL
);
GO
CREATE TABLE [trade].[InsurancePolicy] (
    [InsurancePolicyId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_InsurancePolicy] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_InsurancePolicy_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_InsurancePolicy_Created] DEFAULT(SYSUTCDATETIME()),
    [BuyerCompanyId] INT NULL,
    [InsurerCompanyId] INT NULL,
    [PurchaseOrderId] INT NULL,
    [CurrencyId] INT NULL
);
GO
CREATE TABLE [shipping].[Carrier] (
    [CarrierId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Carrier] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Carrier_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Carrier_Created] DEFAULT(SYSUTCDATETIME()),
    [CompanyId] INT NULL,
    [LicenseId] INT NULL
);
GO
CREATE TABLE [shipping].[Vessel] (
    [VesselId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Vessel] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Vessel_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Vessel_Created] DEFAULT(SYSUTCDATETIME()),
    [CarrierId] INT NULL,
    [FlagRegionId] INT NULL
);
GO
CREATE TABLE [shipping].[Voyage] (
    [VoyageId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Voyage] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Voyage_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Voyage_Created] DEFAULT(SYSUTCDATETIME()),
    [VesselId] INT NULL,
    [OriginRegionId] INT NULL,
    [DestinationRegionId] INT NULL
);
GO
CREATE TABLE [shipping].[Shipment] (
    [ShipmentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Shipment] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Shipment_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Shipment_Created] DEFAULT(SYSUTCDATETIME()),
    [BookingId] INT NULL,
    [VoyageId] INT NULL,
    [PickupCompanyId] INT NULL,
    [DeliveryCompanyId] INT NULL,
    [NextAppointmentId] INT NULL
);
GO
CREATE TABLE [shipping].[Container] (
    [ContainerId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Container] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Container_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Container_Created] DEFAULT(SYSUTCDATETIME()),
    [ShipmentId] INT NULL,
    [CurrentAddressId] INT NULL,
    [ParentContainerId] INT NULL,
    ContainerCode NVARCHAR(24) NOT NULL
);
GO
CREATE TABLE [shipping].[ContainerLine] (
    [ContainerLineId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ContainerLine] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_ContainerLine_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ContainerLine_Created] DEFAULT(SYSUTCDATETIME()),
    [ContainerId] INT NULL,
    [ShipmentId] INT NULL,
    [OrderLineId] INT NULL,
    [ProductId] INT NULL,
    ContainerShipmentId INT NULL,
    ContainerCode NVARCHAR(24) NULL
);
GO
CREATE TABLE [shipping].[TrackingEvent] (
    [TrackingEventId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_TrackingEvent] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_TrackingEvent_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_TrackingEvent_Created] DEFAULT(SYSUTCDATETIME()),
    [ContainerLineId] INT NULL,
    [ShipmentId] INT NULL,
    [LocationId] INT NULL,
    [ReportedByAgentId] INT NULL
);
GO
CREATE TABLE [shipping].[DeliveryAppointment] (
    [DeliveryAppointmentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DeliveryAppointment] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_DeliveryAppointment_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_DeliveryAppointment_Created] DEFAULT(SYSUTCDATETIME()),
    [ShipmentId] INT NULL,
    [AssignedAgentId] INT NULL,
    [DockAddressId] INT NULL,
    [PriorAppointmentId] INT NULL
);
GO
CREATE TABLE [customs].[Declaration] (
    [DeclarationId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Declaration] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Declaration_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Declaration_Created] DEFAULT(SYSUTCDATETIME()),
    [ContainerLineId] INT NULL,
    [ShipmentId] INT NULL,
    [ImportPermitId] INT NULL,
    [DeclarantAgentId] INT NULL,
    DeclarationNo NVARCHAR(32) NULL
);
GO
CREATE TABLE [customs].[DeclarationLine] (
    [DeclarationLineId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DeclarationLine] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_DeclarationLine_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_DeclarationLine_Created] DEFAULT(SYSUTCDATETIME()),
    [DeclarationId] INT NULL,
    [OrderLineId] INT NULL,
    [ContainerLineId] INT NULL,
    [ProductId] INT NULL
);
GO
CREATE TABLE [customs].[Inspection] (
    [InspectionId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Inspection] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Inspection_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Inspection_Created] DEFAULT(SYSUTCDATETIME()),
    [DeclarationLineId] INT NULL,
    [InspectorContactId] INT NULL,
    [FacilityAddressId] INT NULL
);
GO
CREATE TABLE [customs].[InspectionFinding] (
    [InspectionFindingId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_InspectionFinding] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_InspectionFinding_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_InspectionFinding_Created] DEFAULT(SYSUTCDATETIME()),
    [InspectionId] INT NULL,
    [ComplianceDocumentId] INT NULL,
    [AffectedProductId] INT NULL
);
GO
CREATE TABLE [customs].[CorrectionCase] (
    [CorrectionCaseId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_CorrectionCase] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_CorrectionCase_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_CorrectionCase_Created] DEFAULT(SYSUTCDATETIME()),
    [FindingId] INT NULL,
    [PreviousCaseId] INT NULL,
    [AssignedAgentId] INT NULL
);
GO
CREATE TABLE [customs].[CorrectionTask] (
    [CorrectionTaskId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_CorrectionTask] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_CorrectionTask_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_CorrectionTask_Created] DEFAULT(SYSUTCDATETIME()),
    [CorrectionCaseId] INT NULL,
    [ResponsibleContactId] INT NULL,
    [EvidenceDocumentId] INT NULL
);
GO
CREATE TABLE [customs].[Valuation] (
    [ValuationId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Valuation] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_Valuation_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_Valuation_Created] DEFAULT(SYSUTCDATETIME()),
    [DeclarationLineId] INT NULL,
    [CurrencyId] INT NULL,
    [ExchangeRateId] INT NULL,
    [ReviewInspectionId] INT NULL
);
GO
CREATE TABLE [finance].[DutyAssessment] (
    [DutyAssessmentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DutyAssessment] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_DutyAssessment_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_DutyAssessment_Created] DEFAULT(SYSUTCDATETIME()),
    [CorrectionTaskId] INT NULL,
    [ValuationId] INT NULL,
    [DeclarationId] INT NULL,
    [BilledCompanyId] INT NULL
);
GO
CREATE TABLE [finance].[PaymentInstruction] (
    [PaymentInstructionId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PaymentInstruction] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_PaymentInstruction_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_PaymentInstruction_Created] DEFAULT(SYSUTCDATETIME()),
    [AssessmentId] INT NULL,
    [PayerAccountId] INT NULL,
    [PayeeAccountId] INT NULL
);
GO
CREATE TABLE [finance].[PaymentTransaction] (
    [PaymentTransactionId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PaymentTransaction] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_PaymentTransaction_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_PaymentTransaction_Created] DEFAULT(SYSUTCDATETIME()),
    [InstructionId] INT NULL,
    [SettlementCurrencyId] INT NULL,
    [ProcessedByAgentId] INT NULL
);
GO
CREATE TABLE [finance].[BankReceipt] (
    [BankReceiptId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BankReceipt] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_BankReceipt_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_BankReceipt_Created] DEFAULT(SYSUTCDATETIME()),
    [TransactionId] INT NULL,
    [ReceivingAccountId] INT NULL,
    [VerifiedByContactId] INT NULL
);
GO
CREATE TABLE [release].[ReleasePermit] (
    [ReleasePermitId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ReleasePermit] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_ReleasePermit_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ReleasePermit_Created] DEFAULT(SYSUTCDATETIME()),
    [BankReceiptId] INT NULL,
    [DeclarationId] INT NULL,
    [InspectionId] INT NULL
);
GO
CREATE TABLE [release].[GatePass] (
    [GatePassId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_GatePass] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_GatePass_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_GatePass_Created] DEFAULT(SYSUTCDATETIME()),
    [ReleasePermitId] INT NULL,
    [VehicleCarrierId] INT NULL,
    [DriverContactId] INT NULL,
    [ShipmentId] INT NULL
);
GO
CREATE TABLE [release].[GateEvent] (
    [GateEventId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_GateEvent] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_GateEvent_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_GateEvent_Created] DEFAULT(SYSUTCDATETIME()),
    [GatePassId] INT NULL,
    [DepartureAppointmentId] INT NULL,
    [ExitAddressId] INT NULL
);
GO
CREATE TABLE [audit].[EventJournal] (
    [EventJournalId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_EventJournal] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_EventJournal_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_EventJournal_Created] DEFAULT(SYSUTCDATETIME()),
    [GateEventId] INT NULL,
    [ShipmentId] INT NULL,
    [PreviousEventId] INT NULL,
    [ActorContactId] INT NULL
);
GO
CREATE TABLE [audit].[ArchiveItem] (
    [ArchiveItemId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ArchiveItem] PRIMARY KEY,
    [ReferenceCode] NVARCHAR(48) NULL,
    [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_ArchiveItem_Status] DEFAULT(N'Draft'),
    [CreatedUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ArchiveItem_Created] DEFAULT(SYSUTCDATETIME()),
    [EventJournalId] INT NULL,
    [SourceDocumentId] INT NULL,
    [CompanyId] INT NULL
);
GO

-- Real SQL Server foreign keys (metadata used by Database Inspector).

ALTER TABLE [core].[Region] WITH CHECK ADD CONSTRAINT [FK_Region_ParentRegionId_Region]
    FOREIGN KEY ([ParentRegionId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_Region_ParentRegionId] ON [core].[Region] ([ParentRegionId]);
GO
ALTER TABLE [core].[Address] WITH CHECK ADD CONSTRAINT [FK_Address_RegionId_Region]
    FOREIGN KEY ([RegionId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_Address_RegionId] ON [core].[Address] ([RegionId]);
GO
ALTER TABLE [core].[Address] WITH CHECK ADD CONSTRAINT [FK_Address_BillingRegionId_Region]
    FOREIGN KEY ([BillingRegionId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_Address_BillingRegionId] ON [core].[Address] ([BillingRegionId]);
GO
ALTER TABLE [core].[ExchangeRate] WITH CHECK ADD CONSTRAINT [FK_ExchangeRate_CurrencyId_Currency]
    FOREIGN KEY ([CurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_ExchangeRate_CurrencyId] ON [core].[ExchangeRate] ([CurrencyId]);
GO
ALTER TABLE [core].[ExchangeRate] WITH CHECK ADD CONSTRAINT [FK_ExchangeRate_TenantId_Tenant]
    FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant] ([TenantId]);
GO
CREATE INDEX [IX_ExchangeRate_TenantId] ON [core].[ExchangeRate] ([TenantId]);
GO
ALTER TABLE [core].[Department] WITH CHECK ADD CONSTRAINT [FK_Department_TenantId_Tenant]
    FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant] ([TenantId]);
GO
CREATE INDEX [IX_Department_TenantId] ON [core].[Department] ([TenantId]);
GO
ALTER TABLE [core].[Department] WITH CHECK ADD CONSTRAINT [FK_Department_ParentDepartmentId_Department]
    FOREIGN KEY ([ParentDepartmentId]) REFERENCES [core].[Department] ([DepartmentId]);
GO
CREATE INDEX [IX_Department_ParentDepartmentId] ON [core].[Department] ([ParentDepartmentId]);
GO
ALTER TABLE [core].[Department] WITH CHECK ADD CONSTRAINT [FK_Department_ManagerCompanyId_Company]
    FOREIGN KEY ([ManagerCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Department_ManagerCompanyId] ON [core].[Department] ([ManagerCompanyId]);
GO
ALTER TABLE [party].[Company] WITH CHECK ADD CONSTRAINT [FK_Company_TenantId_Tenant]
    FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant] ([TenantId]);
GO
CREATE INDEX [IX_Company_TenantId] ON [party].[Company] ([TenantId]);
GO
ALTER TABLE [party].[Company] WITH CHECK ADD CONSTRAINT [FK_Company_RegionId_Region]
    FOREIGN KEY ([RegionId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_Company_RegionId] ON [party].[Company] ([RegionId]);
GO
ALTER TABLE [party].[Company] WITH CHECK ADD CONSTRAINT [FK_Company_HeadOfficeAddressId_Address]
    FOREIGN KEY ([HeadOfficeAddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_Company_HeadOfficeAddressId] ON [party].[Company] ([HeadOfficeAddressId]);
GO
ALTER TABLE [party].[Company] WITH CHECK ADD CONSTRAINT [FK_Company_ParentCompanyId_Company]
    FOREIGN KEY ([ParentCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Company_ParentCompanyId] ON [party].[Company] ([ParentCompanyId]);
GO
ALTER TABLE [party].[Contact] WITH CHECK ADD CONSTRAINT [FK_Contact_CompanyId_Company]
    FOREIGN KEY ([CompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Contact_CompanyId] ON [party].[Contact] ([CompanyId]);
GO
ALTER TABLE [party].[Contact] WITH CHECK ADD CONSTRAINT [FK_Contact_DepartmentId_Department]
    FOREIGN KEY ([DepartmentId]) REFERENCES [core].[Department] ([DepartmentId]);
GO
CREATE INDEX [IX_Contact_DepartmentId] ON [party].[Contact] ([DepartmentId]);
GO
ALTER TABLE [party].[Contact] WITH CHECK ADD CONSTRAINT [FK_Contact_AddressId_Address]
    FOREIGN KEY ([AddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_Contact_AddressId] ON [party].[Contact] ([AddressId]);
GO
ALTER TABLE [party].[License] WITH CHECK ADD CONSTRAINT [FK_License_CompanyId_Company]
    FOREIGN KEY ([CompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_License_CompanyId] ON [party].[License] ([CompanyId]);
GO
ALTER TABLE [party].[License] WITH CHECK ADD CONSTRAINT [FK_License_IssuedByCompanyId_Company]
    FOREIGN KEY ([IssuedByCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_License_IssuedByCompanyId] ON [party].[License] ([IssuedByCompanyId]);
GO
ALTER TABLE [party].[License] WITH CHECK ADD CONSTRAINT [FK_License_CurrencyId_Currency]
    FOREIGN KEY ([CurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_License_CurrencyId] ON [party].[License] ([CurrencyId]);
GO
ALTER TABLE [party].[BankAccount] WITH CHECK ADD CONSTRAINT [FK_BankAccount_CompanyId_Company]
    FOREIGN KEY ([CompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_BankAccount_CompanyId] ON [party].[BankAccount] ([CompanyId]);
GO
ALTER TABLE [party].[BankAccount] WITH CHECK ADD CONSTRAINT [FK_BankAccount_CurrencyId_Currency]
    FOREIGN KEY ([CurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_BankAccount_CurrencyId] ON [party].[BankAccount] ([CurrencyId]);
GO
ALTER TABLE [party].[BankAccount] WITH CHECK ADD CONSTRAINT [FK_BankAccount_AddressId_Address]
    FOREIGN KEY ([AddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_BankAccount_AddressId] ON [party].[BankAccount] ([AddressId]);
GO
ALTER TABLE [party].[Agent] WITH CHECK ADD CONSTRAINT [FK_Agent_CompanyId_Company]
    FOREIGN KEY ([CompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Agent_CompanyId] ON [party].[Agent] ([CompanyId]);
GO
ALTER TABLE [party].[Agent] WITH CHECK ADD CONSTRAINT [FK_Agent_ContactId_Contact]
    FOREIGN KEY ([ContactId]) REFERENCES [party].[Contact] ([ContactId]);
GO
CREATE INDEX [IX_Agent_ContactId] ON [party].[Agent] ([ContactId]);
GO
ALTER TABLE [party].[Agent] WITH CHECK ADD CONSTRAINT [FK_Agent_LicenseId_License]
    FOREIGN KEY ([LicenseId]) REFERENCES [party].[License] ([LicenseId]);
GO
CREATE INDEX [IX_Agent_LicenseId] ON [party].[Agent] ([LicenseId]);
GO
ALTER TABLE [party].[CompanyBranch] WITH CHECK ADD CONSTRAINT [FK_CompanyBranch_CompanyId_Company]
    FOREIGN KEY ([CompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_CompanyBranch_CompanyId] ON [party].[CompanyBranch] ([CompanyId]);
GO
ALTER TABLE [party].[CompanyBranch] WITH CHECK ADD CONSTRAINT [FK_CompanyBranch_AddressId_Address]
    FOREIGN KEY ([AddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_CompanyBranch_AddressId] ON [party].[CompanyBranch] ([AddressId]);
GO
ALTER TABLE [party].[CompanyBranch] WITH CHECK ADD CONSTRAINT [FK_CompanyBranch_ManagerContactId_Contact]
    FOREIGN KEY ([ManagerContactId]) REFERENCES [party].[Contact] ([ContactId]);
GO
CREATE INDEX [IX_CompanyBranch_ManagerContactId] ON [party].[CompanyBranch] ([ManagerContactId]);
GO
ALTER TABLE [trade].[Product] WITH CHECK ADD CONSTRAINT [FK_Product_TenantId_Tenant]
    FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant] ([TenantId]);
GO
CREATE INDEX [IX_Product_TenantId] ON [trade].[Product] ([TenantId]);
GO
ALTER TABLE [trade].[Product] WITH CHECK ADD CONSTRAINT [FK_Product_OwnerCompanyId_Company]
    FOREIGN KEY ([OwnerCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Product_OwnerCompanyId] ON [trade].[Product] ([OwnerCompanyId]);
GO
ALTER TABLE [trade].[Product] WITH CHECK ADD CONSTRAINT [FK_Product_DefaultCurrencyId_Currency]
    FOREIGN KEY ([DefaultCurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_Product_DefaultCurrencyId] ON [trade].[Product] ([DefaultCurrencyId]);
GO
ALTER TABLE [trade].[PurchaseOrder] WITH CHECK ADD CONSTRAINT [FK_PurchaseOrder_BuyerCompanyId_Company]
    FOREIGN KEY ([BuyerCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_PurchaseOrder_BuyerCompanyId] ON [trade].[PurchaseOrder] ([BuyerCompanyId]);
GO
ALTER TABLE [trade].[PurchaseOrder] WITH CHECK ADD CONSTRAINT [FK_PurchaseOrder_SellerCompanyId_Company]
    FOREIGN KEY ([SellerCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_PurchaseOrder_SellerCompanyId] ON [trade].[PurchaseOrder] ([SellerCompanyId]);
GO
ALTER TABLE [trade].[PurchaseOrder] WITH CHECK ADD CONSTRAINT [FK_PurchaseOrder_CurrencyId_Currency]
    FOREIGN KEY ([CurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_PurchaseOrder_CurrencyId] ON [trade].[PurchaseOrder] ([CurrencyId]);
GO
ALTER TABLE [trade].[PurchaseOrder] WITH CHECK ADD CONSTRAINT [FK_PurchaseOrder_TenantId_Tenant]
    FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant] ([TenantId]);
GO
CREATE INDEX [IX_PurchaseOrder_TenantId] ON [trade].[PurchaseOrder] ([TenantId]);
GO
ALTER TABLE [trade].[OrderLine] WITH CHECK ADD CONSTRAINT [FK_OrderLine_PurchaseOrderId_PurchaseOrder]
    FOREIGN KEY ([PurchaseOrderId]) REFERENCES [trade].[PurchaseOrder] ([PurchaseOrderId]);
GO
CREATE INDEX [IX_OrderLine_PurchaseOrderId] ON [trade].[OrderLine] ([PurchaseOrderId]);
GO
ALTER TABLE [trade].[OrderLine] WITH CHECK ADD CONSTRAINT [FK_OrderLine_ProductId_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [trade].[Product] ([ProductId]);
GO
CREATE INDEX [IX_OrderLine_ProductId] ON [trade].[OrderLine] ([ProductId]);
GO
ALTER TABLE [trade].[OrderLine] WITH CHECK ADD CONSTRAINT [FK_OrderLine_OriginCountryId_Region]
    FOREIGN KEY ([OriginCountryId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_OrderLine_OriginCountryId] ON [trade].[OrderLine] ([OriginCountryId]);
GO
ALTER TABLE [trade].[ImportPermit] WITH CHECK ADD CONSTRAINT [FK_ImportPermit_PurchaseOrderId_PurchaseOrder]
    FOREIGN KEY ([PurchaseOrderId]) REFERENCES [trade].[PurchaseOrder] ([PurchaseOrderId]);
GO
CREATE INDEX [IX_ImportPermit_PurchaseOrderId] ON [trade].[ImportPermit] ([PurchaseOrderId]);
GO
ALTER TABLE [trade].[ImportPermit] WITH CHECK ADD CONSTRAINT [FK_ImportPermit_ImportingCompanyId_Company]
    FOREIGN KEY ([ImportingCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_ImportPermit_ImportingCompanyId] ON [trade].[ImportPermit] ([ImportingCompanyId]);
GO
ALTER TABLE [trade].[ImportPermit] WITH CHECK ADD CONSTRAINT [FK_ImportPermit_IssuingLicenseId_License]
    FOREIGN KEY ([IssuingLicenseId]) REFERENCES [party].[License] ([LicenseId]);
GO
CREATE INDEX [IX_ImportPermit_IssuingLicenseId] ON [trade].[ImportPermit] ([IssuingLicenseId]);
GO
ALTER TABLE [trade].[ImportPermit] WITH CHECK ADD CONSTRAINT [FK_ImportPermit_PrimaryDocumentId_ComplianceDocument]
    FOREIGN KEY ([PrimaryDocumentId]) REFERENCES [trade].[ComplianceDocument] ([ComplianceDocumentId]);
GO
CREATE INDEX [IX_ImportPermit_PrimaryDocumentId] ON [trade].[ImportPermit] ([PrimaryDocumentId]);
GO
ALTER TABLE [trade].[ComplianceDocument] WITH CHECK ADD CONSTRAINT [FK_ComplianceDocument_ImportPermitId_ImportPermit]
    FOREIGN KEY ([ImportPermitId]) REFERENCES [trade].[ImportPermit] ([ImportPermitId]);
GO
CREATE INDEX [IX_ComplianceDocument_ImportPermitId] ON [trade].[ComplianceDocument] ([ImportPermitId]);
GO
ALTER TABLE [trade].[ComplianceDocument] WITH CHECK ADD CONSTRAINT [FK_ComplianceDocument_ProductId_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [trade].[Product] ([ProductId]);
GO
CREATE INDEX [IX_ComplianceDocument_ProductId] ON [trade].[ComplianceDocument] ([ProductId]);
GO
ALTER TABLE [trade].[ComplianceDocument] WITH CHECK ADD CONSTRAINT [FK_ComplianceDocument_IssuedByAgentId_Agent]
    FOREIGN KEY ([IssuedByAgentId]) REFERENCES [party].[Agent] ([AgentId]);
GO
CREATE INDEX [IX_ComplianceDocument_IssuedByAgentId] ON [trade].[ComplianceDocument] ([IssuedByAgentId]);
GO
ALTER TABLE [trade].[ComplianceDocument] WITH CHECK ADD CONSTRAINT [FK_ComplianceDocument_ParentDocumentId_ComplianceDocument]
    FOREIGN KEY ([ParentDocumentId]) REFERENCES [trade].[ComplianceDocument] ([ComplianceDocumentId]);
GO
CREATE INDEX [IX_ComplianceDocument_ParentDocumentId] ON [trade].[ComplianceDocument] ([ParentDocumentId]);
GO
ALTER TABLE [trade].[ShipmentBooking] WITH CHECK ADD CONSTRAINT [FK_ShipmentBooking_OrderLineId_OrderLine]
    FOREIGN KEY ([OrderLineId]) REFERENCES [trade].[OrderLine] ([OrderLineId]);
GO
CREATE INDEX [IX_ShipmentBooking_OrderLineId] ON [trade].[ShipmentBooking] ([OrderLineId]);
GO
ALTER TABLE [trade].[ShipmentBooking] WITH CHECK ADD CONSTRAINT [FK_ShipmentBooking_ImportPermitId_ImportPermit]
    FOREIGN KEY ([ImportPermitId]) REFERENCES [trade].[ImportPermit] ([ImportPermitId]);
GO
CREATE INDEX [IX_ShipmentBooking_ImportPermitId] ON [trade].[ShipmentBooking] ([ImportPermitId]);
GO
ALTER TABLE [trade].[ShipmentBooking] WITH CHECK ADD CONSTRAINT [FK_ShipmentBooking_InsurancePolicyId_InsurancePolicy]
    FOREIGN KEY ([InsurancePolicyId]) REFERENCES [trade].[InsurancePolicy] ([InsurancePolicyId]);
GO
CREATE INDEX [IX_ShipmentBooking_InsurancePolicyId] ON [trade].[ShipmentBooking] ([InsurancePolicyId]);
GO
ALTER TABLE [trade].[ShipmentBooking] WITH CHECK ADD CONSTRAINT [FK_ShipmentBooking_CarrierId_Carrier]
    FOREIGN KEY ([CarrierId]) REFERENCES [shipping].[Carrier] ([CarrierId]);
GO
CREATE INDEX [IX_ShipmentBooking_CarrierId] ON [trade].[ShipmentBooking] ([CarrierId]);
GO
ALTER TABLE [trade].[InsurancePolicy] WITH CHECK ADD CONSTRAINT [FK_InsurancePolicy_BuyerCompanyId_Company]
    FOREIGN KEY ([BuyerCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_InsurancePolicy_BuyerCompanyId] ON [trade].[InsurancePolicy] ([BuyerCompanyId]);
GO
ALTER TABLE [trade].[InsurancePolicy] WITH CHECK ADD CONSTRAINT [FK_InsurancePolicy_InsurerCompanyId_Company]
    FOREIGN KEY ([InsurerCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_InsurancePolicy_InsurerCompanyId] ON [trade].[InsurancePolicy] ([InsurerCompanyId]);
GO
ALTER TABLE [trade].[InsurancePolicy] WITH CHECK ADD CONSTRAINT [FK_InsurancePolicy_PurchaseOrderId_PurchaseOrder]
    FOREIGN KEY ([PurchaseOrderId]) REFERENCES [trade].[PurchaseOrder] ([PurchaseOrderId]);
GO
CREATE INDEX [IX_InsurancePolicy_PurchaseOrderId] ON [trade].[InsurancePolicy] ([PurchaseOrderId]);
GO
ALTER TABLE [trade].[InsurancePolicy] WITH CHECK ADD CONSTRAINT [FK_InsurancePolicy_CurrencyId_Currency]
    FOREIGN KEY ([CurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_InsurancePolicy_CurrencyId] ON [trade].[InsurancePolicy] ([CurrencyId]);
GO
ALTER TABLE [shipping].[Carrier] WITH CHECK ADD CONSTRAINT [FK_Carrier_CompanyId_Company]
    FOREIGN KEY ([CompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Carrier_CompanyId] ON [shipping].[Carrier] ([CompanyId]);
GO
ALTER TABLE [shipping].[Carrier] WITH CHECK ADD CONSTRAINT [FK_Carrier_LicenseId_License]
    FOREIGN KEY ([LicenseId]) REFERENCES [party].[License] ([LicenseId]);
GO
CREATE INDEX [IX_Carrier_LicenseId] ON [shipping].[Carrier] ([LicenseId]);
GO
ALTER TABLE [shipping].[Vessel] WITH CHECK ADD CONSTRAINT [FK_Vessel_CarrierId_Carrier]
    FOREIGN KEY ([CarrierId]) REFERENCES [shipping].[Carrier] ([CarrierId]);
GO
CREATE INDEX [IX_Vessel_CarrierId] ON [shipping].[Vessel] ([CarrierId]);
GO
ALTER TABLE [shipping].[Vessel] WITH CHECK ADD CONSTRAINT [FK_Vessel_FlagRegionId_Region]
    FOREIGN KEY ([FlagRegionId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_Vessel_FlagRegionId] ON [shipping].[Vessel] ([FlagRegionId]);
GO
ALTER TABLE [shipping].[Voyage] WITH CHECK ADD CONSTRAINT [FK_Voyage_VesselId_Vessel]
    FOREIGN KEY ([VesselId]) REFERENCES [shipping].[Vessel] ([VesselId]);
GO
CREATE INDEX [IX_Voyage_VesselId] ON [shipping].[Voyage] ([VesselId]);
GO
ALTER TABLE [shipping].[Voyage] WITH CHECK ADD CONSTRAINT [FK_Voyage_OriginRegionId_Region]
    FOREIGN KEY ([OriginRegionId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_Voyage_OriginRegionId] ON [shipping].[Voyage] ([OriginRegionId]);
GO
ALTER TABLE [shipping].[Voyage] WITH CHECK ADD CONSTRAINT [FK_Voyage_DestinationRegionId_Region]
    FOREIGN KEY ([DestinationRegionId]) REFERENCES [core].[Region] ([RegionId]);
GO
CREATE INDEX [IX_Voyage_DestinationRegionId] ON [shipping].[Voyage] ([DestinationRegionId]);
GO
ALTER TABLE [shipping].[Shipment] WITH CHECK ADD CONSTRAINT [FK_Shipment_BookingId_ShipmentBooking]
    FOREIGN KEY ([BookingId]) REFERENCES [trade].[ShipmentBooking] ([ShipmentBookingId]);
GO
CREATE INDEX [IX_Shipment_BookingId] ON [shipping].[Shipment] ([BookingId]);
GO
ALTER TABLE [shipping].[Shipment] WITH CHECK ADD CONSTRAINT [FK_Shipment_VoyageId_Voyage]
    FOREIGN KEY ([VoyageId]) REFERENCES [shipping].[Voyage] ([VoyageId]);
GO
CREATE INDEX [IX_Shipment_VoyageId] ON [shipping].[Shipment] ([VoyageId]);
GO
ALTER TABLE [shipping].[Shipment] WITH CHECK ADD CONSTRAINT [FK_Shipment_PickupCompanyId_Company]
    FOREIGN KEY ([PickupCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Shipment_PickupCompanyId] ON [shipping].[Shipment] ([PickupCompanyId]);
GO
ALTER TABLE [shipping].[Shipment] WITH CHECK ADD CONSTRAINT [FK_Shipment_DeliveryCompanyId_Company]
    FOREIGN KEY ([DeliveryCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_Shipment_DeliveryCompanyId] ON [shipping].[Shipment] ([DeliveryCompanyId]);
GO
ALTER TABLE [shipping].[Shipment] WITH CHECK ADD CONSTRAINT [FK_Shipment_NextAppointmentId_DeliveryAppointment]
    FOREIGN KEY ([NextAppointmentId]) REFERENCES [shipping].[DeliveryAppointment] ([DeliveryAppointmentId]);
GO
CREATE INDEX [IX_Shipment_NextAppointmentId] ON [shipping].[Shipment] ([NextAppointmentId]);
GO
ALTER TABLE [shipping].[Container] WITH CHECK ADD CONSTRAINT [FK_Container_ShipmentId_Shipment]
    FOREIGN KEY ([ShipmentId]) REFERENCES [shipping].[Shipment] ([ShipmentId]);
GO
CREATE INDEX [IX_Container_ShipmentId] ON [shipping].[Container] ([ShipmentId]);
GO
ALTER TABLE [shipping].[Container] WITH CHECK ADD CONSTRAINT [FK_Container_CurrentAddressId_Address]
    FOREIGN KEY ([CurrentAddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_Container_CurrentAddressId] ON [shipping].[Container] ([CurrentAddressId]);
GO
ALTER TABLE [shipping].[Container] WITH CHECK ADD CONSTRAINT [FK_Container_ParentContainerId_Container]
    FOREIGN KEY ([ParentContainerId]) REFERENCES [shipping].[Container] ([ContainerId]);
GO
CREATE INDEX [IX_Container_ParentContainerId] ON [shipping].[Container] ([ParentContainerId]);
GO
ALTER TABLE [shipping].[ContainerLine] WITH CHECK ADD CONSTRAINT [FK_ContainerLine_ContainerId_Container]
    FOREIGN KEY ([ContainerId]) REFERENCES [shipping].[Container] ([ContainerId]);
GO
CREATE INDEX [IX_ContainerLine_ContainerId] ON [shipping].[ContainerLine] ([ContainerId]);
GO
ALTER TABLE [shipping].[ContainerLine] WITH CHECK ADD CONSTRAINT [FK_ContainerLine_ShipmentId_Shipment]
    FOREIGN KEY ([ShipmentId]) REFERENCES [shipping].[Shipment] ([ShipmentId]);
GO
CREATE INDEX [IX_ContainerLine_ShipmentId] ON [shipping].[ContainerLine] ([ShipmentId]);
GO
ALTER TABLE [shipping].[ContainerLine] WITH CHECK ADD CONSTRAINT [FK_ContainerLine_OrderLineId_OrderLine]
    FOREIGN KEY ([OrderLineId]) REFERENCES [trade].[OrderLine] ([OrderLineId]);
GO
CREATE INDEX [IX_ContainerLine_OrderLineId] ON [shipping].[ContainerLine] ([OrderLineId]);
GO
ALTER TABLE [shipping].[ContainerLine] WITH CHECK ADD CONSTRAINT [FK_ContainerLine_ProductId_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [trade].[Product] ([ProductId]);
GO
CREATE INDEX [IX_ContainerLine_ProductId] ON [shipping].[ContainerLine] ([ProductId]);
GO
ALTER TABLE [shipping].[TrackingEvent] WITH CHECK ADD CONSTRAINT [FK_TrackingEvent_ContainerLineId_ContainerLine]
    FOREIGN KEY ([ContainerLineId]) REFERENCES [shipping].[ContainerLine] ([ContainerLineId]);
GO
CREATE INDEX [IX_TrackingEvent_ContainerLineId] ON [shipping].[TrackingEvent] ([ContainerLineId]);
GO
ALTER TABLE [shipping].[TrackingEvent] WITH CHECK ADD CONSTRAINT [FK_TrackingEvent_ShipmentId_Shipment]
    FOREIGN KEY ([ShipmentId]) REFERENCES [shipping].[Shipment] ([ShipmentId]);
GO
CREATE INDEX [IX_TrackingEvent_ShipmentId] ON [shipping].[TrackingEvent] ([ShipmentId]);
GO
ALTER TABLE [shipping].[TrackingEvent] WITH CHECK ADD CONSTRAINT [FK_TrackingEvent_LocationId_Address]
    FOREIGN KEY ([LocationId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_TrackingEvent_LocationId] ON [shipping].[TrackingEvent] ([LocationId]);
GO
ALTER TABLE [shipping].[TrackingEvent] WITH CHECK ADD CONSTRAINT [FK_TrackingEvent_ReportedByAgentId_Agent]
    FOREIGN KEY ([ReportedByAgentId]) REFERENCES [party].[Agent] ([AgentId]);
GO
CREATE INDEX [IX_TrackingEvent_ReportedByAgentId] ON [shipping].[TrackingEvent] ([ReportedByAgentId]);
GO
ALTER TABLE [shipping].[DeliveryAppointment] WITH CHECK ADD CONSTRAINT [FK_DeliveryAppointment_ShipmentId_Shipment]
    FOREIGN KEY ([ShipmentId]) REFERENCES [shipping].[Shipment] ([ShipmentId]);
GO
CREATE INDEX [IX_DeliveryAppointment_ShipmentId] ON [shipping].[DeliveryAppointment] ([ShipmentId]);
GO
ALTER TABLE [shipping].[DeliveryAppointment] WITH CHECK ADD CONSTRAINT [FK_DeliveryAppointment_AssignedAgentId_Agent]
    FOREIGN KEY ([AssignedAgentId]) REFERENCES [party].[Agent] ([AgentId]);
GO
CREATE INDEX [IX_DeliveryAppointment_AssignedAgentId] ON [shipping].[DeliveryAppointment] ([AssignedAgentId]);
GO
ALTER TABLE [shipping].[DeliveryAppointment] WITH CHECK ADD CONSTRAINT [FK_DeliveryAppointment_DockAddressId_Address]
    FOREIGN KEY ([DockAddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_DeliveryAppointment_DockAddressId] ON [shipping].[DeliveryAppointment] ([DockAddressId]);
GO
ALTER TABLE [shipping].[DeliveryAppointment] WITH CHECK ADD CONSTRAINT [FK_DeliveryAppointment_PriorAppointmentId_DeliveryAppointment]
    FOREIGN KEY ([PriorAppointmentId]) REFERENCES [shipping].[DeliveryAppointment] ([DeliveryAppointmentId]);
GO
CREATE INDEX [IX_DeliveryAppointment_PriorAppointmentId] ON [shipping].[DeliveryAppointment] ([PriorAppointmentId]);
GO
ALTER TABLE [customs].[Declaration] WITH CHECK ADD CONSTRAINT [FK_Declaration_ContainerLineId_ContainerLine]
    FOREIGN KEY ([ContainerLineId]) REFERENCES [shipping].[ContainerLine] ([ContainerLineId]);
GO
CREATE INDEX [IX_Declaration_ContainerLineId] ON [customs].[Declaration] ([ContainerLineId]);
GO
ALTER TABLE [customs].[Declaration] WITH CHECK ADD CONSTRAINT [FK_Declaration_ShipmentId_Shipment]
    FOREIGN KEY ([ShipmentId]) REFERENCES [shipping].[Shipment] ([ShipmentId]);
GO
CREATE INDEX [IX_Declaration_ShipmentId] ON [customs].[Declaration] ([ShipmentId]);
GO
ALTER TABLE [customs].[Declaration] WITH CHECK ADD CONSTRAINT [FK_Declaration_ImportPermitId_ImportPermit]
    FOREIGN KEY ([ImportPermitId]) REFERENCES [trade].[ImportPermit] ([ImportPermitId]);
GO
CREATE INDEX [IX_Declaration_ImportPermitId] ON [customs].[Declaration] ([ImportPermitId]);
GO
ALTER TABLE [customs].[Declaration] WITH CHECK ADD CONSTRAINT [FK_Declaration_DeclarantAgentId_Agent]
    FOREIGN KEY ([DeclarantAgentId]) REFERENCES [party].[Agent] ([AgentId]);
GO
CREATE INDEX [IX_Declaration_DeclarantAgentId] ON [customs].[Declaration] ([DeclarantAgentId]);
GO
ALTER TABLE [customs].[DeclarationLine] WITH CHECK ADD CONSTRAINT [FK_DeclarationLine_DeclarationId_Declaration]
    FOREIGN KEY ([DeclarationId]) REFERENCES [customs].[Declaration] ([DeclarationId]);
GO
CREATE INDEX [IX_DeclarationLine_DeclarationId] ON [customs].[DeclarationLine] ([DeclarationId]);
GO
ALTER TABLE [customs].[DeclarationLine] WITH CHECK ADD CONSTRAINT [FK_DeclarationLine_OrderLineId_OrderLine]
    FOREIGN KEY ([OrderLineId]) REFERENCES [trade].[OrderLine] ([OrderLineId]);
GO
CREATE INDEX [IX_DeclarationLine_OrderLineId] ON [customs].[DeclarationLine] ([OrderLineId]);
GO
ALTER TABLE [customs].[DeclarationLine] WITH CHECK ADD CONSTRAINT [FK_DeclarationLine_ContainerLineId_ContainerLine]
    FOREIGN KEY ([ContainerLineId]) REFERENCES [shipping].[ContainerLine] ([ContainerLineId]);
GO
CREATE INDEX [IX_DeclarationLine_ContainerLineId] ON [customs].[DeclarationLine] ([ContainerLineId]);
GO
ALTER TABLE [customs].[DeclarationLine] WITH CHECK ADD CONSTRAINT [FK_DeclarationLine_ProductId_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [trade].[Product] ([ProductId]);
GO
CREATE INDEX [IX_DeclarationLine_ProductId] ON [customs].[DeclarationLine] ([ProductId]);
GO
ALTER TABLE [customs].[Inspection] WITH CHECK ADD CONSTRAINT [FK_Inspection_DeclarationLineId_DeclarationLine]
    FOREIGN KEY ([DeclarationLineId]) REFERENCES [customs].[DeclarationLine] ([DeclarationLineId]);
GO
CREATE INDEX [IX_Inspection_DeclarationLineId] ON [customs].[Inspection] ([DeclarationLineId]);
GO
ALTER TABLE [customs].[Inspection] WITH CHECK ADD CONSTRAINT [FK_Inspection_InspectorContactId_Contact]
    FOREIGN KEY ([InspectorContactId]) REFERENCES [party].[Contact] ([ContactId]);
GO
CREATE INDEX [IX_Inspection_InspectorContactId] ON [customs].[Inspection] ([InspectorContactId]);
GO
ALTER TABLE [customs].[Inspection] WITH CHECK ADD CONSTRAINT [FK_Inspection_FacilityAddressId_Address]
    FOREIGN KEY ([FacilityAddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_Inspection_FacilityAddressId] ON [customs].[Inspection] ([FacilityAddressId]);
GO
ALTER TABLE [customs].[InspectionFinding] WITH CHECK ADD CONSTRAINT [FK_InspectionFinding_InspectionId_Inspection]
    FOREIGN KEY ([InspectionId]) REFERENCES [customs].[Inspection] ([InspectionId]);
GO
CREATE INDEX [IX_InspectionFinding_InspectionId] ON [customs].[InspectionFinding] ([InspectionId]);
GO
ALTER TABLE [customs].[InspectionFinding] WITH CHECK ADD CONSTRAINT [FK_InspectionFinding_ComplianceDocumentId_ComplianceDocument]
    FOREIGN KEY ([ComplianceDocumentId]) REFERENCES [trade].[ComplianceDocument] ([ComplianceDocumentId]);
GO
CREATE INDEX [IX_InspectionFinding_ComplianceDocumentId] ON [customs].[InspectionFinding] ([ComplianceDocumentId]);
GO
ALTER TABLE [customs].[InspectionFinding] WITH CHECK ADD CONSTRAINT [FK_InspectionFinding_AffectedProductId_Product]
    FOREIGN KEY ([AffectedProductId]) REFERENCES [trade].[Product] ([ProductId]);
GO
CREATE INDEX [IX_InspectionFinding_AffectedProductId] ON [customs].[InspectionFinding] ([AffectedProductId]);
GO
ALTER TABLE [customs].[CorrectionCase] WITH CHECK ADD CONSTRAINT [FK_CorrectionCase_FindingId_InspectionFinding]
    FOREIGN KEY ([FindingId]) REFERENCES [customs].[InspectionFinding] ([InspectionFindingId]);
GO
CREATE INDEX [IX_CorrectionCase_FindingId] ON [customs].[CorrectionCase] ([FindingId]);
GO
ALTER TABLE [customs].[CorrectionCase] WITH CHECK ADD CONSTRAINT [FK_CorrectionCase_PreviousCaseId_CorrectionCase]
    FOREIGN KEY ([PreviousCaseId]) REFERENCES [customs].[CorrectionCase] ([CorrectionCaseId]);
GO
CREATE INDEX [IX_CorrectionCase_PreviousCaseId] ON [customs].[CorrectionCase] ([PreviousCaseId]);
GO
ALTER TABLE [customs].[CorrectionCase] WITH CHECK ADD CONSTRAINT [FK_CorrectionCase_AssignedAgentId_Agent]
    FOREIGN KEY ([AssignedAgentId]) REFERENCES [party].[Agent] ([AgentId]);
GO
CREATE INDEX [IX_CorrectionCase_AssignedAgentId] ON [customs].[CorrectionCase] ([AssignedAgentId]);
GO
ALTER TABLE [customs].[CorrectionTask] WITH CHECK ADD CONSTRAINT [FK_CorrectionTask_CorrectionCaseId_CorrectionCase]
    FOREIGN KEY ([CorrectionCaseId]) REFERENCES [customs].[CorrectionCase] ([CorrectionCaseId]);
GO
CREATE INDEX [IX_CorrectionTask_CorrectionCaseId] ON [customs].[CorrectionTask] ([CorrectionCaseId]);
GO
ALTER TABLE [customs].[CorrectionTask] WITH CHECK ADD CONSTRAINT [FK_CorrectionTask_ResponsibleContactId_Contact]
    FOREIGN KEY ([ResponsibleContactId]) REFERENCES [party].[Contact] ([ContactId]);
GO
CREATE INDEX [IX_CorrectionTask_ResponsibleContactId] ON [customs].[CorrectionTask] ([ResponsibleContactId]);
GO
ALTER TABLE [customs].[CorrectionTask] WITH CHECK ADD CONSTRAINT [FK_CorrectionTask_EvidenceDocumentId_ComplianceDocument]
    FOREIGN KEY ([EvidenceDocumentId]) REFERENCES [trade].[ComplianceDocument] ([ComplianceDocumentId]);
GO
CREATE INDEX [IX_CorrectionTask_EvidenceDocumentId] ON [customs].[CorrectionTask] ([EvidenceDocumentId]);
GO
ALTER TABLE [customs].[Valuation] WITH CHECK ADD CONSTRAINT [FK_Valuation_DeclarationLineId_DeclarationLine]
    FOREIGN KEY ([DeclarationLineId]) REFERENCES [customs].[DeclarationLine] ([DeclarationLineId]);
GO
CREATE INDEX [IX_Valuation_DeclarationLineId] ON [customs].[Valuation] ([DeclarationLineId]);
GO
ALTER TABLE [customs].[Valuation] WITH CHECK ADD CONSTRAINT [FK_Valuation_CurrencyId_Currency]
    FOREIGN KEY ([CurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_Valuation_CurrencyId] ON [customs].[Valuation] ([CurrencyId]);
GO
ALTER TABLE [customs].[Valuation] WITH CHECK ADD CONSTRAINT [FK_Valuation_ExchangeRateId_ExchangeRate]
    FOREIGN KEY ([ExchangeRateId]) REFERENCES [core].[ExchangeRate] ([ExchangeRateId]);
GO
CREATE INDEX [IX_Valuation_ExchangeRateId] ON [customs].[Valuation] ([ExchangeRateId]);
GO
ALTER TABLE [customs].[Valuation] WITH CHECK ADD CONSTRAINT [FK_Valuation_ReviewInspectionId_Inspection]
    FOREIGN KEY ([ReviewInspectionId]) REFERENCES [customs].[Inspection] ([InspectionId]);
GO
CREATE INDEX [IX_Valuation_ReviewInspectionId] ON [customs].[Valuation] ([ReviewInspectionId]);
GO
ALTER TABLE [finance].[DutyAssessment] WITH CHECK ADD CONSTRAINT [FK_DutyAssessment_CorrectionTaskId_CorrectionTask]
    FOREIGN KEY ([CorrectionTaskId]) REFERENCES [customs].[CorrectionTask] ([CorrectionTaskId]);
GO
CREATE INDEX [IX_DutyAssessment_CorrectionTaskId] ON [finance].[DutyAssessment] ([CorrectionTaskId]);
GO
ALTER TABLE [finance].[DutyAssessment] WITH CHECK ADD CONSTRAINT [FK_DutyAssessment_ValuationId_Valuation]
    FOREIGN KEY ([ValuationId]) REFERENCES [customs].[Valuation] ([ValuationId]);
GO
CREATE INDEX [IX_DutyAssessment_ValuationId] ON [finance].[DutyAssessment] ([ValuationId]);
GO
ALTER TABLE [finance].[DutyAssessment] WITH CHECK ADD CONSTRAINT [FK_DutyAssessment_DeclarationId_Declaration]
    FOREIGN KEY ([DeclarationId]) REFERENCES [customs].[Declaration] ([DeclarationId]);
GO
CREATE INDEX [IX_DutyAssessment_DeclarationId] ON [finance].[DutyAssessment] ([DeclarationId]);
GO
ALTER TABLE [finance].[DutyAssessment] WITH CHECK ADD CONSTRAINT [FK_DutyAssessment_BilledCompanyId_Company]
    FOREIGN KEY ([BilledCompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_DutyAssessment_BilledCompanyId] ON [finance].[DutyAssessment] ([BilledCompanyId]);
GO
ALTER TABLE [finance].[PaymentInstruction] WITH CHECK ADD CONSTRAINT [FK_PaymentInstruction_AssessmentId_DutyAssessment]
    FOREIGN KEY ([AssessmentId]) REFERENCES [finance].[DutyAssessment] ([DutyAssessmentId]);
GO
CREATE INDEX [IX_PaymentInstruction_AssessmentId] ON [finance].[PaymentInstruction] ([AssessmentId]);
GO
ALTER TABLE [finance].[PaymentInstruction] WITH CHECK ADD CONSTRAINT [FK_PaymentInstruction_PayerAccountId_BankAccount]
    FOREIGN KEY ([PayerAccountId]) REFERENCES [party].[BankAccount] ([BankAccountId]);
GO
CREATE INDEX [IX_PaymentInstruction_PayerAccountId] ON [finance].[PaymentInstruction] ([PayerAccountId]);
GO
ALTER TABLE [finance].[PaymentInstruction] WITH CHECK ADD CONSTRAINT [FK_PaymentInstruction_PayeeAccountId_BankAccount]
    FOREIGN KEY ([PayeeAccountId]) REFERENCES [party].[BankAccount] ([BankAccountId]);
GO
CREATE INDEX [IX_PaymentInstruction_PayeeAccountId] ON [finance].[PaymentInstruction] ([PayeeAccountId]);
GO
ALTER TABLE [finance].[PaymentTransaction] WITH CHECK ADD CONSTRAINT [FK_PaymentTransaction_InstructionId_PaymentInstruction]
    FOREIGN KEY ([InstructionId]) REFERENCES [finance].[PaymentInstruction] ([PaymentInstructionId]);
GO
CREATE INDEX [IX_PaymentTransaction_InstructionId] ON [finance].[PaymentTransaction] ([InstructionId]);
GO
ALTER TABLE [finance].[PaymentTransaction] WITH CHECK ADD CONSTRAINT [FK_PaymentTransaction_SettlementCurrencyId_Currency]
    FOREIGN KEY ([SettlementCurrencyId]) REFERENCES [core].[Currency] ([CurrencyId]);
GO
CREATE INDEX [IX_PaymentTransaction_SettlementCurrencyId] ON [finance].[PaymentTransaction] ([SettlementCurrencyId]);
GO
ALTER TABLE [finance].[PaymentTransaction] WITH CHECK ADD CONSTRAINT [FK_PaymentTransaction_ProcessedByAgentId_Agent]
    FOREIGN KEY ([ProcessedByAgentId]) REFERENCES [party].[Agent] ([AgentId]);
GO
CREATE INDEX [IX_PaymentTransaction_ProcessedByAgentId] ON [finance].[PaymentTransaction] ([ProcessedByAgentId]);
GO
ALTER TABLE [finance].[BankReceipt] WITH CHECK ADD CONSTRAINT [FK_BankReceipt_TransactionId_PaymentTransaction]
    FOREIGN KEY ([TransactionId]) REFERENCES [finance].[PaymentTransaction] ([PaymentTransactionId]);
GO
CREATE INDEX [IX_BankReceipt_TransactionId] ON [finance].[BankReceipt] ([TransactionId]);
GO
ALTER TABLE [finance].[BankReceipt] WITH CHECK ADD CONSTRAINT [FK_BankReceipt_ReceivingAccountId_BankAccount]
    FOREIGN KEY ([ReceivingAccountId]) REFERENCES [party].[BankAccount] ([BankAccountId]);
GO
CREATE INDEX [IX_BankReceipt_ReceivingAccountId] ON [finance].[BankReceipt] ([ReceivingAccountId]);
GO
ALTER TABLE [finance].[BankReceipt] WITH CHECK ADD CONSTRAINT [FK_BankReceipt_VerifiedByContactId_Contact]
    FOREIGN KEY ([VerifiedByContactId]) REFERENCES [party].[Contact] ([ContactId]);
GO
CREATE INDEX [IX_BankReceipt_VerifiedByContactId] ON [finance].[BankReceipt] ([VerifiedByContactId]);
GO
ALTER TABLE [release].[ReleasePermit] WITH CHECK ADD CONSTRAINT [FK_ReleasePermit_BankReceiptId_BankReceipt]
    FOREIGN KEY ([BankReceiptId]) REFERENCES [finance].[BankReceipt] ([BankReceiptId]);
GO
CREATE INDEX [IX_ReleasePermit_BankReceiptId] ON [release].[ReleasePermit] ([BankReceiptId]);
GO
ALTER TABLE [release].[ReleasePermit] WITH CHECK ADD CONSTRAINT [FK_ReleasePermit_DeclarationId_Declaration]
    FOREIGN KEY ([DeclarationId]) REFERENCES [customs].[Declaration] ([DeclarationId]);
GO
CREATE INDEX [IX_ReleasePermit_DeclarationId] ON [release].[ReleasePermit] ([DeclarationId]);
GO
ALTER TABLE [release].[ReleasePermit] WITH CHECK ADD CONSTRAINT [FK_ReleasePermit_InspectionId_Inspection]
    FOREIGN KEY ([InspectionId]) REFERENCES [customs].[Inspection] ([InspectionId]);
GO
CREATE INDEX [IX_ReleasePermit_InspectionId] ON [release].[ReleasePermit] ([InspectionId]);
GO
ALTER TABLE [release].[GatePass] WITH CHECK ADD CONSTRAINT [FK_GatePass_ReleasePermitId_ReleasePermit]
    FOREIGN KEY ([ReleasePermitId]) REFERENCES [release].[ReleasePermit] ([ReleasePermitId]);
GO
CREATE INDEX [IX_GatePass_ReleasePermitId] ON [release].[GatePass] ([ReleasePermitId]);
GO
ALTER TABLE [release].[GatePass] WITH CHECK ADD CONSTRAINT [FK_GatePass_VehicleCarrierId_Carrier]
    FOREIGN KEY ([VehicleCarrierId]) REFERENCES [shipping].[Carrier] ([CarrierId]);
GO
CREATE INDEX [IX_GatePass_VehicleCarrierId] ON [release].[GatePass] ([VehicleCarrierId]);
GO
ALTER TABLE [release].[GatePass] WITH CHECK ADD CONSTRAINT [FK_GatePass_DriverContactId_Contact]
    FOREIGN KEY ([DriverContactId]) REFERENCES [party].[Contact] ([ContactId]);
GO
CREATE INDEX [IX_GatePass_DriverContactId] ON [release].[GatePass] ([DriverContactId]);
GO
ALTER TABLE [release].[GatePass] WITH CHECK ADD CONSTRAINT [FK_GatePass_ShipmentId_Shipment]
    FOREIGN KEY ([ShipmentId]) REFERENCES [shipping].[Shipment] ([ShipmentId]);
GO
CREATE INDEX [IX_GatePass_ShipmentId] ON [release].[GatePass] ([ShipmentId]);
GO
ALTER TABLE [release].[GateEvent] WITH CHECK ADD CONSTRAINT [FK_GateEvent_GatePassId_GatePass]
    FOREIGN KEY ([GatePassId]) REFERENCES [release].[GatePass] ([GatePassId]);
GO
CREATE INDEX [IX_GateEvent_GatePassId] ON [release].[GateEvent] ([GatePassId]);
GO
ALTER TABLE [release].[GateEvent] WITH CHECK ADD CONSTRAINT [FK_GateEvent_DepartureAppointmentId_DeliveryAppointment]
    FOREIGN KEY ([DepartureAppointmentId]) REFERENCES [shipping].[DeliveryAppointment] ([DeliveryAppointmentId]);
GO
CREATE INDEX [IX_GateEvent_DepartureAppointmentId] ON [release].[GateEvent] ([DepartureAppointmentId]);
GO
ALTER TABLE [release].[GateEvent] WITH CHECK ADD CONSTRAINT [FK_GateEvent_ExitAddressId_Address]
    FOREIGN KEY ([ExitAddressId]) REFERENCES [core].[Address] ([AddressId]);
GO
CREATE INDEX [IX_GateEvent_ExitAddressId] ON [release].[GateEvent] ([ExitAddressId]);
GO
ALTER TABLE [audit].[EventJournal] WITH CHECK ADD CONSTRAINT [FK_EventJournal_GateEventId_GateEvent]
    FOREIGN KEY ([GateEventId]) REFERENCES [release].[GateEvent] ([GateEventId]);
GO
CREATE INDEX [IX_EventJournal_GateEventId] ON [audit].[EventJournal] ([GateEventId]);
GO
ALTER TABLE [audit].[EventJournal] WITH CHECK ADD CONSTRAINT [FK_EventJournal_ShipmentId_Shipment]
    FOREIGN KEY ([ShipmentId]) REFERENCES [shipping].[Shipment] ([ShipmentId]);
GO
CREATE INDEX [IX_EventJournal_ShipmentId] ON [audit].[EventJournal] ([ShipmentId]);
GO
ALTER TABLE [audit].[EventJournal] WITH CHECK ADD CONSTRAINT [FK_EventJournal_PreviousEventId_EventJournal]
    FOREIGN KEY ([PreviousEventId]) REFERENCES [audit].[EventJournal] ([EventJournalId]);
GO
CREATE INDEX [IX_EventJournal_PreviousEventId] ON [audit].[EventJournal] ([PreviousEventId]);
GO
ALTER TABLE [audit].[EventJournal] WITH CHECK ADD CONSTRAINT [FK_EventJournal_ActorContactId_Contact]
    FOREIGN KEY ([ActorContactId]) REFERENCES [party].[Contact] ([ContactId]);
GO
CREATE INDEX [IX_EventJournal_ActorContactId] ON [audit].[EventJournal] ([ActorContactId]);
GO
ALTER TABLE [audit].[ArchiveItem] WITH CHECK ADD CONSTRAINT [FK_ArchiveItem_EventJournalId_EventJournal]
    FOREIGN KEY ([EventJournalId]) REFERENCES [audit].[EventJournal] ([EventJournalId]);
GO
CREATE INDEX [IX_ArchiveItem_EventJournalId] ON [audit].[ArchiveItem] ([EventJournalId]);
GO
ALTER TABLE [audit].[ArchiveItem] WITH CHECK ADD CONSTRAINT [FK_ArchiveItem_SourceDocumentId_ComplianceDocument]
    FOREIGN KEY ([SourceDocumentId]) REFERENCES [trade].[ComplianceDocument] ([ComplianceDocumentId]);
GO
CREATE INDEX [IX_ArchiveItem_SourceDocumentId] ON [audit].[ArchiveItem] ([SourceDocumentId]);
GO
ALTER TABLE [audit].[ArchiveItem] WITH CHECK ADD CONSTRAINT [FK_ArchiveItem_CompanyId_Company]
    FOREIGN KEY ([CompanyId]) REFERENCES [party].[Company] ([CompanyId]);
GO
CREATE INDEX [IX_ArchiveItem_CompanyId] ON [audit].[ArchiveItem] ([CompanyId]);
GO

-- Composite FKs: multi-column relationships must remain ONE edge in the diagram.
ALTER TABLE [trade].[PurchaseOrder] ADD CONSTRAINT [UQ_PurchaseOrder_TenantNumber] UNIQUE ([TenantId],[OrderNumber]);
GO
ALTER TABLE [trade].[OrderLine] WITH CHECK ADD CONSTRAINT [FK_OrderLine_CompositePurchaseOrder]
    FOREIGN KEY ([OrderTenantId],[OrderNumber]) REFERENCES [trade].[PurchaseOrder] ([TenantId],[OrderNumber]);
GO
ALTER TABLE [shipping].[Container] ADD CONSTRAINT [UQ_Container_ShipmentCode] UNIQUE ([ShipmentId],[ContainerCode]);
GO
ALTER TABLE [shipping].[ContainerLine] WITH CHECK ADD CONSTRAINT [FK_ContainerLine_CompositeContainer]
    FOREIGN KEY ([ContainerShipmentId],[ContainerCode]) REFERENCES [shipping].[Container] ([ShipmentId],[ContainerCode]);
GO

-- A final metadata check; should return 43 tables and the complete FK count.
SELECT COUNT(*) AS LabTableCount FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name IN (N'core',N'party',N'trade',N'shipping',N'customs',N'finance',N'release',N'audit');
SELECT COUNT(*) AS LabForeignKeyCount FROM sys.foreign_keys;
SELECT TOP (30) fk.name AS ForeignKeyName,
  OBJECT_SCHEMA_NAME(fk.parent_object_id)+N'.'+OBJECT_NAME(fk.parent_object_id) AS ChildTable,
  OBJECT_SCHEMA_NAME(fk.referenced_object_id)+N'.'+OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk ORDER BY ChildTable, ParentTable;
GO

