-- ============================================================================================
--  Swarnakshi -- change the application role's password.
--
--      psql -U postgres -h localhost -v AppRole="cops_app" -v NewPassword="<new password>" -f 02-rotate-password.sql
--
--  Then put the same password in appsettings.Production.json and restart the API. Do the two
--  within a minute of each other: between them the application cannot open a connection, and
--  a request that arrives in that minute fails.
--
--  Order matters less than it looks. The API's pooled connections were authenticated when they
--  opened and keep working until they are recycled, so changing the password here first does
--  not drop the site instantly - it stops NEW connections, which is what the restart provides.
-- ============================================================================================

\set ON_ERROR_STOP on

\if :{?AppRole}
\else
  \echo 'Pass the role with:  -v AppRole="cops_app"'
  DO $$ BEGIN RAISE EXCEPTION 'Required argument missing - see the line above.'; END $$;
\endif
\if :{?NewPassword}
\else
  \echo 'Pass the new password with:  -v NewPassword="<password>"'
  DO $$ BEGIN RAISE EXCEPTION 'Required argument missing - see the line above.'; END $$;
\endif

SELECT format('ALTER ROLE %I PASSWORD %L', :'AppRole', :'NewPassword')
WHERE EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'AppRole')
\gexec

\echo Password changed for :AppRole. Update appsettings.Production.json and restart the API.
