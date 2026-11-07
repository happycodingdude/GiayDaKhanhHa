CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE TABLE orders (
        id uuid NOT NULL,
        order_code character varying(50) NOT NULL,
        quantity integer NOT NULL,
        start_date date NOT NULL,
        due_date date NOT NULL,
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_orders" PRIMARY KEY (id),
        CONSTRAINT ck_orders_date_range CHECK (start_date <= due_date),
        CONSTRAINT ck_orders_quantity_positive CHECK (quantity > 0),
        CONSTRAINT ck_orders_status CHECK (status IN ('Incomplete', 'Completed'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        username character varying(100) NOT NULL,
        password_hash character varying(255) NOT NULL,
        display_name character varying(100) NOT NULL,
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY (id),
        CONSTRAINT ck_users_status CHECK (status IN ('Active', 'Inactive'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE TABLE production_plans (
        id uuid NOT NULL,
        order_id uuid NOT NULL,
        production_date date NOT NULL,
        initial_planned_quantity integer NOT NULL,
        planned_quantity integer NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_production_plans" PRIMARY KEY (id),
        CONSTRAINT ck_production_plans_initial_quantity CHECK (initial_planned_quantity >= 0),
        CONSTRAINT ck_production_plans_quantity CHECK (planned_quantity >= 0),
        CONSTRAINT fk_production_plans_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE TABLE production_records (
        id uuid NOT NULL,
        order_id uuid NOT NULL,
        production_date date NOT NULL,
        actual_quantity integer NOT NULL,
        created_by uuid NOT NULL,
        updated_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_production_records" PRIMARY KEY (id),
        CONSTRAINT ck_production_records_actual_quantity CHECK (actual_quantity >= 0),
        CONSTRAINT fk_production_records_created_by FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_production_records_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
        CONSTRAINT fk_production_records_updated_by FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE TABLE plan_adjustments (
        id uuid NOT NULL,
        source_production_plan_id uuid NOT NULL,
        shortage_quantity integer NOT NULL,
        adjustment_type character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        created_by uuid NOT NULL,
        applied_by uuid,
        reversed_by uuid,
        created_at timestamp with time zone NOT NULL,
        applied_at timestamp with time zone,
        reversed_at timestamp with time zone,
        CONSTRAINT "PK_plan_adjustments" PRIMARY KEY (id),
        CONSTRAINT ck_plan_adjustments_shortage CHECK (shortage_quantity > 0),
        CONSTRAINT ck_plan_adjustments_status CHECK (status IN ('Applied', 'Reversed')),
        CONSTRAINT ck_plan_adjustments_type CHECK (adjustment_type IN ('Manual', 'Automatic')),
        CONSTRAINT fk_plan_adjustments_applied_by FOREIGN KEY (applied_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_plan_adjustments_created_by FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_plan_adjustments_reversed_by FOREIGN KEY (reversed_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_plan_adjustments_source_plan FOREIGN KEY (source_production_plan_id) REFERENCES production_plans (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE TABLE plan_adjustment_items (
        id uuid NOT NULL,
        plan_adjustment_id uuid NOT NULL,
        production_plan_id uuid NOT NULL,
        add_on_quantity integer NOT NULL,
        CONSTRAINT "PK_plan_adjustment_items" PRIMARY KEY (id),
        CONSTRAINT ck_plan_adjustment_items_add_on CHECK (add_on_quantity > 0),
        CONSTRAINT fk_plan_adjustment_items_adjustment FOREIGN KEY (plan_adjustment_id) REFERENCES plan_adjustments (id) ON DELETE RESTRICT,
        CONSTRAINT fk_plan_adjustment_items_target_plan FOREIGN KEY (production_plan_id) REFERENCES production_plans (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE UNIQUE INDEX uq_orders_order_code ON orders (order_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX ix_plan_adjustment_items_adjustment ON plan_adjustment_items (plan_adjustment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX ix_plan_adjustment_items_target_plan ON plan_adjustment_items (production_plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE UNIQUE INDEX uq_plan_adjustment_items_adjustment_plan ON plan_adjustment_items (plan_adjustment_id, production_plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX "IX_plan_adjustments_applied_by" ON plan_adjustments (applied_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX "IX_plan_adjustments_created_by" ON plan_adjustments (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX "IX_plan_adjustments_reversed_by" ON plan_adjustments (reversed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX ix_plan_adjustments_source_plan ON plan_adjustments (source_production_plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE UNIQUE INDEX uq_production_plans_order_date ON production_plans (order_id, production_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX "IX_production_records_created_by" ON production_records (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE INDEX "IX_production_records_updated_by" ON production_records (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE UNIQUE INDEX uq_production_records_order_date ON production_records (order_id, production_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    CREATE UNIQUE INDEX uq_users_username ON users (username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814052112_InitialSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814052112_InitialSchema', '10.0.1');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    ALTER TABLE production_records RENAME TO production_days;

    ALTER TABLE production_days RENAME CONSTRAINT "PK_production_records" TO "PK_production_days";
    ALTER TABLE production_days RENAME CONSTRAINT fk_production_records_order           TO fk_production_days_order;
    ALTER TABLE production_days RENAME CONSTRAINT fk_production_records_created_by      TO fk_production_days_created_by;
    ALTER TABLE production_days RENAME CONSTRAINT fk_production_records_updated_by      TO fk_production_days_updated_by;
    ALTER TABLE production_days RENAME CONSTRAINT ck_production_records_actual_quantity TO ck_production_days_actual_quantity;

    ALTER INDEX uq_production_records_order_date       RENAME TO uq_production_days_order_date;
    ALTER INDEX "IX_production_records_created_by"     RENAME TO "IX_production_days_created_by";
    ALTER INDEX "IX_production_records_updated_by"     RENAME TO "IX_production_days_updated_by";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    ALTER TABLE production_days ALTER COLUMN actual_quantity DROP NOT NULL;

    ALTER TABLE production_days
        ADD COLUMN status    varchar(20) NOT NULL DEFAULT 'Open',
        ADD COLUMN closed_at timestamptz NULL,
        ADD COLUMN closed_by uuid        NULL;

    ALTER TABLE production_days ALTER COLUMN status DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    UPDATE production_days
    SET status    = 'Closed',
        closed_at = updated_at,
        closed_by = updated_by;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    ALTER TABLE production_days
        ADD CONSTRAINT fk_production_days_closed_by
            FOREIGN KEY (closed_by) REFERENCES users(id) ON DELETE RESTRICT;

    ALTER TABLE production_days
        ADD CONSTRAINT ck_production_days_status CHECK (status IN ('Open', 'Closed'));

    ALTER TABLE production_days
        ADD CONSTRAINT ck_production_days_closed_consistency CHECK (
            (status = 'Closed' AND closed_at IS NOT NULL AND closed_by IS NOT NULL AND actual_quantity IS NOT NULL)
            OR
            (status = 'Open' AND closed_at IS NULL AND closed_by IS NULL AND actual_quantity IS NULL));

    CREATE INDEX ix_production_days_status_date ON production_days (status, production_date);
    CREATE INDEX "IX_production_days_closed_by" ON production_days (closed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE TABLE system_settings (
        id uuid NOT NULL,
        recording_interval_minutes integer NOT NULL,
        day_start_time time NOT NULL,
        day_end_time time NOT NULL,
        updated_by uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_system_settings" PRIMARY KEY (id),
        CONSTRAINT ck_system_settings_interval CHECK (recording_interval_minutes BETWEEN 5 AND 480),
        CONSTRAINT ck_system_settings_time_range CHECK (day_end_time > day_start_time),
        CONSTRAINT fk_system_settings_updated_by FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE TABLE production_entries (
        id uuid NOT NULL,
        production_day_id uuid NOT NULL,
        quantity integer NOT NULL,
        recorded_at timestamp with time zone NOT NULL,
        note character varying(255),
        deleted_at timestamp with time zone,
        created_by uuid NOT NULL,
        updated_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_production_entries" PRIMARY KEY (id),
        CONSTRAINT ck_production_entries_quantity_positive CHECK (quantity > 0),
        CONSTRAINT fk_production_entries_created_by FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_production_entries_day FOREIGN KEY (production_day_id) REFERENCES production_days (id) ON DELETE RESTRICT,
        CONSTRAINT fk_production_entries_updated_by FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE TABLE production_entry_logs (
        id uuid NOT NULL,
        production_entry_id uuid NOT NULL,
        action character varying(20) NOT NULL,
        old_quantity integer,
        new_quantity integer,
        old_note character varying(255),
        new_note character varying(255),
        changed_by uuid NOT NULL,
        changed_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_production_entry_logs" PRIMARY KEY (id),
        CONSTRAINT ck_production_entry_logs_action CHECK (action IN ('Create', 'Update', 'Delete')),
        CONSTRAINT fk_production_entry_logs_changed_by FOREIGN KEY (changed_by) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_production_entry_logs_entry FOREIGN KEY (production_entry_id) REFERENCES production_entries (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE INDEX "IX_production_entries_created_by" ON production_entries (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE INDEX "IX_production_entries_updated_by" ON production_entries (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE INDEX ix_production_entries_day_active ON production_entries (production_day_id) WHERE deleted_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE INDEX ix_production_entries_day_recorded_at ON production_entries (production_day_id, recorded_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE INDEX "IX_production_entry_logs_changed_by" ON production_entry_logs (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE INDEX ix_production_entry_logs_entry ON production_entry_logs (production_entry_id, changed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    CREATE INDEX "IX_system_settings_updated_by" ON system_settings (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    INSERT INTO production_entries
        (id, production_day_id, quantity, recorded_at, note, deleted_at,
         created_by, updated_by, created_at, updated_at)
    SELECT gen_random_uuid(), d.id, d.actual_quantity, d.updated_at, NULL, NULL,
           d.created_by, d.updated_by, d.created_at, d.updated_at
    FROM production_days d
    WHERE d.actual_quantity > 0;

    INSERT INTO production_entry_logs
        (id, production_entry_id, action, old_quantity, new_quantity, old_note, new_note,
         changed_by, changed_at)
    SELECT gen_random_uuid(), e.id, 'Create', NULL, e.quantity, NULL, NULL,
           e.created_by, e.created_at
    FROM production_entries e;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902091104_CR01IntradayProductionEntries') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902091104_CR01IntradayProductionEntries', '10.0.1');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902131344_CR01SimplifySystemSettings') THEN
    ALTER TABLE system_settings DROP CONSTRAINT ck_system_settings_time_range;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902131344_CR01SimplifySystemSettings') THEN
    ALTER TABLE system_settings DROP COLUMN day_end_time;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902131344_CR01SimplifySystemSettings') THEN
    ALTER TABLE system_settings DROP COLUMN day_start_time;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902131344_CR01SimplifySystemSettings') THEN
    ALTER TABLE system_settings ADD remind_before_due boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902131344_CR01SimplifySystemSettings') THEN
    ALTER TABLE system_settings ALTER COLUMN remind_before_due DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902131344_CR01SimplifySystemSettings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902131344_CR01SimplifySystemSettings', '10.0.1');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DELETE FROM plan_adjustment_items;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DELETE FROM plan_adjustments;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DELETE FROM production_entry_logs;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DELETE FROM production_entries;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DELETE FROM production_days;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DELETE FROM production_plans;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DELETE FROM orders;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DROP INDEX uq_production_plans_order_date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    DROP INDEX uq_production_days_order_date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders DROP CONSTRAINT ck_orders_date_range;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders DROP CONSTRAINT ck_orders_status;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders RENAME COLUMN order_code TO shoe_code;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER INDEX uq_orders_order_code RENAME TO uq_orders_shoe_code;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE production_plans ADD production_line_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE production_days ADD production_line_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ALTER COLUMN start_date DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ALTER COLUMN due_date DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ADD image_content_type character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ADD image_file_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ADD image_path character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ADD image_size_bytes integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE production_plans ALTER COLUMN production_line_id DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE production_days ALTER COLUMN production_line_id DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE TABLE production_lines (
        id uuid NOT NULL,
        code character varying(30) NOT NULL,
        name character varying(100) NOT NULL,
        status character varying(20) NOT NULL,
        sort_order integer NOT NULL DEFAULT 0,
        note character varying(500),
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_production_lines" PRIMARY KEY (id),
        CONSTRAINT ck_production_lines_sort_order CHECK (sort_order >= 0),
        CONSTRAINT ck_production_lines_status CHECK (status IN ('Active', 'Inactive'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE TABLE order_production_lines (
        order_id uuid NOT NULL,
        production_line_id uuid NOT NULL,
        allocated_quantity integer NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_order_production_lines PRIMARY KEY (order_id, production_line_id),
        CONSTRAINT ck_order_production_lines_allocated CHECK (allocated_quantity > 0),
        CONSTRAINT fk_order_production_lines_line FOREIGN KEY (production_line_id) REFERENCES production_lines (id) ON DELETE RESTRICT,
        CONSTRAINT fk_order_production_lines_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE INDEX ix_production_plans_line ON production_plans (production_line_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE UNIQUE INDEX uq_production_plans_order_date_line ON production_plans (order_id, production_date, production_line_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE INDEX ix_production_days_line ON production_days (production_line_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE UNIQUE INDEX uq_production_days_order_date_line ON production_days (order_id, production_date, production_line_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE INDEX ix_orders_status ON orders (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ADD CONSTRAINT ck_orders_image CHECK ((image_path IS NULL AND image_file_name IS NULL AND image_content_type IS NULL AND image_size_bytes IS NULL) OR (image_path IS NOT NULL AND image_file_name IS NOT NULL AND image_content_type IS NOT NULL AND image_size_bytes IS NOT NULL AND image_size_bytes > 0));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ADD CONSTRAINT ck_orders_schedule_dates CHECK ((status = 'Pending' AND start_date IS NULL AND due_date IS NULL) OR (status <> 'Pending' AND start_date IS NOT NULL AND due_date IS NOT NULL AND start_date <= due_date));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE orders ADD CONSTRAINT ck_orders_status CHECK (status IN ('Pending', 'Incomplete', 'Completed'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE INDEX ix_order_production_lines_line ON order_production_lines (production_line_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    CREATE UNIQUE INDEX uq_production_lines_code ON production_lines (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE production_days ADD CONSTRAINT fk_production_days_line FOREIGN KEY (production_line_id) REFERENCES production_lines (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    ALTER TABLE production_plans ADD CONSTRAINT fk_production_plans_line FOREIGN KEY (production_line_id) REFERENCES production_lines (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907162123_CR001GoodsReceiptScheduleProductionLines') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260907162123_CR001GoodsReceiptScheduleProductionLines', '10.0.1');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927150921_ConfirmProductionSchedule') THEN
    ALTER TABLE orders ADD schedule_confirmed_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927150921_ConfirmProductionSchedule') THEN
    UPDATE orders SET schedule_confirmed_at = updated_at WHERE status <> 'Pending';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927150921_ConfirmProductionSchedule') THEN
    ALTER TABLE orders ADD CONSTRAINT ck_orders_schedule_confirmed CHECK (status <> 'Pending' OR schedule_confirmed_at IS NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927150921_ConfirmProductionSchedule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260927150921_ConfirmProductionSchedule', '10.0.1');
    END IF;
END $EF$;
COMMIT;

