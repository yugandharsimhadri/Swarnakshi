/*
    Swarnakshi - the LAST SQL Server upgrade, to run once before the move to PostgreSQL.

    The data migrator reads the SQL Server database through the current application model, so
    the SQL Server schema has to be the one that model was last generated against - migration
    20260909134423_AuditTrail. A database deployed before 9 September 2026 is behind it, and the
    migrator stops with "Invalid column name 'ApprovedAt'" on the first table it reaches. This
    script brings it forward. The migrator checks __EFMigrationsHistory for the migration and
    refuses to start until it is there.

    Run it against the LIVE SQL Server database, with the API stopped (11-postgresql.md, 4.2a):

        sqlcmd -S .\SQLEXPRESS -E -C -b -d COPS -i 2026-09-09-last-sqlserver-upgrade.sql

    -b matters: without it sqlcmd returns success even when a batch failed, and a half-applied
    upgrade looks like a clean run.

    Idempotent. Every statement is wrapped in a check against __EFMigrationsHistory, so running
    it against a database that already has some of these migrations applies only what is missing,
    and running it twice does nothing the second time.

    What it does: the approval-gate columns on SupplierPayments (existing payments become
    Approved - they were paid before approval existed); drops ContractWorks.EstimatedCost; widens
    AuditLogs and indexes it. Schema and the two data fixes only - no master data.

    This is the only SQL Server script left in the repository. It is not generated any more:
    the SQL Server migrations it was generated from were replaced by the PostgreSQL ones.
    Frozen as of commit 965bb9f.
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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    UPDATE [AuditLogs] SET [Action] = LEFT([Action], 400) WHERE LEN([Action]) > 400;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    UPDATE [AuditLogs] SET [EntityType] = LEFT([EntityType], 100) WHERE LEN([EntityType]) > 100;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'EntityType');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [AuditLogs] ALTER COLUMN [EntityType] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'DataJson');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [AuditLogs] ALTER COLUMN [DataJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'Action');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [AuditLogs] ALTER COLUMN [Action] nvarchar(400) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Entity] ON [AuditLogs] ([CompanyId], [EntityType], [EntityId], [At]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_When] ON [AuditLogs] ([CompanyId], [At]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909134423_AuditTrail'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909134423_AuditTrail', N'10.0.0');
END;

COMMIT;
GO


