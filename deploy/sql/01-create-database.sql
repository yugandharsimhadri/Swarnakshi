-- ============================================================================================
--  Swarnakshi -- create the application database and the role that owns it.
--
--  Run once per server, as the postgres superuser, BEFORE the first deployment:
--
--      psql -U postgres -h localhost -v DbName="cops" -v AppRole="cops_app" -v AppPassword="<password>" -f 01-create-database.sql
--
--  All three are required and none has a default. The database name in particular:
--  the application will NOT create its own database - its role is deliberately not
--  CREATEDB - so a name here that does not match the connection string leaves the app
--  dying at startup with "permission denied", which says nothing about the mismatch that
--  caused it. Better to be asked than to silently create a database nobody will connect to.
--
--  Idempotent: safe to re-run. It never drops anything and never resets an existing
--  password - rotate a password with 02-rotate-password.sql instead.
--
--  The application role OWNS the database. That is what lets EF Core migrations create and
--  alter tables without the role being a superuser, and what stops it touching any other
--  database on the server. Lower-case names throughout: PostgreSQL folds unquoted
--  identifiers to lower case, and a mixed-case name would need quoting in every command
--  anybody ever typed against it.
-- ============================================================================================

\set ON_ERROR_STOP on

-- psql leaves an unset variable as the literal text ':DbName' - these catch it before it
-- becomes a database called ":DbName".
\if :{?DbName}
\else
  \echo 'Pass the database name with:  -v DbName="cops"'
  DO $$ BEGIN RAISE EXCEPTION 'Required argument missing - see the line above.'; END $$;
\endif
\if :{?AppRole}
\else
  \echo 'Pass the application role with:  -v AppRole="cops_app"'
  DO $$ BEGIN RAISE EXCEPTION 'Required argument missing - see the line above.'; END $$;
\endif
\if :{?AppPassword}
\else
  \echo 'Pass the application password with:  -v AppPassword="<password>"'
  DO $$ BEGIN RAISE EXCEPTION 'Required argument missing - see the line above.'; END $$;
\endif

-- ---------- 1. the role ----------
-- LOGIN, no CREATEDB, no SUPERUSER. It can do anything inside the database it owns and
-- nothing outside it.
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'AppRole', :'AppPassword')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'AppRole')
\gexec

-- ---------- 2. the database ----------
-- UTF-8, owned by the application role. CREATE DATABASE cannot run inside a transaction
-- block, which is why this file has no BEGIN/COMMIT around it.
SELECT format('CREATE DATABASE %I OWNER %I ENCODING ''UTF8'' TEMPLATE template0', :'DbName', :'AppRole')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'DbName')
\gexec

-- A database created earlier by somebody else still has to belong to the app role, or the
-- first migration fails on the first CREATE TABLE.
SELECT format('ALTER DATABASE %I OWNER TO %I', :'DbName', :'AppRole')
WHERE EXISTS (SELECT 1 FROM pg_database d JOIN pg_roles r ON r.oid = d.datdba
              WHERE d.datname = :'DbName' AND r.rolname <> :'AppRole')
\gexec

-- ---------- 3. inside the database: the public schema ----------
-- PostgreSQL 15+ no longer lets every role create in public. The owner of the database is
-- not automatically the owner of its public schema, so hand it over explicitly.
\connect :"DbName"

SELECT format('ALTER SCHEMA public OWNER TO %I', :'AppRole')
WHERE (SELECT nspowner::regrole::text FROM pg_namespace WHERE nspname = 'public') <> :'AppRole'
\gexec

-- ---------- 4. report ----------
SELECT
    current_database()                              AS database,
    pg_catalog.pg_get_userbyid(d.datdba)           AS owner,
    pg_catalog.pg_encoding_to_char(d.encoding)     AS encoding,
    d.datcollate                                    AS collation
FROM pg_database d WHERE d.datname = current_database();

\echo Done. :DbName is ready for the first deployment.
