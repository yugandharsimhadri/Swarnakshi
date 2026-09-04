/*
    Swarnakshi -- create the SCOPS database, its login and its application user.

    Run once per server, as a sysadmin, before the first deployment:

        sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\01-create-database.sql -v AppPassword="<password>"

    Idempotent: safe to re-run. It never drops anything and never resets an existing
    password -- rotate a password with 02-rotate-password.sql instead.

    The application user is deliberately NOT db_owner. It gets db_datareader,
    db_datawriter and EXECUTE, plus the DDL rights EF Core migrations need. That is
    enough to run the app and to migrate it, and not enough to drop the database.
*/

:on error exit
SET NOCOUNT ON;
GO

:setvar DbName "SCOPS"
:setvar AppLogin "SivayaanHMS"

-- AppPassword must be supplied on the command line with -v AppPassword="..."
-- so the real password never lives in this file or in source control.
IF '$(AppPassword)' = '' OR '$(AppPassword)' = '$' + '(AppPassword)'
BEGIN
    RAISERROR('Pass the application password with:  -v AppPassword="<password>"', 20, 1) WITH LOG;
END
GO

/* ---------- 1. the database ---------- */
IF DB_ID(N'$(DbName)') IS NULL
BEGIN
    PRINT 'Creating database $(DbName)...';
    CREATE DATABASE [$(DbName)];
END
ELSE
    PRINT 'Database $(DbName) already exists -- left as it is.';
GO

/*  READ_COMMITTED_SNAPSHOT keeps readers from blocking writers. The app posts inventory and
    financial side effects inside one transaction; without RCSI a long post blocks every
    dashboard query behind it. Set in single-user mode so no open session stops the change. */
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'$(DbName)' AND is_read_committed_snapshot_on = 0)
BEGIN
    PRINT 'Enabling READ_COMMITTED_SNAPSHOT...';
    ALTER DATABASE [$(DbName)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    ALTER DATABASE [$(DbName)] SET READ_COMMITTED_SNAPSHOT ON;
    ALTER DATABASE [$(DbName)] SET MULTI_USER;
END
GO

--  SIMPLE until there is a scheduled log backup. 03-backup.ps1 takes FULL backups; switch to
--  FULL recovery only once log backups are scheduled, or the log grows without bound.
ALTER DATABASE [$(DbName)] SET RECOVERY SIMPLE;
ALTER DATABASE [$(DbName)] SET AUTO_CLOSE OFF;      -- Express defaults this ON; it costs a slow first request
ALTER DATABASE [$(DbName)] SET AUTO_SHRINK OFF;
GO

/* ---------- 2. the login ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'$(AppLogin)')
BEGIN
    PRINT 'Creating login $(AppLogin)...';
    -- CHECK_POLICY ON: the server's own password policy applies. CHECK_EXPIRATION OFF, because
    -- a service account whose password expires takes the application down at 3am.
    CREATE LOGIN [$(AppLogin)]
        WITH PASSWORD = N'$(AppPassword)',
             DEFAULT_DATABASE = [$(DbName)],
             CHECK_POLICY = ON,
             CHECK_EXPIRATION = OFF;
END
ELSE
    PRINT 'Login $(AppLogin) already exists -- password left unchanged.';
GO

/* ---------- 3. the database user and its rights ---------- */
USE [$(DbName)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppLogin)')
BEGIN
    PRINT 'Creating user $(AppLogin) in $(DbName)...';
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)];
END
GO

ALTER ROLE db_datareader ADD MEMBER [$(AppLogin)];
ALTER ROLE db_datawriter ADD MEMBER [$(AppLogin)];
GO

--  EF Core migrations issue CREATE/ALTER TABLE, CREATE INDEX and INSERT into __EFMigrationsHistory.
--  Granting the DDL rights directly is narrower than db_owner: the account can shape the schema
--  it owns and cannot drop the database, change its options, or manage other principals.
GRANT EXECUTE            TO [$(AppLogin)];
GRANT CREATE TABLE       TO [$(AppLogin)];
GRANT CREATE VIEW        TO [$(AppLogin)];
GRANT CREATE PROCEDURE   TO [$(AppLogin)];
GRANT ALTER  ON SCHEMA::[dbo] TO [$(AppLogin)];
GRANT REFERENCES ON SCHEMA::[dbo] TO [$(AppLogin)];
GO

/* ---------- 4. report ---------- */
SELECT
    DatabaseName        = DB_NAME(),
    Collation           = CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation')),
    RecoveryModel       = CONVERT(nvarchar(20),  DATABASEPROPERTYEX(DB_NAME(), 'Recovery')),
    SnapshotIsolation   = (SELECT CONVERT(bit, is_read_committed_snapshot_on)
                           FROM sys.databases WHERE name = DB_NAME()),
    AppUser             = (SELECT name FROM sys.database_principals WHERE name = N'$(AppLogin)');
GO

PRINT 'Done. $(DbName) is ready for the first deployment.';
GO
