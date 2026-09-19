-- ============================================================================================
--  Swarnakshi -- trim the audit trail.
--
--      psql -U cops_app -h localhost -d cops -v KeepMonths=24 -f 05-purge-audit.sql
--
--  WHY THIS EXISTS. audit_logs is append-only: every insert, edit and delete in the application
--  writes a row and nothing ever removes one. That is the correct design for a trail, and it
--  means the table only grows. PostgreSQL has no 10 GB cap the way SQL Server Express did, but a
--  table that grows without limit still fills a disk, and a full disk stops every write.
--
--  It deletes nothing recent. KeepMonths is required and must be at least 12: a trail shorter
--  than a financial year cannot answer the questions a trail is kept for.
--
--  Deletes in batches, so a large first run does not hold one enormous transaction and block
--  the application while it runs. Safe to stop and re-run; it simply carries on.
--
--  HOW MUCH IS IN THERE. Before deciding, look:
--
--      SELECT COUNT(*) AS rows,
--             pg_size_pretty(pg_total_relation_size('audit_logs')) AS size,
--             MIN(at) AS oldest, MAX(at) AS newest
--      FROM audit_logs;
-- ============================================================================================

\set ON_ERROR_STOP on

\if :{?KeepMonths}
\else
  \echo 'Pass how many months to keep:  -v KeepMonths=24'
  DO $$ BEGIN RAISE EXCEPTION 'Required argument missing - see the line above.'; END $$;
\endif

DO $$
DECLARE
    keep    int := :KeepMonths;
    cutoff  timestamptz;
    batch   int;
    total   bigint := 0;
BEGIN
    IF keep IS NULL OR keep < 12 THEN
        RAISE EXCEPTION 'KeepMonths must be a whole number of at least 12. A trail shorter than a financial year cannot answer the questions it is kept for.';
    END IF;

    cutoff := now() - make_interval(months => keep);
    RAISE NOTICE 'Deleting audit rows written before %', cutoff;

    LOOP
        -- ctid is the row's physical address: the cheapest way to delete "any 5000 of these".
        DELETE FROM audit_logs
        WHERE ctid IN (SELECT ctid FROM audit_logs WHERE at < cutoff LIMIT 5000);
        GET DIAGNOSTICS batch = ROW_COUNT;
        total := total + batch;
        EXIT WHEN batch = 0;
    END LOOP;

    RAISE NOTICE 'Removed % row(s).', total;
END $$;

-- The space is reclaimed by autovacuum in due course. VACUUM here would make it immediate but
-- takes a lock the application would notice; leaving it to autovacuum costs nothing but time.
SELECT
    COUNT(*)                                            AS rows_remaining,
    pg_size_pretty(pg_total_relation_size('audit_logs')) AS size,
    MIN(at)                                             AS oldest,
    MAX(at)                                             AS newest
FROM audit_logs;
