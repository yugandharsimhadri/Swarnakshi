/*
    Swarnakshi - complete database schema.

    GENERATED FILE. Do not edit by hand: regenerate with
        powershell -File deploy\scripts\New-SchemaScript.ps1
    after adding an EF migration, or your edit is lost on the next build.

    Run it against a database that already exists (create it with 01-create-database.sql):

        psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f 03-schema.sql

    Idempotent. Every migration is wrapped in a check against __EFMigrationsHistory, so running
    this twice does nothing the second time, and running it against a partly-migrated database
    applies only what is missing.

    Applying this by hand is optional. Deploy.ps1 applies the same migrations itself through
    Swarnakshi.Api.exe --migrate, and finding the work already done it simply reports the schema is
    up to date. Doing it here is for sites where only a DBA may change the schema - and it means
    the application login never needs CREATE TABLE or ALTER at all.

    It creates tables, indexes and foreign keys. It does NOT create master data: the platform
    operator, the founding company, expense heads, units and the material taxonomy are seeded in
    application code the first time the service starts, not here.

    Generated: 2026-09-19 12:05:49 from commit b2f9375
*/
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE approval_requests (
        id uuid NOT NULL,
        entity_type character varying(512) NOT NULL,
        entity_id uuid NOT NULL,
        entity_ref character varying(512),
        site_id uuid,
        project_id uuid,
        amount numeric(18,2),
        current_status integer NOT NULL,
        requested_by_user_id uuid NOT NULL,
        requested_at timestamp with time zone NOT NULL,
        decided_by_user_id uuid,
        decided_at timestamp with time zone,
        remarks character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_approval_requests PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE attachments (
        id uuid NOT NULL,
        entity_type character varying(512) NOT NULL,
        entity_id uuid NOT NULL,
        file_name character varying(512) NOT NULL,
        content_type character varying(512) NOT NULL,
        size bigint NOT NULL,
        storage_path character varying(512) NOT NULL,
        uploaded_by_user_id uuid,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_attachments PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE audit_logs (
        id uuid NOT NULL,
        entity_type character varying(100) NOT NULL,
        entity_id uuid NOT NULL,
        action character varying(400) NOT NULL,
        data_json text,
        user_id uuid,
        at timestamp with time zone NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_audit_logs PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE companies (
        id uuid NOT NULL,
        code character varying(30) NOT NULL,
        name character varying(200) NOT NULL,
        contact_email character varying(512),
        contact_mobile character varying(512),
        license_expires_on date NOT NULL,
        is_active boolean NOT NULL,
        notes character varying(512),
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_companies PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE contractors (
        id uuid NOT NULL,
        code character varying(512) NOT NULL,
        name character varying(512) NOT NULL,
        company_name character varying(512),
        mobile character varying(512),
        email character varying(512),
        address character varying(512),
        pan character varying(512),
        gstin character varying(512),
        bank_details character varying(512),
        contractor_type character varying(512),
        is_active boolean NOT NULL,
        notes character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_contractors PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE customers (
        id uuid NOT NULL,
        code character varying(512) NOT NULL,
        name character varying(512) NOT NULL,
        mobile character varying(512),
        email character varying(512),
        address character varying(512),
        pan character varying(512),
        gstin character varying(512),
        is_active boolean NOT NULL,
        notes character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_customers PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE expense_heads (
        id uuid NOT NULL,
        name character varying(512) NOT NULL,
        sort_order integer NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_expense_heads PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE labour_categories (
        id uuid NOT NULL,
        name character varying(512) NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_labour_categories PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE material_categories (
        id uuid NOT NULL,
        name character varying(512) NOT NULL,
        sort_order integer NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_material_categories PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE payment_methods (
        id uuid NOT NULL,
        name character varying(512) NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_payment_methods PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE platform_users (
        id uuid NOT NULL,
        username character varying(60) NOT NULL,
        display_name character varying(200) NOT NULL,
        password_hash character varying(512) NOT NULL,
        is_active boolean NOT NULL,
        refresh_token character varying(512),
        refresh_token_expiry timestamp with time zone,
        last_login_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_platform_users PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE project_types (
        id uuid NOT NULL,
        name character varying(512) NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_project_types PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE suppliers (
        id uuid NOT NULL,
        code character varying(512) NOT NULL,
        name character varying(512) NOT NULL,
        mobile character varying(512),
        email character varying(512),
        address character varying(512),
        pan character varying(512),
        gstin character varying(512),
        is_active boolean NOT NULL,
        notes character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_suppliers PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE transaction_sequences (
        id uuid NOT NULL,
        prefix character varying(512) NOT NULL,
        year integer NOT NULL,
        last_number integer NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_transaction_sequences PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE units (
        id uuid NOT NULL,
        code character varying(20) NOT NULL,
        name character varying(512) NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_units PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        username character varying(60) NOT NULL,
        email character varying(256),
        mobile character varying(20),
        password_hash character varying(512) NOT NULL,
        role integer NOT NULL,
        is_active boolean NOT NULL,
        is_company_admin boolean NOT NULL,
        refresh_token character varying(512),
        refresh_token_expiry timestamp with time zone,
        tokens_valid_from timestamp with time zone,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_users PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE approval_histories (
        id uuid NOT NULL,
        approval_request_id uuid NOT NULL,
        action integer NOT NULL,
        previous_status integer NOT NULL,
        new_status integer NOT NULL,
        user_id uuid NOT NULL,
        at timestamp with time zone NOT NULL,
        remarks character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_approval_histories PRIMARY KEY (id),
        CONSTRAINT fk_approval_histories_approval_requests_approval_request_id FOREIGN KEY (approval_request_id) REFERENCES approval_requests (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE expense_subheads (
        id uuid NOT NULL,
        expense_head_id uuid NOT NULL,
        name character varying(512) NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_expense_subheads PRIMARY KEY (id),
        CONSTRAINT fk_expense_subheads_expense_heads_expense_head_id FOREIGN KEY (expense_head_id) REFERENCES expense_heads (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE material_subcategories (
        id uuid NOT NULL,
        material_category_id uuid NOT NULL,
        name character varying(512) NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_material_subcategories PRIMARY KEY (id),
        CONSTRAINT fk_material_subcategories_material_categories_material_categor FOREIGN KEY (material_category_id) REFERENCES material_categories (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE sites (
        id uuid NOT NULL,
        code character varying(30) NOT NULL,
        name character varying(512) NOT NULL,
        address character varying(512),
        city character varying(512),
        state character varying(512),
        pin character varying(512),
        supervisor_user_id uuid,
        start_date date,
        status integer NOT NULL,
        notes character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_sites PRIMARY KEY (id),
        CONSTRAINT fk_sites_users_supervisor_user_id FOREIGN KEY (supervisor_user_id) REFERENCES users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE user_permissions (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        permission_key character varying(512) NOT NULL,
        granted boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_user_permissions PRIMARY KEY (id),
        CONSTRAINT fk_user_permissions_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE material_spec_definitions (
        id uuid NOT NULL,
        material_subcategory_id uuid NOT NULL,
        key character varying(60) NOT NULL,
        label character varying(120) NOT NULL,
        kind integer NOT NULL,
        options character varying(600),
        is_required boolean NOT NULL,
        part_of_identity boolean NOT NULL,
        sort_order integer NOT NULL,
        is_active boolean NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_material_spec_definitions PRIMARY KEY (id),
        CONSTRAINT fk_material_spec_definitions_material_subcategories_material_s FOREIGN KEY (material_subcategory_id) REFERENCES material_subcategories (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE materials (
        id uuid NOT NULL,
        code character varying(40) NOT NULL,
        name character varying(512) NOT NULL,
        material_subcategory_id uuid NOT NULL,
        brand character varying(120),
        description character varying(512),
        unit_id uuid NOT NULL,
        secondary_unit_id uuid,
        conversion_factor numeric(18,2),
        generic_measurement character varying(120),
        min_stock_level numeric(18,2) NOT NULL,
        reorder_level numeric(18,2) NOT NULL,
        default_purchase_rate numeric(18,2) NOT NULL,
        gst_rate numeric(18,2),
        is_active boolean NOT NULL,
        notes character varying(512),
        spec_summary character varying(400),
        spec_signature character varying(500) NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_materials PRIMARY KEY (id),
        CONSTRAINT fk_materials_material_subcategories_material_subcategory_id FOREIGN KEY (material_subcategory_id) REFERENCES material_subcategories (id) ON DELETE RESTRICT,
        CONSTRAINT fk_materials_units_secondary_unit_id FOREIGN KEY (secondary_unit_id) REFERENCES units (id) ON DELETE RESTRICT,
        CONSTRAINT fk_materials_units_unit_id FOREIGN KEY (unit_id) REFERENCES units (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE employees (
        id uuid NOT NULL,
        code character varying(40) NOT NULL,
        name character varying(200) NOT NULL,
        phone character varying(20) NOT NULL,
        monthly_salary numeric(18,2) NOT NULL,
        join_date date NOT NULL,
        leave_date date,
        designation character varying(120),
        address character varying(512),
        notes character varying(512),
        is_active boolean NOT NULL,
        site_id uuid,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_employees PRIMARY KEY (id),
        CONSTRAINT fk_employees_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE projects (
        id uuid NOT NULL,
        code character varying(30) NOT NULL,
        name character varying(512) NOT NULL,
        villa_number character varying(512),
        site_id uuid NOT NULL,
        customer_id uuid,
        project_type_id uuid,
        address character varying(512),
        start_date date,
        expected_completion_date date,
        actual_completion_date date,
        estimated_cost numeric(18,2) NOT NULL,
        contract_sale_value numeric(18,2),
        status integer NOT NULL,
        completion_percent integer NOT NULL,
        notes character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_projects PRIMARY KEY (id),
        CONSTRAINT fk_projects_customers_customer_id FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT,
        CONSTRAINT fk_projects_project_types_project_type_id FOREIGN KEY (project_type_id) REFERENCES project_types (id) ON DELETE SET NULL,
        CONSTRAINT fk_projects_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE settings (
        id uuid NOT NULL,
        key character varying(512) NOT NULL,
        value character varying(512) NOT NULL,
        site_id uuid,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_settings PRIMARY KEY (id),
        CONSTRAINT fk_settings_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE site_expenses (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        site_id uuid NOT NULL,
        date date NOT NULL,
        expense_head_id uuid NOT NULL,
        description character varying(512),
        amount numeric(18,2) NOT NULL,
        payment_status integer NOT NULL,
        payment_method_id uuid,
        source_type character varying(512),
        source_id uuid,
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
        CONSTRAINT pk_site_expenses PRIMARY KEY (id),
        CONSTRAINT fk_site_expenses_expense_heads_expense_head_id FOREIGN KEY (expense_head_id) REFERENCES expense_heads (id) ON DELETE RESTRICT,
        CONSTRAINT fk_site_expenses_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id) ON DELETE RESTRICT,
        CONSTRAINT fk_site_expenses_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE user_site_assignments (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        site_id uuid NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_user_site_assignments PRIMARY KEY (id),
        CONSTRAINT fk_user_site_assignments_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE CASCADE,
        CONSTRAINT fk_user_site_assignments_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE inventory_balances (
        id uuid NOT NULL,
        site_id uuid NOT NULL,
        material_id uuid NOT NULL,
        quantity numeric(18,2) NOT NULL,
        average_rate numeric(18,2) NOT NULL,
        value numeric(18,2) NOT NULL,
        last_movement_at timestamp with time zone,
        last_purchase_rate numeric(18,2),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_inventory_balances PRIMARY KEY (id),
        CONSTRAINT fk_inventory_balances_materials_material_id FOREIGN KEY (material_id) REFERENCES materials (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_balances_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE material_spec_values (
        id uuid NOT NULL,
        material_id uuid NOT NULL,
        material_spec_definition_id uuid NOT NULL,
        value character varying(200) NOT NULL,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_material_spec_values PRIMARY KEY (id),
        CONSTRAINT fk_material_spec_values_material_spec_definitions_material_spe FOREIGN KEY (material_spec_definition_id) REFERENCES material_spec_definitions (id) ON DELETE RESTRICT,
        CONSTRAINT fk_material_spec_values_materials_material_id FOREIGN KEY (material_id) REFERENCES materials (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE contract_works (
        id uuid NOT NULL,
        project_id uuid NOT NULL,
        contractor_id uuid NOT NULL,
        work_category character varying(512) NOT NULL,
        description character varying(512),
        contract_amount numeric(18,2) NOT NULL,
        start_date date,
        expected_completion date,
        actual_completion date,
        payment_terms character varying(512),
        work_status integer NOT NULL,
        total_paid numeric(18,2) NOT NULL,
        balance numeric(18,2) NOT NULL,
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
        CONSTRAINT pk_contract_works PRIMARY KEY (id),
        CONSTRAINT fk_contract_works_contractors_contractor_id FOREIGN KEY (contractor_id) REFERENCES contractors (id) ON DELETE RESTRICT,
        CONSTRAINT fk_contract_works_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE customer_payments (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        project_id uuid NOT NULL,
        customer_id uuid NOT NULL,
        date date NOT NULL,
        amount numeric(18,2) NOT NULL,
        payment_method_id uuid NOT NULL,
        reference character varying(512),
        description character varying(512),
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
        CONSTRAINT pk_customer_payments PRIMARY KEY (id),
        CONSTRAINT fk_customer_payments_customers_customer_id FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT,
        CONSTRAINT fk_customer_payments_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id) ON DELETE RESTRICT,
        CONSTRAINT fk_customer_payments_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE employee_payments (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        employee_id uuid NOT NULL,
        date date NOT NULL,
        kind integer NOT NULL,
        amount numeric(18,2) NOT NULL,
        advance_recovered numeric(18,2) NOT NULL,
        period_start date,
        period_end date,
        payment_method_id uuid,
        reference character varying(512),
        project_id uuid,
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
        CONSTRAINT pk_employee_payments PRIMARY KEY (id),
        CONSTRAINT fk_employee_payments_employees_employee_id FOREIGN KEY (employee_id) REFERENCES employees (id) ON DELETE RESTRICT,
        CONSTRAINT fk_employee_payments_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id) ON DELETE RESTRICT,
        CONSTRAINT fk_employee_payments_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE inventory_transactions (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        date date NOT NULL,
        site_id uuid NOT NULL,
        material_id uuid NOT NULL,
        unit_id uuid NOT NULL,
        quantity numeric(18,2) NOT NULL,
        rate numeric(18,2) NOT NULL,
        amount numeric(18,2) NOT NULL,
        type integer NOT NULL,
        project_id uuid,
        source_type character varying(512),
        source_id uuid,
        source_ref character varying(512),
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
        CONSTRAINT pk_inventory_transactions PRIMARY KEY (id),
        CONSTRAINT fk_inventory_transactions_materials_material_id FOREIGN KEY (material_id) REFERENCES materials (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_transactions_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_transactions_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_transactions_units_unit_id FOREIGN KEY (unit_id) REFERENCES units (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE labour_entries (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        project_id uuid NOT NULL,
        labour_category_id uuid NOT NULL,
        period_type integer NOT NULL,
        period_start date NOT NULL,
        period_end date NOT NULL,
        amount numeric(18,2) NOT NULL,
        payment_method_id uuid,
        payment_type character varying(512),
        remarks character varying(512),
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        modified_at timestamp with time zone,
        modified_by uuid,
        approved_at timestamp with time zone,
        approved_by uuid,
        status integer NOT NULL,
        concurrency_token uuid NOT NULL,
        CONSTRAINT pk_labour_entries PRIMARY KEY (id),
        CONSTRAINT fk_labour_entries_labour_categories_labour_category_id FOREIGN KEY (labour_category_id) REFERENCES labour_categories (id) ON DELETE RESTRICT,
        CONSTRAINT fk_labour_entries_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id) ON DELETE RESTRICT,
        CONSTRAINT fk_labour_entries_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE material_requests (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        site_id uuid NOT NULL,
        project_id uuid NOT NULL,
        request_type integer NOT NULL,
        request_status integer NOT NULL,
        requested_by_user_id uuid NOT NULL,
        date date NOT NULL,
        notes character varying(512),
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
        CONSTRAINT pk_material_requests PRIMARY KEY (id),
        CONSTRAINT fk_material_requests_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT,
        CONSTRAINT fk_material_requests_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE project_expenses (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        project_id uuid NOT NULL,
        date date NOT NULL,
        expense_head_id uuid NOT NULL,
        expense_subhead_id uuid,
        description character varying(512),
        amount numeric(18,2) NOT NULL,
        expense_type integer NOT NULL,
        payment_status integer NOT NULL,
        payment_method_id uuid,
        source_type character varying(512),
        source_id uuid,
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
        CONSTRAINT pk_project_expenses PRIMARY KEY (id),
        CONSTRAINT fk_project_expenses_expense_heads_expense_head_id FOREIGN KEY (expense_head_id) REFERENCES expense_heads (id) ON DELETE RESTRICT,
        CONSTRAINT fk_project_expenses_expense_subheads_expense_subhead_id FOREIGN KEY (expense_subhead_id) REFERENCES expense_subheads (id) ON DELETE RESTRICT,
        CONSTRAINT fk_project_expenses_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id) ON DELETE RESTRICT,
        CONSTRAINT fk_project_expenses_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE contractor_payments (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        contractor_id uuid NOT NULL,
        project_id uuid NOT NULL,
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
        CONSTRAINT pk_contractor_payments PRIMARY KEY (id),
        CONSTRAINT fk_contractor_payments_contract_works_contract_work_id FOREIGN KEY (contract_work_id) REFERENCES contract_works (id) ON DELETE RESTRICT,
        CONSTRAINT fk_contractor_payments_contractors_contractor_id FOREIGN KEY (contractor_id) REFERENCES contractors (id) ON DELETE RESTRICT,
        CONSTRAINT fk_contractor_payments_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id) ON DELETE RESTRICT,
        CONSTRAINT fk_contractor_payments_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE material_request_items (
        id uuid NOT NULL,
        material_request_id uuid NOT NULL,
        material_id uuid NOT NULL,
        unit_id uuid NOT NULL,
        requested_qty numeric(18,2) NOT NULL,
        approved_qty numeric(18,2),
        issued_qty numeric(18,2) NOT NULL,
        rate numeric(18,2),
        expense_head_id uuid,
        expense_subhead_id uuid,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_material_request_items PRIMARY KEY (id),
        CONSTRAINT fk_material_request_items_expense_heads_expense_head_id FOREIGN KEY (expense_head_id) REFERENCES expense_heads (id) ON DELETE RESTRICT,
        CONSTRAINT fk_material_request_items_expense_subheads_expense_subhead_id FOREIGN KEY (expense_subhead_id) REFERENCES expense_subheads (id) ON DELETE RESTRICT,
        CONSTRAINT fk_material_request_items_material_requests_material_request_id FOREIGN KEY (material_request_id) REFERENCES material_requests (id) ON DELETE CASCADE,
        CONSTRAINT fk_material_request_items_materials_material_id FOREIGN KEY (material_id) REFERENCES materials (id) ON DELETE RESTRICT,
        CONSTRAINT fk_material_request_items_units_unit_id FOREIGN KEY (unit_id) REFERENCES units (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE purchase_headers (
        id uuid NOT NULL,
        txn_number character varying(512) NOT NULL,
        supplier_id uuid NOT NULL,
        site_id uuid NOT NULL,
        project_id uuid,
        material_request_id uuid,
        invoice_number character varying(512),
        invoice_date date,
        date date NOT NULL,
        sub_total numeric(18,2) NOT NULL,
        discount numeric(18,2) NOT NULL,
        tax_amount numeric(18,2) NOT NULL,
        other_charges numeric(18,2) NOT NULL,
        total_amount numeric(18,2) NOT NULL,
        paid_amount numeric(18,2) NOT NULL,
        balance_amount numeric(18,2) NOT NULL,
        payment_status integer NOT NULL,
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
        CONSTRAINT pk_purchase_headers PRIMARY KEY (id),
        CONSTRAINT fk_purchase_headers_material_requests_material_request_id FOREIGN KEY (material_request_id) REFERENCES material_requests (id) ON DELETE RESTRICT,
        CONSTRAINT fk_purchase_headers_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE RESTRICT,
        CONSTRAINT fk_purchase_headers_sites_site_id FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE RESTRICT,
        CONSTRAINT fk_purchase_headers_suppliers_supplier_id FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE purchase_items (
        id uuid NOT NULL,
        purchase_header_id uuid NOT NULL,
        material_id uuid NOT NULL,
        unit_id uuid NOT NULL,
        quantity numeric(18,2) NOT NULL,
        rate numeric(18,2) NOT NULL,
        discount numeric(18,2) NOT NULL,
        tax_amount numeric(18,2) NOT NULL,
        line_total numeric(18,2) NOT NULL,
        deliver_to_project_id uuid,
        expense_head_id uuid,
        company_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        is_demo boolean NOT NULL,
        CONSTRAINT pk_purchase_items PRIMARY KEY (id),
        CONSTRAINT fk_purchase_items_expense_heads_expense_head_id FOREIGN KEY (expense_head_id) REFERENCES expense_heads (id) ON DELETE RESTRICT,
        CONSTRAINT fk_purchase_items_materials_material_id FOREIGN KEY (material_id) REFERENCES materials (id) ON DELETE RESTRICT,
        CONSTRAINT fk_purchase_items_projects_deliver_to_project_id FOREIGN KEY (deliver_to_project_id) REFERENCES projects (id) ON DELETE RESTRICT,
        CONSTRAINT fk_purchase_items_purchase_headers_purchase_header_id FOREIGN KEY (purchase_header_id) REFERENCES purchase_headers (id) ON DELETE CASCADE,
        CONSTRAINT fk_purchase_items_units_unit_id FOREIGN KEY (unit_id) REFERENCES units (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE TABLE supplier_payments (
        id uuid NOT NULL,
        purchase_header_id uuid NOT NULL,
        date date NOT NULL,
        amount numeric(18,2) NOT NULL,
        payment_method_id uuid,
        reference character varying(512),
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
        CONSTRAINT pk_supplier_payments PRIMARY KEY (id),
        CONSTRAINT fk_supplier_payments_payment_methods_payment_method_id FOREIGN KEY (payment_method_id) REFERENCES payment_methods (id),
        CONSTRAINT fk_supplier_payments_purchase_headers_purchase_header_id FOREIGN KEY (purchase_header_id) REFERENCES purchase_headers (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_approval_histories_approval_request_id ON approval_histories (approval_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_approval_histories_company_id ON approval_histories (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_approval_requests_company_id ON approval_requests (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_approval_requests_current_status ON approval_requests (current_status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_approval_requests_entity_type_entity_id ON approval_requests (entity_type, entity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_attachments_company_id ON attachments (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_attachments_entity_type_entity_id ON attachments (entity_type, entity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_audit_logs_company_id ON audit_logs (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX "IX_AuditLogs_Entity" ON audit_logs (company_id, entity_type, entity_id, at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX "IX_AuditLogs_When" ON audit_logs (company_id, at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_companies_code ON companies (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_companies_name ON companies (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contract_works_company_id ON contract_works (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contract_works_contractor_id ON contract_works (contractor_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contract_works_project_id ON contract_works (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contractor_payments_company_id ON contractor_payments (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_contractor_payments_company_id_txn_number ON contractor_payments (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contractor_payments_contract_work_id ON contractor_payments (contract_work_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contractor_payments_contractor_id ON contractor_payments (contractor_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contractor_payments_payment_method_id ON contractor_payments (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contractor_payments_project_id ON contractor_payments (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_contractors_company_id ON contractors (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_contractors_company_id_code ON contractors (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_customer_payments_company_id ON customer_payments (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_customer_payments_company_id_txn_number ON customer_payments (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_customer_payments_customer_id ON customer_payments (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_customer_payments_payment_method_id ON customer_payments (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_customer_payments_project_id ON customer_payments (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_customers_company_id ON customers (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_customers_company_id_code ON customers (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_employee_payments_company_id ON employee_payments (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_employee_payments_company_id_txn_number ON employee_payments (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_employee_payments_employee_id_date ON employee_payments (employee_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_employee_payments_payment_method_id ON employee_payments (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_employee_payments_project_id ON employee_payments (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_employees_company_id ON employees (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_employees_company_id_code ON employees (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_employees_company_id_phone ON employees (company_id, phone);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_employees_site_id ON employees (site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_expense_heads_company_id ON expense_heads (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_expense_subheads_company_id ON expense_subheads (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_expense_subheads_company_id_expense_head_id_name ON expense_subheads (company_id, expense_head_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_expense_subheads_expense_head_id ON expense_subheads (expense_head_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_balances_company_id ON inventory_balances (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_inventory_balances_company_id_site_id_material_id ON inventory_balances (company_id, site_id, material_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_balances_material_id ON inventory_balances (material_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_balances_site_id ON inventory_balances (site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_transactions_company_id ON inventory_transactions (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_inventory_transactions_company_id_txn_number ON inventory_transactions (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_transactions_material_id ON inventory_transactions (material_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_transactions_project_id ON inventory_transactions (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_transactions_site_id_material_id_date ON inventory_transactions (site_id, material_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_inventory_transactions_unit_id ON inventory_transactions (unit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX "IX_InventoryTransactions_CompanyId_SiteId_Type_Date" ON inventory_transactions (company_id, site_id, type, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_labour_categories_company_id ON labour_categories (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_labour_entries_company_id ON labour_entries (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_labour_entries_company_id_txn_number ON labour_entries (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_labour_entries_labour_category_id ON labour_entries (labour_category_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_labour_entries_payment_method_id ON labour_entries (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_labour_entries_project_id ON labour_entries (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_categories_company_id ON material_categories (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_request_items_company_id ON material_request_items (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_request_items_expense_head_id ON material_request_items (expense_head_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_request_items_expense_subhead_id ON material_request_items (expense_subhead_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_request_items_material_id ON material_request_items (material_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_request_items_material_request_id ON material_request_items (material_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_request_items_unit_id ON material_request_items (unit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_requests_company_id ON material_requests (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_material_requests_company_id_txn_number ON material_requests (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_requests_project_id ON material_requests (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_requests_site_id ON material_requests (site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_spec_definitions_company_id ON material_spec_definitions (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_material_spec_definitions_company_id_material_subcategory_i ON material_spec_definitions (company_id, material_subcategory_id, key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_spec_definitions_material_subcategory_id ON material_spec_definitions (material_subcategory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_spec_values_company_id ON material_spec_values (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_material_spec_values_company_id_material_id_material_spec_d ON material_spec_values (company_id, material_id, material_spec_definition_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_spec_values_material_id ON material_spec_values (material_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_spec_values_material_spec_definition_id ON material_spec_values (material_spec_definition_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_spec_values_value ON material_spec_values (value);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_subcategories_company_id ON material_subcategories (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_material_subcategories_company_id_material_category_id_name ON material_subcategories (company_id, material_category_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_material_subcategories_material_category_id ON material_subcategories (material_category_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_materials_brand ON materials (brand);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_materials_company_id ON materials (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_materials_company_id_code ON materials (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_materials_company_id_spec_signature ON materials (company_id, spec_signature);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_materials_is_active ON materials (is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_materials_material_subcategory_id ON materials (material_subcategory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_materials_secondary_unit_id ON materials (secondary_unit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_materials_unit_id ON materials (unit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_payment_methods_company_id ON payment_methods (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_platform_users_username ON platform_users (username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_project_expenses_company_id ON project_expenses (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_project_expenses_company_id_txn_number ON project_expenses (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_project_expenses_expense_head_id ON project_expenses (expense_head_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_project_expenses_expense_subhead_id ON project_expenses (expense_subhead_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_project_expenses_payment_method_id ON project_expenses (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_project_expenses_project_id_date ON project_expenses (project_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX "IX_ProjectExpenses_CompanyId_Status_Covering" ON project_expenses (company_id, status, project_id) INCLUDE (expense_type, amount, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_project_types_company_id ON project_types (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_projects_company_id ON projects (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_projects_company_id_code ON projects (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_projects_customer_id ON projects (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_projects_project_type_id ON projects (project_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_projects_site_id ON projects (site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_headers_company_id ON purchase_headers (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_purchase_headers_company_id_txn_number ON purchase_headers (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_headers_material_request_id ON purchase_headers (material_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_headers_project_id ON purchase_headers (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_headers_site_id ON purchase_headers (site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_headers_supplier_id ON purchase_headers (supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_items_company_id ON purchase_items (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_items_deliver_to_project_id ON purchase_items (deliver_to_project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_items_expense_head_id ON purchase_items (expense_head_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_items_material_id ON purchase_items (material_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_items_purchase_header_id ON purchase_items (purchase_header_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_purchase_items_unit_id ON purchase_items (unit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_settings_company_id ON settings (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_settings_company_id_key_site_id ON settings (company_id, key, site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_settings_site_id ON settings (site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_site_expenses_company_id ON site_expenses (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_site_expenses_company_id_txn_number ON site_expenses (company_id, txn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_site_expenses_expense_head_id ON site_expenses (expense_head_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_site_expenses_payment_method_id ON site_expenses (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_site_expenses_site_id_date ON site_expenses (site_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_sites_company_id ON sites (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_sites_company_id_code ON sites (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_sites_supervisor_user_id ON sites (supervisor_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_supplier_payments_company_id ON supplier_payments (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_supplier_payments_payment_method_id ON supplier_payments (payment_method_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_supplier_payments_purchase_header_id ON supplier_payments (purchase_header_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_suppliers_company_id ON suppliers (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_suppliers_company_id_code ON suppliers (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_transaction_sequences_company_id ON transaction_sequences (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_transaction_sequences_company_id_prefix_year ON transaction_sequences (company_id, prefix, year);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_units_company_id ON units (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_units_company_id_code ON units (company_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_user_permissions_company_id ON user_permissions (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_user_permissions_user_id ON user_permissions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_user_site_assignments_company_id ON user_site_assignments (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_user_site_assignments_site_id ON user_site_assignments (site_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_user_site_assignments_user_id ON user_site_assignments (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_users_company_id ON users (company_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE UNIQUE INDEX ix_users_company_id_username ON users (company_id, username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    CREATE INDEX ix_users_mobile ON users (mobile);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260919060724_InitialPostgres') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260919060724_InitialPostgres', '10.0.4');
    END IF;
END $EF$;
COMMIT;


