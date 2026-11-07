/*
    Swarnakshi - database upgrade.

    GENERATED FILE. Regenerate with
        powershell -File deploy\scripts\New-UpgradeScript.ps1 -From 20261025162805_CustomerAddOnRequests

    From: 20261025162805_CustomerAddOnRequests
    To:   the current build

    Run it against the LIVE database, as the application's role, before or during the deployment
    of the matching build:

        psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f 2026-11-07-contractor-payment-changes.sql

    ON_ERROR_STOP matters: without it psql carries on past a failed statement and a half-applied
    upgrade looks like a clean run. -1 wraps the whole file in one transaction, so it is all or
    nothing.

    Idempotent, and it agrees with the application. Every statement is wrapped in a check against
    __EFMigrationsHistory, so running it twice does nothing the second time - and if the API has
    already migrated itself on restart, this finds the work done and changes nothing. Running both
    is not a mistake; it is the intended belt and braces.

    Schema only. No master data: settings, expense heads, units and the material taxonomy are
    seeded by the application on first start, not here.

    Generated: 2026-11-07 13:45:31 from commit 67af34d
*/
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261107074105_ContractorPaymentChanges') THEN
    CREATE TABLE contractor_payment_changes (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        contractor_payment_id uuid NOT NULL,
        kind integer NOT NULL,
        reason character varying(500) NOT NULL,
        previous_amount numeric(18,2) NOT NULL,
        previous_date date NOT NULL,
        contract_work_id uuid,
        date date NOT NULL,
        amount numeric(18,2) NOT NULL,
        payment_method_id uuid NOT NULL,
        reference_number character varying(512),
        description character varying(512),
        payment_kind integer NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        modified_at timestamp with time zone,
        modified_by uuid,
        approved_at timestamp with time zone,
        approved_by uuid,
        status integer NOT NULL,
        remarks character varying(512),
        concurrency_token uuid NOT NULL,
        CONSTRAINT pk_contractor_payment_changes PRIMARY KEY (id),
        CONSTRAINT fk_contractor_payment_changes_contractor_payments_contractor_p FOREIGN KEY (contractor_payment_id) REFERENCES contractor_payments (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261107074105_ContractorPaymentChanges') THEN
    CREATE INDEX ix_contractor_payment_changes_company_id ON contractor_payment_changes (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261107074105_ContractorPaymentChanges') THEN
    CREATE UNIQUE INDEX ix_contractor_payment_changes_company_id_txn_number ON contractor_payment_changes (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261107074105_ContractorPaymentChanges') THEN
    CREATE INDEX ix_contractor_payment_changes_contractor_payment_id_status ON contractor_payment_changes (contractor_payment_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261107074105_ContractorPaymentChanges') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261107074105_ContractorPaymentChanges', '10.0.4');
    END IF;
END $EF$;
COMMIT;


