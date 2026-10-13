/*
    Swarnakshi - database upgrade.

    GENERATED FILE. Regenerate with
        powershell -File deploy\scripts\New-UpgradeScript.ps1 -From 20260919060724_InitialPostgres

    From: 20260919060724_InitialPostgres
    To:   the current build

    Run it against the LIVE database, as the application's role, before or during the deployment
    of the matching build:

        psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f 2026-10-13-own-pocket-expenses.sql

    ON_ERROR_STOP matters: without it psql carries on past a failed statement and a half-applied
    upgrade looks like a clean run. -1 wraps the whole file in one transaction, so it is all or
    nothing.

    Idempotent, and it agrees with the application. Every statement is wrapped in a check against
    __EFMigrationsHistory, so running it twice does nothing the second time - and if the API has
    already migrated itself on restart, this finds the work done and changes nothing. Running both
    is not a mistake; it is the intended belt and braces.

    Schema only. No master data: settings, expense heads, units and the material taxonomy are
    seeded by the application on first start, not here.

    Generated: 2026-10-13 18:30:29 from commit 1ecadf9
*/
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    ALTER TABLE project_expenses ADD paid_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    ALTER TABLE project_expenses ADD reimbursement_status integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE TABLE expense_settlements (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        paid_to_user_id uuid NOT NULL,
        date date NOT NULL,
        amount numeric(18,2) NOT NULL,
        payment_method_id uuid,
        reference character varying(100),
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
        CONSTRAINT pk_expense_settlements PRIMARY KEY (id),
        CONSTRAINT fk_expense_settlements_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id) ON DELETE RESTRICT,
        CONSTRAINT fk_expense_settlements_users_paid_to_user_id FOREIGN KEY (paid_to_user_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE TABLE expense_settlement_lines (
        id uuid NOT NULL,
        expense_settlement_id uuid NOT NULL,
        project_expense_id uuid NOT NULL,
        amount numeric(18,2) NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_expense_settlement_lines PRIMARY KEY (id),
        CONSTRAINT fk_expense_settlement_lines_expense_settlements_expense_settle FOREIGN KEY (expense_settlement_id) REFERENCES expense_settlements (id) ON DELETE CASCADE,
        CONSTRAINT fk_expense_settlement_lines_project_expenses_project_expense_id FOREIGN KEY (project_expense_id) REFERENCES project_expenses (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_project_expenses_paid_by_user_id ON project_expenses (paid_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX "IX_ProjectExpenses_Reimbursement" ON project_expenses (company_id, reimbursement_status, paid_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlement_lines_company_id ON expense_settlement_lines (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlement_lines_company_id_project_expense_id ON expense_settlement_lines (company_id, project_expense_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlement_lines_expense_settlement_id ON expense_settlement_lines (expense_settlement_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlement_lines_project_expense_id ON expense_settlement_lines (project_expense_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlements_company_id ON expense_settlements (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlements_company_id_paid_to_user_id_date ON expense_settlements (company_id, paid_to_user_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE UNIQUE INDEX ix_expense_settlements_company_id_txn_number ON expense_settlements (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlements_paid_to_user_id ON expense_settlements (paid_to_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    CREATE INDEX ix_expense_settlements_payment_method_id ON expense_settlements (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    ALTER TABLE project_expenses ADD CONSTRAINT fk_project_expenses_users_paid_by_user_id FOREIGN KEY (paid_by_user_id) REFERENCES users (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261013124900_OwnPocketExpenses') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261013124900_OwnPocketExpenses', '10.0.4');
    END IF;
END $EF$;
COMMIT;


