# STEP 3 — DATABASE SCHEMA
## Production Management Web App

> Status: **DONE**
>
> Baseline: Step 1 — Domain Model DONE, Step 2 — Data Model DONE, Step 3 — Database Schema DONE.

---

# 1. Scope

PostgreSQL schema for the production management system.

Final tables:

1. `users`
2. `orders`
3. `production_plans`
4. `production_records`
5. `plan_adjustments`
6. `plan_adjustment_items`

The `User` model was added during Step 3 because authentication had been missed from the original data model.

---

# 2. Identity / User Model

## 2.1 User

Phase 1 uses:

- Username + password authentication.
- Password is stored only as `password_hash`.
- `users.username` is unique.
- No Role/Permission tables in Phase 1.
- User is a separate identity model, not part of the Order aggregate.
- User acts as the actor for auditable production operations.

Model:

```text
User
├── Id
├── Username
├── PasswordHash
├── DisplayName
├── Status
├── CreatedAt
└── UpdatedAt
```

Status:

```text
Active
Inactive
```

Authentication implementation (JWT/session/etc.) is deferred to later architecture/API/implementation steps.

---

# 3. Core Production Domain

```text
Order
├── ProductionPlan
├── ProductionRecord
└── PlanAdjustment
      └── PlanAdjustmentItem
```

User is outside the production aggregate.

---

# 4. PostgreSQL DDL

## 4.1 users

```sql
CREATE TABLE users (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username        varchar(100) NOT NULL,
    password_hash   varchar(255) NOT NULL,
    display_name    varchar(100) NOT NULL,
    status          varchar(20) NOT NULL,
    created_at      timestamptz NOT NULL,
    updated_at      timestamptz NOT NULL,

    CONSTRAINT uq_users_username
        UNIQUE (username),

    CONSTRAINT ck_users_status
        CHECK (status IN ('Active', 'Inactive'))
);
```

---

## 4.2 orders

```sql
CREATE TABLE orders (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_code      varchar(50) NOT NULL,
    quantity        integer NOT NULL,
    start_date      date NOT NULL,
    due_date        date NOT NULL,
    status          varchar(20) NOT NULL,
    created_at      timestamptz NOT NULL,
    updated_at      timestamptz NOT NULL,

    CONSTRAINT uq_orders_order_code
        UNIQUE (order_code),

    CONSTRAINT ck_orders_quantity_positive
        CHECK (quantity > 0),

    CONSTRAINT ck_orders_date_range
        CHECK (start_date <= due_date),

    CONSTRAINT ck_orders_status
        CHECK (status IN ('Incomplete', 'Completed'))
);
```

---

## 4.3 production_plans

```sql
CREATE TABLE production_plans (
    id                          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id                    bigint NOT NULL,
    production_date             date NOT NULL,
    initial_planned_quantity    integer NOT NULL,
    planned_quantity            integer NOT NULL,
    created_at                  timestamptz NOT NULL,
    updated_at                  timestamptz NOT NULL,

    CONSTRAINT fk_production_plans_order
        FOREIGN KEY (order_id)
        REFERENCES orders(id)
        ON DELETE RESTRICT,

    CONSTRAINT uq_production_plans_order_date
        UNIQUE (order_id, production_date),

    CONSTRAINT ck_production_plans_initial_quantity
        CHECK (initial_planned_quantity >= 0),

    CONSTRAINT ck_production_plans_quantity
        CHECK (planned_quantity >= 0)
);
```

---

## 4.4 production_records

User audit was added to this table.

```sql
CREATE TABLE production_records (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id            bigint NOT NULL,
    production_date     date NOT NULL,
    actual_quantity     integer NOT NULL,
    created_by          bigint NOT NULL,
    updated_by          bigint NOT NULL,
    created_at          timestamptz NOT NULL,
    updated_at          timestamptz NOT NULL,

    CONSTRAINT fk_production_records_order
        FOREIGN KEY (order_id)
        REFERENCES orders(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_production_records_created_by
        FOREIGN KEY (created_by)
        REFERENCES users(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_production_records_updated_by
        FOREIGN KEY (updated_by)
        REFERENCES users(id)
        ON DELETE RESTRICT,

    CONSTRAINT uq_production_records_order_date
        UNIQUE (order_id, production_date),

    CONSTRAINT ck_production_records_actual_quantity
        CHECK (actual_quantity >= 0)
);
```

Rules:

- One record per Order per production date.
- Editing actual updates the existing record.
- No record means actual has not been entered.
- `actual_quantity = 0` is valid if explicitly entered.

---

## 4.5 plan_adjustments

User audit was added because Adjustment is a business operation with historical significance.

```sql
CREATE TABLE plan_adjustments (
    id                          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_production_plan_id  bigint NOT NULL,
    shortage_quantity           integer NOT NULL,
    adjustment_type             varchar(20) NOT NULL,
    status                      varchar(20) NOT NULL,
    created_by                  bigint NOT NULL,
    applied_by                  bigint NULL,
    reversed_by                 bigint NULL,
    created_at                  timestamptz NOT NULL,
    applied_at                  timestamptz NULL,
    reversed_at                 timestamptz NULL,

    CONSTRAINT fk_plan_adjustments_source_plan
        FOREIGN KEY (source_production_plan_id)
        REFERENCES production_plans(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_plan_adjustments_created_by
        FOREIGN KEY (created_by)
        REFERENCES users(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_plan_adjustments_applied_by
        FOREIGN KEY (applied_by)
        REFERENCES users(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_plan_adjustments_reversed_by
        FOREIGN KEY (reversed_by)
        REFERENCES users(id)
        ON DELETE RESTRICT,

    CONSTRAINT ck_plan_adjustments_shortage
        CHECK (shortage_quantity > 0),

    CONSTRAINT ck_plan_adjustments_type
        CHECK (adjustment_type IN ('Manual', 'Automatic')),

    CONSTRAINT ck_plan_adjustments_status
        CHECK (status IN ('Applied', 'Reversed'))
);
```

Important:

- No `order_id`.
- Source Order is obtained through `source_production_plan_id`.
- Preview is not persisted.
- Only Apply creates an Adjustment.
- Applied Adjustment is immutable history.
- Recalculation creates a new Adjustment after reversing the old one.

---

## 4.6 plan_adjustment_items

```sql
CREATE TABLE plan_adjustment_items (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    plan_adjustment_id  bigint NOT NULL,
    production_plan_id  bigint NOT NULL,
    add_on_quantity     integer NOT NULL,

    CONSTRAINT fk_plan_adjustment_items_adjustment
        FOREIGN KEY (plan_adjustment_id)
        REFERENCES plan_adjustments(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_plan_adjustment_items_target_plan
        FOREIGN KEY (production_plan_id)
        REFERENCES production_plans(id)
        ON DELETE RESTRICT,

    CONSTRAINT uq_plan_adjustment_items_adjustment_plan
        UNIQUE (plan_adjustment_id, production_plan_id),

    CONSTRAINT ck_plan_adjustment_items_add_on
        CHECK (add_on_quantity > 0)
);
```

The unique constraint prevents the same target plan from appearing twice in one Adjustment.

---

# 5. Enum Strategy

Use:

```text
varchar + CHECK
```

in PostgreSQL rather than PostgreSQL native ENUM.

Reason:

- Easier future migration.
- Domain status may expand later.
- Backend .NET can still use strongly typed enums.
- Avoids unnecessary PostgreSQL-specific coupling.
- Fits the project's non-overengineering principle.

Current values:

### OrderStatus

```text
Incomplete
Completed
```

### AdjustmentType

```text
Manual
Automatic
```

### AdjustmentStatus

```text
Applied
Reversed
```

### UserStatus

```text
Active
Inactive
```

---

# 6. Index Strategy

Unique constraints already create the required unique indexes:

```text
orders(order_code)

production_plans(order_id, production_date)

production_records(order_id, production_date)

plan_adjustment_items(plan_adjustment_id, production_plan_id)
```

Additional indexes:

```sql
CREATE INDEX ix_plan_adjustments_source_plan
ON plan_adjustments (source_production_plan_id);

CREATE INDEX ix_plan_adjustment_items_adjustment
ON plan_adjustment_items (plan_adjustment_id);

CREATE INDEX ix_plan_adjustment_items_target_plan
ON plan_adjustment_items (production_plan_id);
```

The `users.username` unique constraint already provides the login lookup index.

---

# 7. Foreign Key Delete Strategy

Production history must not disappear through cascading deletes.

Use:

```text
ON DELETE RESTRICT
```

for all business relationships.

Especially:

```text
orders
  ← production_plans
  ← production_records

production_plans
  ← plan_adjustments
  ← plan_adjustment_items
```

and:

```text
users
  ← production_records
  ← plan_adjustments
```

No cascade delete is used.

Order hard-delete is not allowed once production data exists.

---

# 8. Timestamp Strategy

Use PostgreSQL:

```text
timestamptz
```

for audit timestamps.

All timestamps are stored in UTC.

Business dates use:

```text
date
```

for:

- `start_date`
- `due_date`
- `production_date`

No timezone is attached to business dates.

---

# 9. Cross-row Business Invariants

The following rules are NOT implemented as normal CHECK constraints:

```text
SUM(ProductionRecord.ActualQuantity)
    <= Order.Quantity
```

and:

```text
SUM(PlanAdjustmentItem.AddOnQuantity)
    = PlanAdjustment.ShortageQuantity
```

These require transaction-level/application-level validation.

Protection strategy:

```text
Application
+
Database Transaction
+
Concurrency Control
```

---

# 10. Concurrency Strategy

No `version` column is added in Step 3.

Concurrency-sensitive operations will use database transactions and row locking.

For example, when editing Actual:

```text
BEGIN TRANSACTION

Lock Order row
      ↓
Read current total actual
      ↓
Calculate new total
      ↓
Validate:
    new total <= order.quantity
      ↓
Update ProductionRecord
      ↓
Update Order status
      ↓
COMMIT
```

This prevents two concurrent requests from independently passing validation and exceeding `Order.Quantity`.

The exact EF Core transaction/locking implementation belongs to the implementation/API steps.

---

# 11. Adjustment Consistency

When applying an Adjustment:

```text
BEGIN TRANSACTION

Lock source/affected plans as required
      ↓
Validate shortage
      ↓
Validate target plans
      ↓
Validate total item quantity
      ↓
Create PlanAdjustment
      ↓
Create PlanAdjustmentItems
      ↓
Update target ProductionPlan.PlannedQuantity
      ↓
COMMIT
```

The business invariant:

```text
SUM(items.add_on_quantity)
=
adjustment.shortage_quantity
```

must be validated within the same transaction.

---

# 12. Initial Plan Integrity

At initial Order planning:

```text
SUM(InitialPlannedQuantity)
=
Order.Quantity
```

This is an application transaction invariant.

After adjustments:

```text
SUM(PlannedQuantity)
```

may be greater than:

```text
Order.Quantity
```

because adjustments do not increase Order.Quantity.

---

# 13. Derived Data

The database does not persist:

```text
TotalActual
Remaining
Progress
TotalPlan
DailyShortage
DailyDifference
CumulativeActual
CumulativePlan
```

They are calculated from source data.

Examples:

```text
TotalActual = SUM(ProductionRecord.ActualQuantity)

Remaining = Order.Quantity - TotalActual

Progress = TotalActual / Order.Quantity
```

---

# 14. User Audit Strategy

Phase 1 now includes User because authentication is required.

Audited production actions:

## ProductionRecord

```text
created_by
updated_by
```

This answers:

- Who entered actual?
- Who last edited actual?

## PlanAdjustment

```text
created_by
applied_by
reversed_by
```

This answers:

- Who created the adjustment?
- Who applied it?
- Who reversed it?

We intentionally do NOT add audit columns to every table.

---

# 15. Seed / Reference Data

No static password should be hard-coded in migration files.

Initial Manager/User creation should be handled through an application bootstrap/setup mechanism using a properly generated password hash.

No Role/Permission seed data is required in Phase 1.

---

# 16. Final Schema

```text
users
├── id
├── username
├── password_hash
├── display_name
├── status
├── created_at
└── updated_at

orders
├── id
├── order_code
├── quantity
├── start_date
├── due_date
├── status
├── created_at
└── updated_at

production_plans
├── id
├── order_id
├── production_date
├── initial_planned_quantity
├── planned_quantity
├── created_at
└── updated_at

production_records
├── id
├── order_id
├── production_date
├── actual_quantity
├── created_by
├── updated_by
├── created_at
└── updated_at

plan_adjustments
├── id
├── source_production_plan_id
├── shortage_quantity
├── adjustment_type
├── status
├── created_by
├── applied_by
├── reversed_by
├── created_at
├── applied_at
└── reversed_at

plan_adjustment_items
├── id
├── plan_adjustment_id
├── production_plan_id
└── add_on_quantity
```

---

# 17. Final Relationship

```text
                         ┌──────────────┐
                         │    users     │
                         └──────┬───────┘
                                │
                    ┌───────────┴───────────┐
                    │                       │
                    ▼                       ▼
          production_records       plan_adjustments
                                            │
                                            │
                                            ▼
                                    plan_adjustment_items
                                            │
                                            ▼
                                      production_plans
                                            │
                                            ▼
                                          orders
```

More precisely:

```text
orders
  1 ─────── * production_plans
  1 ─────── * production_records

production_plans
  1 ─────── * plan_adjustments

plan_adjustments
  1 ─────── * plan_adjustment_items

plan_adjustment_items
  * ─────── 1 production_plans

users
  1 ─────── * production_records
  1 ─────── * plan_adjustments
```

---

# 18. Step 3 Decisions

Final decisions:

- PostgreSQL.
- 6 tables including `users`.
- `User` is a separate identity model.
- Phase 1 authentication uses username/password.
- Password stored only as hash.
- No Role/Permission tables in Phase 1.
- `varchar + CHECK` instead of PostgreSQL ENUM.
- `timestamptz` for audit timestamps.
- `date` for business dates.
- `ON DELETE RESTRICT`.
- No cascade deletion of production history.
- No hard-delete of production Orders.
- No `order_id` in `plan_adjustments`.
- Source Plan referenced by `source_production_plan_id`.
- Target Plan referenced by `production_plan_id`.
- Duplicate target Plan within one Adjustment is prohibited.
- Preview Adjustment is not persisted.
- Applied Adjustment is immutable history.
- Reversal creates historical state; recalculation creates a new Adjustment.
- Actual has one record per Order per date.
- Cross-row invariants remain transaction/application responsibilities.
- Concurrency uses transaction + database row locking.
- No explicit `version` column is added at this stage.
- User audit is captured for Actual and Adjustment operations.
- No unnecessary audit columns on every table.
- Initial user creation must not hard-code a password in migrations.

---

# 19. Roadmap Status

```text
Step 1 — Domain Model
DONE

Step 2 — Data Model
DONE

Step 3 — Database Schema
DONE

Step 4 — API Contract
NEXT

Step 5 — Frontend Architecture
PENDING

Step 6 — Implementation
PENDING
```

## Step 3 complete.

Next time, start directly from:

> **STEP 4 — API CONTRACT**

Do not reopen Step 1–3 unless a later API/implementation requirement exposes a serious contradiction with the established baseline.
