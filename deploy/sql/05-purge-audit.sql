/*
    Swarnakshi -- trim the audit trail.

        sqlcmd -S .\SQLEXPRESS -E -C -b -d COPS -i 05-purge-audit.sql -v KeepMonths="24"

    WHY THIS EXISTS. AuditLogs is append-only: every insert, edit and delete in the application
    writes a row and nothing ever removes one. That is the correct design for a trail, and it means
    the table only grows. SQL Server EXPRESS caps a database at 10 GB, and a cap you reach without
    warning takes the whole application down -- not just the trail -- because every write then fails.

    So this is not housekeeping. It is the thing that stops an audit trail becoming an outage.

    It deletes nothing recent. KeepMonths is required and must be at least 12: a trail shorter than
    a financial year cannot answer the questions a trail is kept for.

    Deletes in batches, so a large first run does not hold one enormous transaction and block the
    application while it runs. Safe to stop and re-run; it simply carries on.

    HOW MUCH IS IN THERE. Before deciding, look:

        SELECT COUNT(*) AS Rows,
               CAST(SUM(DATALENGTH(DataJson)) / 1048576.0 AS decimal(10,1)) AS DataMB,
               MIN(At) AS Oldest, MAX(At) AS Newest
        FROM AuditLogs;
*/

:on error exit
SET NOCOUNT ON;
GO

IF '$(KeepMonths)' = '' OR '$(KeepMonths)' = '$' + '(KeepMonths)'
    RAISERROR('Pass how many months to keep:  -v KeepMonths="24"', 20, 1) WITH LOG;
GO

DECLARE @keep int = TRY_CAST('$(KeepMonths)' AS int);

IF @keep IS NULL OR @keep < 12
BEGIN
    RAISERROR('KeepMonths must be a whole number of at least 12. A trail shorter than a financial year cannot answer the questions it is kept for.', 20, 1) WITH LOG;
END
GO

DECLARE @keep int = TRY_CAST('$(KeepMonths)' AS int);
DECLARE @cutoff datetimeoffset = DATEADD(month, -@keep, SYSDATETIMEOFFSET());
DECLARE @total bigint = 0, @batch int = 1;

PRINT 'Deleting audit rows written before ' + CONVERT(varchar(33), @cutoff, 127);

WHILE @batch > 0
BEGIN
    DELETE TOP (5000) FROM [AuditLogs] WHERE [At] < @cutoff;
    SET @batch = @@ROWCOUNT;
    SET @total += @batch;
END

PRINT 'Removed ' + CAST(@total AS varchar(20)) + ' row(s).';
GO

SELECT
    RowsRemaining = COUNT(*),
    DataMB        = CAST(ISNULL(SUM(DATALENGTH(DataJson)), 0) / 1048576.0 AS decimal(10,1)),
    Oldest        = MIN([At]),
    Newest        = MAX([At])
FROM [AuditLogs];
GO
