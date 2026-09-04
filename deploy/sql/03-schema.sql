/*
    Swarnakshi - complete database schema.

    GENERATED FILE. Do not edit by hand: regenerate with
        powershell -File deploy\scripts\New-SchemaScript.ps1
    after adding an EF migration, or your edit is lost on the next build.

    Run it against a database that already exists (create it with 01-create-database.sql):

        sqlcmd -S .\SQLEXPRESS -E -C -b -d SCOPS -i 03-schema.sql

    Idempotent. Every migration is wrapped in a check against __EFMigrationsHistory, so running
    this twice does nothing the second time, and running it against a partly-migrated database
    applies only what is missing.

    Applying this by hand is optional. Deploy.ps1 applies the same migrations itself through
    Swarnakshi.Api.exe --migrate, and finding the work already done it simply reports the schema is
    up to date. Doing it here is for sites where only a DBA may change the schema - and it means
    the application login never needs CREATE TABLE or ALTER at all.

    It creates tables, indexes and foreign keys. It does NOT create master data: the platform
    operator, the founding company, expense heads, units and the material taxonomy are seeded in
    application code the first time the service starts, not here.

    Generated: 2026-09-04 14:26:02 from commit 89376ba
*/

-- sqlcmd connects with QUOTED_IDENTIFIER OFF and SQL Server will not create this schema's indexes
-- under that setting. SSMS defaults it ON, so without these two lines the script works in SSMS and
-- dies on the command line after one table - which is the worst way for it to fail.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ApprovalRequests] (
        [Id] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(512) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [EntityRef] nvarchar(512) NULL,
        [SiteId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NULL,
        [CurrentStatus] int NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [RequestedAt] datetimeoffset NOT NULL,
        [DecidedByUserId] uniqueidentifier NULL,
        [DecidedAt] datetimeoffset NULL,
        [Remarks] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_ApprovalRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Attachments] (
        [Id] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(512) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(512) NOT NULL,
        [ContentType] nvarchar(512) NOT NULL,
        [Size] bigint NOT NULL,
        [StoragePath] nvarchar(512) NOT NULL,
        [UploadedByUserId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(512) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Action] nvarchar(512) NOT NULL,
        [DataJson] nvarchar(512) NULL,
        [UserId] uniqueidentifier NULL,
        [At] datetimeoffset NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ContactEmail] nvarchar(512) NULL,
        [ContactMobile] nvarchar(512) NULL,
        [LicenseExpiresOn] date NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(512) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Contractors] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(512) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [CompanyName] nvarchar(512) NULL,
        [Mobile] nvarchar(512) NULL,
        [Email] nvarchar(512) NULL,
        [Address] nvarchar(512) NULL,
        [Pan] nvarchar(512) NULL,
        [Gstin] nvarchar(512) NULL,
        [BankDetails] nvarchar(512) NULL,
        [ContractorType] nvarchar(512) NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Contractors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(512) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [Mobile] nvarchar(512) NULL,
        [Email] nvarchar(512) NULL,
        [Address] nvarchar(512) NULL,
        [Pan] nvarchar(512) NULL,
        [Gstin] nvarchar(512) NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpenseHeads] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_ExpenseHeads] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [LabourCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_LabourCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [MaterialCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_MaterialCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentMethods] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_PaymentMethods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [PlatformUsers] (
        [Id] uniqueidentifier NOT NULL,
        [Username] nvarchar(60) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [RefreshToken] nvarchar(512) NULL,
        [RefreshTokenExpiry] datetimeoffset NULL,
        [LastLoginAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PlatformUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ProjectTypes] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_ProjectTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(512) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [Mobile] nvarchar(512) NULL,
        [Email] nvarchar(512) NULL,
        [Address] nvarchar(512) NULL,
        [Pan] nvarchar(512) NULL,
        [Gstin] nvarchar(512) NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [TransactionSequences] (
        [Id] uniqueidentifier NOT NULL,
        [Prefix] nvarchar(512) NOT NULL,
        [Year] int NOT NULL,
        [LastNumber] int NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_TransactionSequences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Units] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Units] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Username] nvarchar(60) NOT NULL,
        [Email] nvarchar(256) NULL,
        [Mobile] nvarchar(20) NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsCompanyAdmin] bit NOT NULL,
        [RefreshToken] nvarchar(512) NULL,
        [RefreshTokenExpiry] datetimeoffset NULL,
        [TokensValidFrom] datetimeoffset NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ApprovalHistories] (
        [Id] uniqueidentifier NOT NULL,
        [ApprovalRequestId] uniqueidentifier NOT NULL,
        [Action] int NOT NULL,
        [PreviousStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [At] datetimeoffset NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_ApprovalHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApprovalHistories_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [ApprovalRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpenseSubheads] (
        [Id] uniqueidentifier NOT NULL,
        [ExpenseHeadId] uniqueidentifier NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_ExpenseSubheads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseSubheads_ExpenseHeads_ExpenseHeadId] FOREIGN KEY ([ExpenseHeadId]) REFERENCES [ExpenseHeads] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [MaterialSubcategories] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialCategoryId] uniqueidentifier NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_MaterialSubcategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialSubcategories_MaterialCategories_MaterialCategoryId] FOREIGN KEY ([MaterialCategoryId]) REFERENCES [MaterialCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Sites] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [Address] nvarchar(512) NULL,
        [City] nvarchar(512) NULL,
        [State] nvarchar(512) NULL,
        [Pin] nvarchar(512) NULL,
        [SupervisorUserId] uniqueidentifier NULL,
        [StartDate] date NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Sites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sites_Users_SupervisorUserId] FOREIGN KEY ([SupervisorUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [UserPermissions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PermissionKey] nvarchar(512) NOT NULL,
        [Granted] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [MaterialSpecDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialSubcategoryId] uniqueidentifier NOT NULL,
        [Key] nvarchar(60) NOT NULL,
        [Label] nvarchar(120) NOT NULL,
        [Kind] int NOT NULL,
        [Options] nvarchar(600) NULL,
        [IsRequired] bit NOT NULL,
        [PartOfIdentity] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_MaterialSpecDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialSpecDefinitions_MaterialSubcategories_MaterialSubcategoryId] FOREIGN KEY ([MaterialSubcategoryId]) REFERENCES [MaterialSubcategories] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Materials] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [MaterialSubcategoryId] uniqueidentifier NOT NULL,
        [Brand] nvarchar(120) NULL,
        [Description] nvarchar(512) NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [SecondaryUnitId] uniqueidentifier NULL,
        [ConversionFactor] decimal(18,2) NULL,
        [GenericMeasurement] nvarchar(120) NULL,
        [MinStockLevel] decimal(18,2) NOT NULL,
        [ReorderLevel] decimal(18,2) NOT NULL,
        [DefaultPurchaseRate] decimal(18,2) NOT NULL,
        [GstRate] decimal(18,2) NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(512) NULL,
        [SpecSummary] nvarchar(400) NULL,
        [SpecSignature] nvarchar(500) NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Materials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Materials_MaterialSubcategories_MaterialSubcategoryId] FOREIGN KEY ([MaterialSubcategoryId]) REFERENCES [MaterialSubcategories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Materials_Units_SecondaryUnitId] FOREIGN KEY ([SecondaryUnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Materials_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [MonthlySalary] decimal(18,2) NOT NULL,
        [JoinDate] date NOT NULL,
        [LeaveDate] date NULL,
        [Designation] nvarchar(120) NULL,
        [Address] nvarchar(512) NULL,
        [Notes] nvarchar(512) NULL,
        [IsActive] bit NOT NULL,
        [SiteId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Projects] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [VillaNumber] nvarchar(512) NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NULL,
        [ProjectTypeId] uniqueidentifier NULL,
        [Address] nvarchar(512) NULL,
        [StartDate] date NULL,
        [ExpectedCompletionDate] date NULL,
        [ActualCompletionDate] date NULL,
        [EstimatedCost] decimal(18,2) NOT NULL,
        [ContractSaleValue] decimal(18,2) NULL,
        [Status] int NOT NULL,
        [CompletionPercent] int NOT NULL,
        [Notes] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Projects_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Projects_ProjectTypes_ProjectTypeId] FOREIGN KEY ([ProjectTypeId]) REFERENCES [ProjectTypes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Projects_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [Settings] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(512) NOT NULL,
        [Value] nvarchar(512) NOT NULL,
        [SiteId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_Settings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Settings_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [SiteExpenses] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [ExpenseHeadId] uniqueidentifier NOT NULL,
        [Description] nvarchar(512) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentStatus] int NOT NULL,
        [PaymentMethodId] uniqueidentifier NULL,
        [SourceType] nvarchar(512) NULL,
        [SourceId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SiteExpenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SiteExpenses_ExpenseHeads_ExpenseHeadId] FOREIGN KEY ([ExpenseHeadId]) REFERENCES [ExpenseHeads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SiteExpenses_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SiteExpenses_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [UserSiteAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_UserSiteAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserSiteAssignments_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserSiteAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [InventoryBalances] (
        [Id] uniqueidentifier NOT NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [AverageRate] decimal(18,2) NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [LastMovementAt] datetimeoffset NULL,
        [LastPurchaseRate] decimal(18,2) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_InventoryBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryBalances_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryBalances_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [MaterialSpecValues] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [MaterialSpecDefinitionId] uniqueidentifier NOT NULL,
        [Value] nvarchar(200) NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_MaterialSpecValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialSpecValues_MaterialSpecDefinitions_MaterialSpecDefinitionId] FOREIGN KEY ([MaterialSpecDefinitionId]) REFERENCES [MaterialSpecDefinitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaterialSpecValues_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ContractWorks] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ContractorId] uniqueidentifier NOT NULL,
        [WorkCategory] nvarchar(512) NOT NULL,
        [Description] nvarchar(512) NULL,
        [EstimatedCost] decimal(18,2) NOT NULL,
        [ContractAmount] decimal(18,2) NOT NULL,
        [StartDate] date NULL,
        [ExpectedCompletion] date NULL,
        [ActualCompletion] date NULL,
        [PaymentTerms] nvarchar(512) NULL,
        [WorkStatus] int NOT NULL,
        [TotalPaid] decimal(18,2) NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ContractWorks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractWorks_Contractors_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [Contractors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ContractWorks_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomerPayments] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethodId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(512) NULL,
        [Description] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CustomerPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerPayments_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerPayments_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerPayments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [EmployeePayments] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Kind] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [AdvanceRecovered] decimal(18,2) NOT NULL,
        [PeriodStart] date NULL,
        [PeriodEnd] date NULL,
        [PaymentMethodId] uniqueidentifier NULL,
        [Reference] nvarchar(512) NULL,
        [ProjectId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_EmployeePayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeePayments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeePayments_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeePayments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [InventoryTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [Date] date NOT NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Rate] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Type] int NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [SourceType] nvarchar(512) NULL,
        [SourceId] uniqueidentifier NULL,
        [SourceRef] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryTransactions_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [LabourEntries] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [LabourCategoryId] uniqueidentifier NOT NULL,
        [PeriodType] int NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethodId] uniqueidentifier NULL,
        [PaymentType] nvarchar(512) NULL,
        [Remarks] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_LabourEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LabourEntries_LabourCategories_LabourCategoryId] FOREIGN KEY ([LabourCategoryId]) REFERENCES [LabourCategories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabourEntries_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabourEntries_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [MaterialRequests] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [RequestType] int NOT NULL,
        [RequestStatus] int NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Notes] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_MaterialRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialRequests_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaterialRequests_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ProjectExpenses] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [ExpenseHeadId] uniqueidentifier NOT NULL,
        [ExpenseSubheadId] uniqueidentifier NULL,
        [Description] nvarchar(512) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ExpenseType] int NOT NULL,
        [PaymentStatus] int NOT NULL,
        [PaymentMethodId] uniqueidentifier NULL,
        [SourceType] nvarchar(512) NULL,
        [SourceId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ProjectExpenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectExpenses_ExpenseHeads_ExpenseHeadId] FOREIGN KEY ([ExpenseHeadId]) REFERENCES [ExpenseHeads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectExpenses_ExpenseSubheads_ExpenseSubheadId] FOREIGN KEY ([ExpenseSubheadId]) REFERENCES [ExpenseSubheads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectExpenses_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectExpenses_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [ContractorPayments] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [ContractorId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ContractWorkId] uniqueidentifier NULL,
        [Date] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethodId] uniqueidentifier NOT NULL,
        [ReferenceNumber] nvarchar(512) NULL,
        [Description] nvarchar(512) NULL,
        [PaymentKind] int NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ContractorPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractorPayments_ContractWorks_ContractWorkId] FOREIGN KEY ([ContractWorkId]) REFERENCES [ContractWorks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ContractorPayments_Contractors_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [Contractors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ContractorPayments_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ContractorPayments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [MaterialRequestItems] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialRequestId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [RequestedQty] decimal(18,2) NOT NULL,
        [ApprovedQty] decimal(18,2) NULL,
        [IssuedQty] decimal(18,2) NOT NULL,
        [Rate] decimal(18,2) NULL,
        [ExpenseHeadId] uniqueidentifier NULL,
        [ExpenseSubheadId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_MaterialRequestItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialRequestItems_ExpenseHeads_ExpenseHeadId] FOREIGN KEY ([ExpenseHeadId]) REFERENCES [ExpenseHeads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaterialRequestItems_ExpenseSubheads_ExpenseSubheadId] FOREIGN KEY ([ExpenseSubheadId]) REFERENCES [ExpenseSubheads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaterialRequestItems_MaterialRequests_MaterialRequestId] FOREIGN KEY ([MaterialRequestId]) REFERENCES [MaterialRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MaterialRequestItems_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaterialRequestItems_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [PurchaseHeaders] (
        [Id] uniqueidentifier NOT NULL,
        [TxnNumber] nvarchar(512) NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [MaterialRequestId] uniqueidentifier NULL,
        [InvoiceNumber] nvarchar(512) NULL,
        [InvoiceDate] date NULL,
        [Date] date NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [Discount] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [OtherCharges] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [BalanceAmount] decimal(18,2) NOT NULL,
        [PaymentStatus] int NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [Remarks] nvarchar(512) NULL,
        [ConcurrencyToken] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PurchaseHeaders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseHeaders_MaterialRequests_MaterialRequestId] FOREIGN KEY ([MaterialRequestId]) REFERENCES [MaterialRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseHeaders_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseHeaders_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseHeaders_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [PurchaseItems] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseHeaderId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Rate] decimal(18,2) NOT NULL,
        [Discount] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [DeliverToProjectId] uniqueidentifier NULL,
        [ExpenseHeadId] uniqueidentifier NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_PurchaseItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseItems_ExpenseHeads_ExpenseHeadId] FOREIGN KEY ([ExpenseHeadId]) REFERENCES [ExpenseHeads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseItems_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseItems_Projects_DeliverToProjectId] FOREIGN KEY ([DeliverToProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseItems_PurchaseHeaders_PurchaseHeaderId] FOREIGN KEY ([PurchaseHeaderId]) REFERENCES [PurchaseHeaders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseItems_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE TABLE [SupplierPayments] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseHeaderId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethodId] uniqueidentifier NULL,
        [Reference] nvarchar(512) NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_SupplierPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierPayments_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]),
        CONSTRAINT [FK_SupplierPayments_PurchaseHeaders_PurchaseHeaderId] FOREIGN KEY ([PurchaseHeaderId]) REFERENCES [PurchaseHeaders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalHistories_ApprovalRequestId] ON [ApprovalHistories] ([ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalHistories_CompanyId] ON [ApprovalHistories] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalRequests_CompanyId] ON [ApprovalRequests] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalRequests_CurrentStatus] ON [ApprovalRequests] ([CurrentStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalRequests_EntityType_EntityId] ON [ApprovalRequests] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attachments_CompanyId] ON [Attachments] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attachments_EntityType_EntityId] ON [Attachments] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CompanyId] ON [AuditLogs] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_Code] ON [Companies] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_Name] ON [Companies] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractWorks_CompanyId] ON [ContractWorks] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractWorks_ContractorId] ON [ContractWorks] ([ContractorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractWorks_ProjectId] ON [ContractWorks] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractorPayments_CompanyId] ON [ContractorPayments] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ContractorPayments_CompanyId_TxnNumber] ON [ContractorPayments] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractorPayments_ContractWorkId] ON [ContractorPayments] ([ContractWorkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractorPayments_ContractorId] ON [ContractorPayments] ([ContractorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractorPayments_PaymentMethodId] ON [ContractorPayments] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractorPayments_ProjectId] ON [ContractorPayments] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contractors_CompanyId] ON [Contractors] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Contractors_CompanyId_Code] ON [Contractors] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerPayments_CompanyId] ON [CustomerPayments] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerPayments_CompanyId_TxnNumber] ON [CustomerPayments] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerPayments_CustomerId] ON [CustomerPayments] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerPayments_PaymentMethodId] ON [CustomerPayments] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerPayments_ProjectId] ON [CustomerPayments] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_CompanyId] ON [Customers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_CompanyId_Code] ON [Customers] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmployeePayments_CompanyId] ON [EmployeePayments] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmployeePayments_CompanyId_TxnNumber] ON [EmployeePayments] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmployeePayments_EmployeeId_Date] ON [EmployeePayments] ([EmployeeId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmployeePayments_PaymentMethodId] ON [EmployeePayments] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmployeePayments_ProjectId] ON [EmployeePayments] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_CompanyId] ON [Employees] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_CompanyId_Code] ON [Employees] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_CompanyId_Phone] ON [Employees] ([CompanyId], [Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_SiteId] ON [Employees] ([SiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExpenseHeads_CompanyId] ON [ExpenseHeads] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExpenseSubheads_CompanyId] ON [ExpenseSubheads] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExpenseSubheads_CompanyId_ExpenseHeadId_Name] ON [ExpenseSubheads] ([CompanyId], [ExpenseHeadId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExpenseSubheads_ExpenseHeadId] ON [ExpenseSubheads] ([ExpenseHeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryBalances_CompanyId] ON [InventoryBalances] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryBalances_CompanyId_SiteId_MaterialId] ON [InventoryBalances] ([CompanyId], [SiteId], [MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryBalances_MaterialId] ON [InventoryBalances] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryBalances_SiteId] ON [InventoryBalances] ([SiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_CompanyId] ON [InventoryTransactions] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryTransactions_CompanyId_TxnNumber] ON [InventoryTransactions] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_MaterialId] ON [InventoryTransactions] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_ProjectId] ON [InventoryTransactions] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_SiteId_MaterialId_Date] ON [InventoryTransactions] ([SiteId], [MaterialId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_UnitId] ON [InventoryTransactions] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabourCategories_CompanyId] ON [LabourCategories] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabourEntries_CompanyId] ON [LabourEntries] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LabourEntries_CompanyId_TxnNumber] ON [LabourEntries] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabourEntries_LabourCategoryId] ON [LabourEntries] ([LabourCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabourEntries_PaymentMethodId] ON [LabourEntries] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabourEntries_ProjectId] ON [LabourEntries] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialCategories_CompanyId] ON [MaterialCategories] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequestItems_CompanyId] ON [MaterialRequestItems] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequestItems_ExpenseHeadId] ON [MaterialRequestItems] ([ExpenseHeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequestItems_ExpenseSubheadId] ON [MaterialRequestItems] ([ExpenseSubheadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequestItems_MaterialId] ON [MaterialRequestItems] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequestItems_MaterialRequestId] ON [MaterialRequestItems] ([MaterialRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequestItems_UnitId] ON [MaterialRequestItems] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequests_CompanyId] ON [MaterialRequests] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialRequests_CompanyId_TxnNumber] ON [MaterialRequests] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequests_ProjectId] ON [MaterialRequests] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialRequests_SiteId] ON [MaterialRequests] ([SiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSpecDefinitions_CompanyId] ON [MaterialSpecDefinitions] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialSpecDefinitions_CompanyId_MaterialSubcategoryId_Key] ON [MaterialSpecDefinitions] ([CompanyId], [MaterialSubcategoryId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSpecDefinitions_MaterialSubcategoryId] ON [MaterialSpecDefinitions] ([MaterialSubcategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSpecValues_CompanyId] ON [MaterialSpecValues] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialSpecValues_CompanyId_MaterialId_MaterialSpecDefinitionId] ON [MaterialSpecValues] ([CompanyId], [MaterialId], [MaterialSpecDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSpecValues_MaterialId] ON [MaterialSpecValues] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSpecValues_MaterialSpecDefinitionId] ON [MaterialSpecValues] ([MaterialSpecDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSpecValues_Value] ON [MaterialSpecValues] ([Value]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSubcategories_CompanyId] ON [MaterialSubcategories] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialSubcategories_CompanyId_MaterialCategoryId_Name] ON [MaterialSubcategories] ([CompanyId], [MaterialCategoryId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaterialSubcategories_MaterialCategoryId] ON [MaterialSubcategories] ([MaterialCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Materials_Brand] ON [Materials] ([Brand]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Materials_CompanyId] ON [Materials] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Materials_CompanyId_Code] ON [Materials] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Materials_CompanyId_SpecSignature] ON [Materials] ([CompanyId], [SpecSignature]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Materials_IsActive] ON [Materials] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Materials_MaterialSubcategoryId] ON [Materials] ([MaterialSubcategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Materials_SecondaryUnitId] ON [Materials] ([SecondaryUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Materials_UnitId] ON [Materials] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentMethods_CompanyId] ON [PaymentMethods] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlatformUsers_Username] ON [PlatformUsers] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectExpenses_CompanyId] ON [ProjectExpenses] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectExpenses_CompanyId_TxnNumber] ON [ProjectExpenses] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectExpenses_ExpenseHeadId] ON [ProjectExpenses] ([ExpenseHeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectExpenses_ExpenseSubheadId] ON [ProjectExpenses] ([ExpenseSubheadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectExpenses_PaymentMethodId] ON [ProjectExpenses] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectExpenses_ProjectId_Date] ON [ProjectExpenses] ([ProjectId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectTypes_CompanyId] ON [ProjectTypes] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Projects_CompanyId] ON [Projects] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_CompanyId_Code] ON [Projects] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Projects_CustomerId] ON [Projects] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Projects_ProjectTypeId] ON [Projects] ([ProjectTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Projects_SiteId] ON [Projects] ([SiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseHeaders_CompanyId] ON [PurchaseHeaders] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseHeaders_CompanyId_TxnNumber] ON [PurchaseHeaders] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseHeaders_MaterialRequestId] ON [PurchaseHeaders] ([MaterialRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseHeaders_ProjectId] ON [PurchaseHeaders] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseHeaders_SiteId] ON [PurchaseHeaders] ([SiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseHeaders_SupplierId] ON [PurchaseHeaders] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseItems_CompanyId] ON [PurchaseItems] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseItems_DeliverToProjectId] ON [PurchaseItems] ([DeliverToProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseItems_ExpenseHeadId] ON [PurchaseItems] ([ExpenseHeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseItems_MaterialId] ON [PurchaseItems] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseItems_PurchaseHeaderId] ON [PurchaseItems] ([PurchaseHeaderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseItems_UnitId] ON [PurchaseItems] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Settings_CompanyId] ON [Settings] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Settings_CompanyId_Key_SiteId] ON [Settings] ([CompanyId], [Key], [SiteId]) WHERE [SiteId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Settings_SiteId] ON [Settings] ([SiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SiteExpenses_CompanyId] ON [SiteExpenses] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SiteExpenses_CompanyId_TxnNumber] ON [SiteExpenses] ([CompanyId], [TxnNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SiteExpenses_ExpenseHeadId] ON [SiteExpenses] ([ExpenseHeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SiteExpenses_PaymentMethodId] ON [SiteExpenses] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SiteExpenses_SiteId_Date] ON [SiteExpenses] ([SiteId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sites_CompanyId] ON [Sites] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sites_CompanyId_Code] ON [Sites] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sites_SupervisorUserId] ON [Sites] ([SupervisorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierPayments_CompanyId] ON [SupplierPayments] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierPayments_PaymentMethodId] ON [SupplierPayments] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierPayments_PurchaseHeaderId] ON [SupplierPayments] ([PurchaseHeaderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_CompanyId] ON [Suppliers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Suppliers_CompanyId_Code] ON [Suppliers] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TransactionSequences_CompanyId] ON [TransactionSequences] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TransactionSequences_CompanyId_Prefix_Year] ON [TransactionSequences] ([CompanyId], [Prefix], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Units_CompanyId] ON [Units] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Units_CompanyId_Code] ON [Units] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_CompanyId] ON [UserPermissions] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_UserId] ON [UserPermissions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserSiteAssignments_CompanyId] ON [UserSiteAssignments] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserSiteAssignments_SiteId] ON [UserSiteAssignments] ([SiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserSiteAssignments_UserId] ON [UserSiteAssignments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_CompanyId] ON [Users] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_CompanyId_Username] ON [Users] ([CompanyId], [Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_Mobile] ON [Users] ([Mobile]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903165631_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260903165631_InitialCreate', N'10.0.0');
END;

COMMIT;
GO


