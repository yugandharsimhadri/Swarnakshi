/*
    Swarnakshi - database upgrade.

    GENERATED FILE. Regenerate with
        powershell -File deploy\scripts\New-UpgradeScript.ps1 -From 20261013124900_OwnPocketExpenses

    From: 20261013124900_OwnPocketExpenses
    To:   the current build

    Run it against the LIVE database, as the application's role, before or during the deployment
    of the matching build:

        psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f 2026-10-25-customer-add-on-requests.sql

    ON_ERROR_STOP matters: without it psql carries on past a failed statement and a half-applied
    upgrade looks like a clean run. -1 wraps the whole file in one transaction, so it is all or
    nothing.

    Idempotent, and it agrees with the application. Every statement is wrapped in a check against
    __EFMigrationsHistory, so running it twice does nothing the second time - and if the API has
    already migrated itself on restart, this finds the work done and changes nothing. Running both
    is not a mistake; it is the intended belt and braces.

    Schema only. No master data: settings, expense heads, units and the material taxonomy are
    seeded by the application on first start, not here.

    Generated: 2026-10-25 22:14:28 from commit 82b779b
*/
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025162805_CustomerAddOnRequests') THEN
    CREATE TABLE add_on_requests (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        project_id uuid NOT NULL,
        date date NOT NULL,
        expense_head_id uuid NOT NULL,
        description character varying(1000) NOT NULL,
        expected_amount numeric(18,2) NOT NULL,
        status integer NOT NULL,
        remarks character varying(512),
        modified_at timestamp with time zone,
        modified_by uuid,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_add_on_requests PRIMARY KEY (id),
        CONSTRAINT fk_add_on_requests_expense_heads_expense_head_id FOREIGN KEY (expense_head_id) REFERENCES expense_heads (id) ON DELETE RESTRICT,
        CONSTRAINT fk_add_on_requests_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025162805_CustomerAddOnRequests') THEN
    CREATE INDEX ix_add_on_requests_company_id ON add_on_requests (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025162805_CustomerAddOnRequests') THEN
    CREATE UNIQUE INDEX ix_add_on_requests_company_id_txn_number ON add_on_requests (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025162805_CustomerAddOnRequests') THEN
    CREATE INDEX ix_add_on_requests_expense_head_id ON add_on_requests (expense_head_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025162805_CustomerAddOnRequests') THEN
    CREATE INDEX ix_add_on_requests_project_id_status ON add_on_requests (project_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025162805_CustomerAddOnRequests') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261025162805_CustomerAddOnRequests', '10.0.4');
    END IF;
END $EF$;
COMMIT;


