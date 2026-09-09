/*
    Swarnakshi - database upgrade.

    GENERATED FILE. Regenerate with
        powershell -File deploy\scripts\New-UpgradeScript.ps1 -From 20260904091235_PerformanceIndexes

    From: 20260904091235_PerformanceIndexes
    To:   the current build

    Run it against the LIVE database, before or during the deployment of the matching build:

        sqlcmd -S .\SQLEXPRESS -E -C -b -d COPS -i 2026-09-09-engineer-role-and-contract-amount-only.sql

    -b matters: without it sqlcmd returns success even when a batch failed, and a half-applied
    upgrade looks like a clean run.

    Idempotent, and it agrees with the application. Every statement is wrapped in a check against
    __EFMigrationsHistory, so running it twice does nothing the second time - and if the API has
    already migrated itself on restart, this finds the work done and changes nothing. Running both
    is not a mistake; it is the intended belt and braces.

    Schema only. No master data: settings, expense heads, units and the material taxonomy are
    seeded by the application on first start, not here.

    Generated: 2026-09-09 10:13:54 from commit d7b417a
*/

-- sqlcmd connects with QUOTED_IDENTIFIER OFF and SQL Server refuses to create this schema's
-- indexes under that setting. SSMS defaults it ON, so without these two lines the script works in
-- SSMS and fails on the command line - which is where a DBA would actually run it.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    ALTER TABLE [SupplierPayments] ADD [ApprovedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    ALTER TABLE [SupplierPayments] ADD [ApprovedBy] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    ALTER TABLE [SupplierPayments] ADD [ConcurrencyToken] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    ALTER TABLE [SupplierPayments] ADD [ModifiedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    ALTER TABLE [SupplierPayments] ADD [ModifiedBy] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    ALTER TABLE [SupplierPayments] ADD [Remarks] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    ALTER TABLE [SupplierPayments] ADD [Status] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    EXEC(N'UPDATE [SupplierPayments] SET [Status] = 6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    EXEC(N'UPDATE [SupplierPayments] SET [ConcurrencyToken] = NEWID()');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905075817_ApprovalGate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905075817_ApprovalGate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909043949_EngineerRoleAndContractAmountOnly'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContractWorks]') AND [c].[name] = N'EstimatedCost');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [ContractWorks] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [ContractWorks] DROP COLUMN [EstimatedCost];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909043949_EngineerRoleAndContractAmountOnly'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909043949_EngineerRoleAndContractAmountOnly', N'10.0.0');
END;

COMMIT;
GO


