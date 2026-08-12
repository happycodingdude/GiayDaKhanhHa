# PROMPT — IMPLEMENT PRODUCTION MANAGEMENT WEB APP

## 1. ROLE

You are acting as:

- Senior Business Analyst
- Senior Solution Architect
- Senior .NET Backend Engineer
- Senior React/TypeScript Frontend Engineer
- Database Engineer
- Product/Solution Consultant

Your task is to implement the Production Management Web App from the project's approved design documents.

The business and architecture design has already been analyzed and decided in previous work.

**Do not redesign the product while implementing it.**

Your job is to:

```text
Understand existing design
        ↓
Inspect repository
        ↓
Map design → implementation
        ↓
Implement
        ↓
Test
        ↓
Validate against business rules
        ↓
Continue until the implementation is complete
```

---

# 2. PROJECT DOCUMENTATION — READ ALL BEFORE CODING

All project design documents are located in:

```text
docs/
```

The current documentation set is:

```text
docs/
├── create-order-production-plan-screen.md
├── dashboard-screen-spec.md
├── man_hinh_6_option_2_spec_vi.md
├── order-detail-screen-spec.md
├── phantich.md
├── Production_Management_Implementation_Prompt_for_Claude.md
├── Production_Management_Step_3_Database_Schema.md
├── Production_Management_Step_4_API_Contract.md
├── Production_Management_Step_5_Frontend_Architecture.md
├── production-management-master-summary.md
├── production-management-step-1-domain-model.md
├── production-management-step-2-data-model.md
├── production-management-technical-continuation-prompt.md
├── production-order-list-screen.md
├── production-quantity-entry-screen-spec.md
└── production-shortage-option-1-screen-spec.md
```

### IMPORTANT

Before writing implementation code, read **all `.md` files inside `docs/`**.

Do not read only the Step 1–5 files.

The screen specification documents contain important UX/business-flow details that must be preserved during implementation.

The documents have different purposes:

### A. Business / Domain / Data / Technical Design

```text
production-management-master-summary.md
production-management-step-1-domain-model.md
production-management-step-2-data-model.md
Production_Management_Step_3_Database_Schema.md
Production_Management_Step_4_API_Contract.md
Production_Management_Step_5_Frontend_Architecture.md
production-management-technical-continuation-prompt.md
phantich.md
```

### B. Screen / UX Specifications

```text
create-order-production-plan-screen.md
dashboard-screen-spec.md
order-detail-screen-spec.md
production-order-list-screen.md
production-quantity-entry-screen-spec.md
production-shortage-option-1-screen-spec.md
man_hinh_6_option_2_spec_vi.md
```

### C. This implementation instruction

```text
Production_Management_Implementation_Prompt_for_Claude.md
```

This file is the implementation instruction and should be read together with all other documents.

---

# 3. DOCUMENT PRIORITY / CONFLICT RESOLUTION

Because the documentation contains both design decisions and detailed screen specifications, use the following priority rules.

## Priority 1 — Explicit approved business rules

If `production-management-master-summary.md` or the Step 1–4 domain/data/database/API documents explicitly define a business invariant, preserve it.

Examples:

```text
SUM(actual) <= Order.Quantity
Actual is a value, not an increment
Shortage is derived
Adjustment does not reduce another day's plan
Applied Adjustment is immutable
```

These rules must not be changed for implementation convenience.

---

## Priority 2 — Approved domain/data/database/API architecture

Use:

```text
production-management-step-1-domain-model.md
production-management-step-2-data-model.md
Production_Management_Step_3_Database_Schema.md
Production_Management_Step_4_API_Contract.md
```

for:

```text
Domain behavior
Entity relationships
Persistence model
Database constraints
API boundaries
Transaction expectations
Concurrency behavior
```

---

## Priority 3 — Approved frontend architecture

Use:

```text
Production_Management_Step_5_Frontend_Architecture.md
```

for:

```text
React architecture
Routing
TanStack Query
Feature boundaries
State management
Forms
API client
Frontend security
```

---

## Priority 4 — Screen specifications

Use all screen specification files for:

```text
Screen structure
User flow
Labels
Actions
Validation UX
Tables
Dialogs
Statistics presentation
Adjustment workflow
Navigation
```

Do not ignore these files just because the API/domain design already exists.

---

## Priority 5 — Existing repository conventions

Existing project code may determine:

```text
Naming
Project organization
Infrastructure utilities
Coding conventions
Testing conventions
```

Preserve existing conventions when they do not conflict with the approved design.

---

## Conflict rule

If two documents appear to conflict:

1. Identify the exact conflict.
2. Determine whether one is a UI representation and the other is a business invariant.
3. Preserve the business invariant.
4. Do not silently change either requirement.
5. If the conflict materially changes business behavior or architecture, stop and ask the user.
6. If it is only an implementation/detail ambiguity that does not affect business behavior, make the simplest reasonable engineering decision and document it.

Do not ask for confirmation for trivial implementation choices.

---

# 4. BUSINESS CONTEXT

This is a web application for managing shoe production.

Goals:

- Manage the quantity that must be produced.
- Manage production plans by date.
- At the end of the production day, manager enters actual production.
- Compare planned vs actual.
- If actual is lower than plan, handle shortage through an adjustment mechanism.
- Provide production statistics and progress.
- Phase 1 has one manager/user.
- Future versions may support multiple users/staff.
- Currently one day has one Order, but the system should remain structurally capable of evolving to multiple Orders per day.
- Shoe model and size are outside current scope.
- Application requires network connectivity.
- Primary target is desktop/laptop.

Technology already approved:

```text
Backend: .NET
Frontend: React + TypeScript + Vite
Database: PostgreSQL
Routing: TanStack Router
Server State: TanStack Query
Forms: React Hook Form + Zod
```

Do not use Next.js.

---

# 5. CORE DOMAIN — ORDER

Order is the main Aggregate Root.

```text
Order
├── ProductionPlan
├── ProductionRecord
└── PlanAdjustment
      └── PlanAdjustmentItem
```

Order:

```text
Id
OrderCode
Quantity
StartDate
DueDate
Status
```

Status:

```text
Incomplete
Completed
```

Rules:

```text
Quantity > 0
StartDate <= DueDate
```

When:

```text
TotalActual == Order.Quantity
```

Order:

```text
Completed
```

When:

```text
TotalActual < Order.Quantity
```

Order:

```text
Incomplete
```

---

# 6. PRODUCTION PLAN

```text
ProductionPlan
├── Id
├── OrderId
├── ProductionDate
├── InitialPlannedQuantity
├── PlannedQuantity
├── CreatedAt
└── UpdatedAt
```

Invariant:

```text
UNIQUE(OrderId, ProductionDate)
```

Initial plan:

```text
SUM(InitialPlannedQuantity) = Order.Quantity
```

`InitialPlannedQuantity` never changes.

`PlannedQuantity` is the current plan after adjustment.

After adjustment:

```text
SUM(PlannedQuantity)
```

may exceed:

```text
Order.Quantity
```

This is intentional.

Do NOT add a rule that reduces other production plans when an adjustment is applied.

---

# 7. PRODUCTION ACTUAL

There is exactly one ProductionRecord per:

```text
Order + ProductionDate
```

Constraint:

```text
UNIQUE(OrderId, ProductionDate)
```

Rules:

- Actual is entered once at the end of the day.
- If incorrect, edit the existing record.
- Do not create another record to accumulate quantity.
- No record means actual has not been entered.
- Actual `0` is valid.
- `0` must be distinguished from no record.

Invariant:

```text
SUM(All Actual) <= Order.Quantity
```

For editing:

```text
NewTotal =
CurrentTotal
- OldActual
+ NewActual
```

Reject if:

```text
NewTotal > Order.Quantity
```

Actual is a value, not an increment.

Do not create:

```text
/add
/increment
```

APIs.

---

# 8. SHORTAGE

Shortage is a derived business value.

Example:

```text
Plan = 100
Actual = 80
Shortage = 20
```

Do NOT create a Shortage entity/table.

Derived values include:

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

Do not persist these as independent authoritative values.

---

# 9. PLAN ADJUSTMENT

Two approved adjustment types:

```text
Manual
Automatic
```

## Manual

Manager chooses target plans.

Example:

```text
Shortage = 20

Target A +10
Target B +10
```

Do not reduce another day's plan.

## Automatic

System proposes an allocation.

Example:

```text
Shortage = 23

02/08 +6
03/08 +6
04/08 +6
05/08 +5
```

Keep automatic allocation logic isolated so the allocation rule can evolve without changing the entire adjustment workflow.

---

# 10. ADJUSTMENT MODEL

PlanAdjustment:

```text
Id
SourceProductionPlanId
ShortageQuantity
AdjustmentType
Status
CreatedBy
AppliedBy
ReversedBy
CreatedAt
AppliedAt
ReversedAt
```

There is intentionally no:

```text
OrderId
```

Order is reached through:

```text
PlanAdjustment
 ↓
SourceProductionPlan
 ↓
Order
```

PlanAdjustmentItem:

```text
Id
PlanAdjustmentId
ProductionPlanId
AddOnQuantity
```

`ProductionPlanId` is the target plan.

Invariant:

```text
SUM(Item.AddOnQuantity)
=
Adjustment.ShortageQuantity
```

No duplicate target plan inside one adjustment:

```text
UNIQUE(PlanAdjustmentId, ProductionPlanId)
```

---

# 11. ADJUSTMENT LIFECYCLE

Preview does not persist.

```text
Preview
 ↓
Calculate
 ↓
Proposal
```

Only Apply creates the persisted Adjustment:

```text
Applied
```

If actual changes and the adjustment is no longer valid:

```text
Applied
 ↓
Reversed
```

Never edit historical adjustment data.

Example:

```text
Adjustment #001
+20
→ Reversed

Adjustment #002
+10
→ Applied
```

Applied Adjustment is immutable historical fact.

---

# 12. ACTIVE ADJUSTMENT INVARIANT

A SourceProductionPlan can have at most one:

```text
Applied
```

PlanAdjustment at any time.

Valid:

```text
Adjustment #001 → Applied
Adjustment #001 → Reversed
Adjustment #002 → Applied
```

Invalid:

```text
Adjustment #001 → Applied
Adjustment #002 → Applied
```

for the same source ProductionPlan.

---

# 13. USER / AUTHENTICATION

Phase 1 User:

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

Authentication:

```text
Username + Password
```

Database stores only:

```text
password_hash
```

Never plaintext passwords.

Phase 1 does not have:

```text
Role
Permission
RolePermission
```

tables.

User is the actor for audit fields.

ProductionRecord:

```text
CreatedBy
UpdatedBy
```

PlanAdjustment:

```text
CreatedBy
AppliedBy
ReversedBy
```

Do not add audit columns to every table.

---

# 14. DATABASE BASELINE

Database:

```text
PostgreSQL
```

Enums:

```text
varchar + CHECK
```

Do not use PostgreSQL native ENUM.

Business dates:

```text
date
```

Audit timestamps:

```text
timestamptz
```

Store timestamps UTC.

Foreign keys:

```text
ON DELETE RESTRICT
```

Do not cascade production history.

Do not hard-delete an Order once production data exists.

Cross-row invariants:

```text
Application
+
Transaction
+
Concurrency Control
```

No DB triggers in Phase 1.

Concurrency:

```text
Transaction + PostgreSQL Row Locking
```

No explicit version column in Phase 1.

---

# 15. DATABASE TABLES

Approved core tables:

```text
users
orders
production_plans
production_records
plan_adjustments
plan_adjustment_items
```

## users

```text
id
username
password_hash
display_name
status
created_at
updated_at
```

Constraints:

```text
PK(id)
UNIQUE(username)
CHECK(status IN ('Active', 'Inactive'))
```

## orders

```text
id
order_code
quantity
start_date
due_date
status
created_at
updated_at
```

Constraints:

```text
PK(id)
UNIQUE(order_code)
CHECK(quantity > 0)
CHECK(start_date <= due_date)
CHECK(status IN ('Incomplete', 'Completed'))
```

## production_plans

```text
id
order_id
production_date
initial_planned_quantity
planned_quantity
created_at
updated_at
```

Constraints:

```text
PK(id)
FK(order_id)
UNIQUE(order_id, production_date)
CHECK(initial_planned_quantity >= 0)
CHECK(planned_quantity >= 0)
```

## production_records

```text
id
order_id
production_date
actual_quantity
created_by
updated_by
created_at
updated_at
```

Constraints:

```text
PK(id)
FK(order_id)
FK(created_by -> users.id)
FK(updated_by -> users.id)
UNIQUE(order_id, production_date)
CHECK(actual_quantity >= 0)
```

## plan_adjustments

```text
id
source_production_plan_id
shortage_quantity
adjustment_type
status
created_by
applied_by
reversed_by
created_at
applied_at
reversed_at
```

Constraints:

```text
PK(id)
FK(source_production_plan_id)
FK(created_by -> users.id)
FK(applied_by -> users.id)
FK(reversed_by -> users.id)
CHECK(shortage_quantity > 0)
CHECK(adjustment_type IN ('Manual', 'Automatic'))
CHECK(status IN ('Applied', 'Reversed'))
```

## plan_adjustment_items

```text
id
plan_adjustment_id
production_plan_id
add_on_quantity
```

Constraints:

```text
PK(id)
FK(plan_adjustment_id)
FK(production_plan_id)
UNIQUE(plan_adjustment_id, production_plan_id)
CHECK(add_on_quantity > 0)
```

---

# 16. API BASELINE

Base:

```text
/api/v1
```

Approved endpoints:

```text
POST /api/v1/auth/login
POST /api/v1/auth/logout
GET  /api/v1/auth/me

POST /api/v1/orders
GET  /api/v1/orders
GET  /api/v1/orders/{orderId}

GET  /api/v1/orders/{orderId}/production-plans

POST /api/v1/orders/{orderId}/production-records
PUT  /api/v1/orders/{orderId}/production-records/{productionRecordId}

POST /api/v1/production-plans/{productionPlanId}/adjustments/preview
POST /api/v1/production-plans/{productionPlanId}/adjustments

POST /api/v1/plan-adjustments/{adjustmentId}/reverse
GET  /api/v1/orders/{orderId}/plan-adjustments

GET  /api/v1/orders/{orderId}/statistics
GET  /api/v1/statistics/dashboard
```

Do not add generic CRUD endpoints merely because entities exist.

Read `Production_Management_Step_4_API_Contract.md` for the exact approved request/response contract and follow it.

---

# 17. AUTHENTICATION

Use:

```text
HttpOnly Cookie Authentication
```

Not JWT.

Login:

```text
POST /auth/login
```

Current user:

```text
GET /auth/me
```

Logout:

```text
POST /auth/logout
```

Do not store authentication tokens in:

```text
localStorage
sessionStorage
```

Login failure:

```text
401
```

Inactive user:

```text
403
```

---

# 18. ERROR MODEL

Standard error:

```json
{
  "code": "ORDER_NOT_FOUND",
  "message": "Order was not found.",
  "details": null
}
```

Validation error:

```json
{
  "code": "VALIDATION_ERROR",
  "message": "One or more validation errors occurred.",
  "details": [
    {
      "field": "quantity",
      "code": "MUST_BE_GREATER_THAN_ZERO",
      "message": "Quantity must be greater than zero."
    }
  ]
}
```

HTTP semantics:

```text
400 Invalid request / validation
401 Not authenticated
403 Authenticated but not allowed
404 Not found
409 Business/concurrency conflict
422 Business-rule validation
500 Unexpected error
```

Use the exact API error contract from:

```text
Production_Management_Step_4_API_Contract.md
```

if it specifies a more precise structure.

---

# 19. CREATE ORDER

Creating an Order also creates its initial ProductionPlans.

One transaction:

```text
Create Order
+
Create Initial ProductionPlans
```

Invariant:

```text
SUM(initial planned quantities)
=
Order.Quantity
```

Do not allow invalid initial plan totals.

Follow the detailed screen flow in:

```text
create-order-production-plan-screen.md
```

---

# 20. ACTUAL TRANSACTION

Create/Edit Actual must use a transaction.

Conceptually:

```text
BEGIN
    Lock Order
    Read current total actual
    Validate new total
    Insert/Update ProductionRecord
    Recalculate Order.Status
COMMIT
```

Do not perform:

```text
Read
→ Validate
→ Write
```

without locking.

---

# 21. ADJUSTMENT PREVIEW

Preview:

```text
POST /api/v1/production-plans/{productionPlanId}/adjustments/preview
```

Preview does not persist.

Manual preview contains target plans and add-on quantities.

Automatic preview asks the backend to calculate the proposal.

Read the exact DTO contract from:

```text
Production_Management_Step_4_API_Contract.md
```

Do not invent a different request/response shape.

---

# 22. ADJUSTMENT APPLY

Apply must revalidate the current server state.

Never trust an old frontend preview.

Transaction conceptually:

```text
Lock source ProductionPlan
Lock target ProductionPlans
Recalculate current shortage
Validate submitted proposal
Create PlanAdjustment
Create PlanAdjustmentItems
Increase target PlannedQuantity
Commit
```

All changes are atomic.

If preview is stale:

```text
409 Conflict
```

with the approved error code for stale adjustment state.

The frontend must request a new preview.

---

# 23. ADJUSTMENT REVERSE

```text
POST /api/v1/plan-adjustments/{adjustmentId}/reverse
```

Transaction:

```text
Lock Adjustment
Lock affected ProductionPlans
Validate Adjustment.Status == Applied
Subtract AddOnQuantity
Set Status = Reversed
Set ReversedBy
Set ReversedAt
Commit
```

Cannot reverse an already reversed adjustment.

Do not rewrite history.

---

# 24. IDEMPOTENCY / DUPLICATION

Do not introduce a generic idempotency table in Phase 1.

Use database/business constraints:

Actual:

```text
UNIQUE(order_id, production_date)
```

Adjustment:

```text
One Applied Adjustment per SourceProductionPlan
+
Transaction
+
Row Locking
```

---

# 25. CONCURRENCY

Important operations:

```text
Actual create/edit
Adjustment Apply
Adjustment Reverse
```

must use:

```text
Transaction
+
Row Locking
```

When locking multiple ProductionPlans, lock them in deterministic ID/date order to reduce deadlock risk.

No explicit version column.

---

# 26. FRONTEND ARCHITECTURE

Use:

```text
React
TypeScript
Vite
TanStack Router
TanStack Query
React Hook Form
Zod
```

Feature-based structure:

```text
src/
├── app/
│   ├── router/
│   ├── providers/
│   ├── layouts/
│   └── config/
│
├── features/
│   ├── auth/
│   ├── orders/
│   ├── production/
│   ├── adjustments/
│   └── statistics/
│
├── shared/
│   ├── components/
│   ├── forms/
│   ├── dialogs/
│   ├── table/
│   ├── feedback/
│   ├── hooks/
│   └── lib/
│
├── api/
│   ├── client.ts
│   └── errors.ts
│
├── main.tsx
└── index.css
```

Do not create a giant global:

```text
components/
hooks/
services/
```

structure.

---

# 27. ROUTES

Approved:

```text
/login
/dashboard
/orders
/orders/new
/orders/:orderId
```

Production plans, actuals and adjustments are part of the Order workflow.

Do not create unnecessary top-level routes for them.

---

# 28. SERVER STATE

TanStack Query owns:

```text
Current User
Orders
Order Detail
Production Plans
Production Records
Adjustments
Statistics
```

No Redux/Zustand in Phase 1.

Local React state handles:

```text
Dialogs
Forms
Temporary selections
Preview state
Filters
```

---

# 29. API CLIENT

React components must never call `fetch()` or HTTP directly.

Flow:

```text
Component
 ↓
Feature Hook
 ↓
Feature API
 ↓
Shared API Client
 ↓
Backend
```

Frontend DTOs represent API contracts, not database entities.

---

# 30. ORDER DETAIL

Order Detail is the central production-management screen.

Use the approved screen specification:

```text
order-detail-screen-spec.md
```

and frontend architecture:

```text
Production_Management_Step_5_Frontend_Architecture.md
```

Conceptually:

```text
Order Header
 ↓
Order Summary
 ↓
Production Timeline
 ↓
Shortage / Adjustment
 ↓
Adjustment History
 ↓
Statistics
```

Daily production row:

```text
Date
Planned
Actual
Difference
Shortage
Action
```

---

# 31. PRODUCTION ACTUAL SCREEN

Use:

```text
production-quantity-entry-screen-spec.md
```

Actual is a value.

Correct:

```text
Actual Quantity: 18
```

Incorrect:

```text
+5
```

Explicit zero:

```text
0
```

is valid.

The UI must distinguish:

```text
No actual record
```

from:

```text
Actual = 0
```

---

# 32. ORDER LIST

Use:

```text
production-order-list-screen.md
```

The list should present the approved order-level production information and status.

Do not invent fields that are not supported by the approved API/domain design.

---

# 33. CREATE ORDER / PRODUCTION PLAN SCREEN

Use:

```text
create-order-production-plan-screen.md
```

The screen must follow the approved workflow for:

```text
Order information
Production period
Daily production plan
Validation
Review
Create
```

The backend remains authoritative for all invariants.

---

# 34. SHORTAGE OPTION 1

Use:

```text
production-shortage-option-1-screen-spec.md
```

This screen represents the approved first shortage-handling option.

Preserve its:

```text
User flow
Target selection
Validation
Confirmation
```

Do not alter the underlying adjustment business rules.

---

# 35. SHORTAGE OPTION 2

Use:

```text
man_hinh_6_option_2_spec_vi.md
```

This is the approved UI/flow specification for the second shortage-handling option.

Implement it consistently with the Adjustment domain:

```text
Preview
Proposal
Apply
```

and the approved API contract.

---

# 36. DASHBOARD

Use:

```text
dashboard-screen-spec.md
```

The dashboard must consume derived/statistical data from the backend.

Do not introduce new persisted aggregate fields solely to simplify dashboard rendering.

---

# 37. STATISTICS

Statistics are derived.

Examples:

```text
Total Plan
Total Actual
Remaining
Progress
Daily Difference
Shortage
Cumulative Actual
Cumulative Plan
```

Follow:

```text
dashboard-screen-spec.md
order-detail-screen-spec.md
```

for presentation requirements.

---

# 38. DATE/TIME

Business dates:

```text
ProductionDate
StartDate
DueDate
```

are date-only values.

Use:

```text
YYYY-MM-DD
```

Do not convert them through timezone-sensitive JavaScript `Date` logic in a way that can shift the calendar day.

Audit timestamps are UTC.

Frontend converts timestamps only for display.

---

# 39. FRONTEND QUERY INVALIDATION

After Actual create/edit:

```text
["orders", orderId]
["orders", orderId, "production-plans"]
["orders", orderId, "statistics"]
```

After Adjustment Apply/Reverse:

```text
["orders", orderId]
["orders", orderId, "production-plans"]
["orders", orderId, "statistics"]
["orders", orderId, "plan-adjustments"]
```

Use the exact query-key strategy from the approved frontend architecture where specified.

---

# 40. OPTIMISTIC UPDATE POLICY

Do NOT use optimistic updates for:

```text
Actual create/edit
Adjustment Apply
Adjustment Reverse
```

Use:

```text
Submit
 ↓
Loading
 ↓
Server transaction
 ↓
Success
 ↓
Invalidate/refetch
```

---

# 41. BACKEND ARCHITECTURE

Implement as a modular monolith.

Maintain clear boundaries:

```text
API
Application
Domain
Infrastructure
```

Conceptual flow:

```text
Controller / Endpoint
 ↓
Application Use Case
 ↓
Domain Rules
 ↓
Infrastructure / EF Core
 ↓
PostgreSQL
```

Do not introduce microservices.

Do not over-engineer CQRS/event sourcing unless the existing repository already requires it.

---

# 42. IMPLEMENTATION ORDER

## Phase 1 — Foundation

```text
Backend solution structure
Frontend project structure
PostgreSQL
EF Core
API infrastructure
API client
TanStack Router
TanStack Query
```

## Phase 2 — Authentication

```text
User
Password hashing
Login
Logout
Current user
Protected routes
Login UI
```

## Phase 3 — Order

```text
Order
Create Order
Initial Production Plans
Order list
Order detail
```

## Phase 4 — Production

```text
ProductionPlan
ProductionRecord
Create Actual
Edit Actual
Order status
Production timeline
```

## Phase 5 — Adjustment

```text
Shortage
Manual preview
Automatic preview
Apply
Reverse
History
Concurrency
```

## Phase 6 — Statistics

```text
Order statistics
Dashboard statistics
Progress
```

## Phase 7 — Hardening

```text
Validation
Error handling
Concurrency tests
Integration tests
UI states
Security review
Database constraints
```

---

# 43. TESTING REQUIREMENTS

At minimum test:

## Order

```text
Quantity > 0
StartDate <= DueDate
Initial plan total == Order.Quantity
Completed when total actual == quantity
Incomplete when total actual < quantity
```

## Actual

```text
Actual >= 0
One record per Order + ProductionDate
Total actual cannot exceed Order.Quantity
Edit replaces old value
Actual = 0 is valid
```

## Shortage

```text
Shortage = max(Plan - Actual, 0)
```

## Adjustment

```text
Shortage > 0
Item quantity > 0
Item total == shortage
No duplicate target
Target plan increases
Other plans are not reduced
Applied adjustment immutable
Applied → Reversed
Cannot reverse twice
Only one Applied adjustment per source plan
```

## Concurrency

Test concurrent operations against the same:

```text
Order
ProductionPlan
Adjustment
```

---

# 44. WHAT NOT TO BUILD IN PHASE 1

Do not add:

```text
Role/Permission system
Staff management
Multi-tenancy
Microservices
Event sourcing
CQRS infrastructure without need
Database triggers
PostgreSQL native enums
Generic repository solely for pattern compliance
Generic idempotency table
Global audit log system
Notifications
Mobile app
Shoe model/size management
Complex reporting engine
```

Do not build speculative infrastructure.

---

# 45. IMPLEMENTATION WORKING METHOD

Before coding:

```text
1. Read every .md file under docs/.
2. Inspect the repository structure.
3. Identify backend/frontend/database state.
4. Identify existing implementation.
5. Compare existing code against approved documentation.
6. Produce a concise gap analysis.
7. Define implementation sequence.
8. Begin implementation.
```

During implementation:

```text
Inspect
 ↓
Implement smallest coherent unit
 ↓
Build
 ↓
Run tests
 ↓
Fix
 ↓
Review business invariants
 ↓
Continue
```

Do not ask the user for confirmation after every small step.

Only ask when a genuine business or architectural decision cannot be resolved from the documentation.

---

# 46. DEFINITION OF DONE

A feature is done only when:

```text
Business rule implemented
+
Database constraints correct
+
Transaction/concurrency behavior correct
+
API contract correct
+
Frontend UX correct
+
Validation correct
+
Error handling correct
+
Relevant automated tests pass
```

A successful compilation alone is NOT Definition of Done.

---

# 47. NON-NEGOTIABLE BUSINESS RULES

Never silently change these:

```text
Actual total <= Order.Quantity
Actual is a value, not an increment
One ProductionRecord per Order + ProductionDate
Actual = 0 is valid
No record != Actual 0
Shortage is derived
Initial plan total == Order.Quantity
InitialPlannedQuantity is immutable
PlannedQuantity can increase through adjustment
Adjustment does not reduce another day's plan
Adjustment item total == shortage
No duplicate target within one adjustment
Preview does not persist
Apply revalidates current state
Applied Adjustment is immutable
Applied → Reversed
Only one Applied Adjustment per source plan
Order status derived from total actual
HttpOnly Cookie Authentication
PostgreSQL
Transaction + Row Locking
No Phase 1 Role/Permission system
```

---

# 48. FIRST TASK TO EXECUTE

Start by doing exactly this:

### Step 1

Read:

```text
docs/*.md
```

all of them.

### Step 2

Inspect the repository:

```text
backend
frontend
solution/project files
package files
EF Core configuration
database configuration
existing tests
```

### Step 3

Produce:

```text
IMPLEMENTATION GAP ANALYSIS
```

containing:

```text
1. Existing architecture
2. Existing backend status
3. Existing frontend status
4. Existing database/migrations status
5. Existing authentication status
6. What is already implemented
7. What is missing
8. What conflicts with the approved design
9. Recommended implementation order
```

Keep the gap analysis concise.

### Step 4

Begin implementing the first coherent phase immediately.

Do not wait for user confirmation unless there is a real business/architecture conflict.

---


# 50. NON-NEGOTIABLE IMPLEMENTATION STRATEGY — EVERY FEATURE MUST BE UI-TESTABLE

The implementation must follow a **Vertical Slice / End-to-End Feature** strategy.

Do NOT implement the whole backend first and postpone the frontend.

For every feature:

```text
Database / Persistence
        ↓
Domain / Application
        ↓
API
        ↓
Frontend API integration
        ↓
UI
        ↓
User can test the complete flow
```

A feature is NOT considered complete if only:

```text
Entity exists
API exists
Unit tests pass
```

but the user cannot perform the business flow through the UI.

## Required Definition of Done for EVERY FEATURE

Every feature must include, where applicable:

1. Database / migration changes
2. Backend domain/application logic
3. API endpoint(s)
4. Request/response DTOs
5. Backend validation
6. Business-rule validation
7. Transaction/concurrency handling
8. Frontend API integration
9. Frontend screen/component
10. Form/input validation
11. Loading state
12. Empty state
13. Error state
14. Success feedback
15. Query invalidation/refetch behavior
16. Automated tests for important business rules
17. A clear UI test scenario

The user must be able to open the application and test the feature manually.

---

## Feature Delivery Rule

Implement features as complete vertical slices.

### Example — Create Order

Do NOT do:

```text
Step 1: Create Order entity
Step 2: Create repository
Step 3: Create API
Step 4: Move to next backend feature
Step 5: Build all frontend later
```

Instead:

```text
Create Order
    ↓
Database
    ↓
Domain/Application
    ↓
API
    ↓
React API integration
    ↓
Create Order screen
    ↓
Validation
    ↓
Success/error handling
    ↓
User manually creates an Order in UI
    ↓
Verify Order appears in Order List
    ↓
Feature DONE
```

---

## Feature Testability Requirement

At the end of each feature, provide a short:

```text
UI TEST CHECKLIST
```

containing concrete actions the user can perform.

Example:

```text
UI TEST — Create Order

1. Login
2. Open Orders
3. Click Create Order
4. Enter Order Code = ORD-001
5. Enter Quantity = 100
6. Select Start Date
7. Select Due Date
8. Enter daily plan totaling 100
9. Click Create
10. Verify success message
11. Verify Order appears in Order List
12. Open Order Detail
13. Verify the created Production Plans
```

The test must use the real UI and real backend/database.

Do not treat Postman/cURL/swagger-only testing as sufficient for feature completion.

API-level tests may supplement UI testing, but they do not replace it.

---

## Feature Boundary

A feature should be small enough to become usable and testable.

Recommended sequence:

```text
Foundation
   ↓
Authentication UI
   ↓
Create Order UI + backend
   ↓
Order List UI + backend
   ↓
Order Detail UI + backend
   ↓
Enter Actual UI + backend
   ↓
Edit Actual UI + backend
   ↓
Shortage Display
   ↓
Manual Adjustment UI + backend
   ↓
Automatic Adjustment UI + backend
   ↓
Adjustment History UI + backend
   ↓
Adjustment Reverse UI + backend
   ↓
Statistics UI + backend
   ↓
Dashboard UI + backend
   ↓
Hardening / cross-feature testing
```

The exact grouping may be adjusted when the screen specifications indicate that two operations form one coherent user flow, but **every completed increment must remain UI-testable**.

---

## Never Leave the Application in a Backend-Only State

Avoid ending an implementation increment with:

```text
"API is complete; frontend will be implemented later."
```

unless the user explicitly asks for backend-only work.

The normal implementation behavior is:

```text
Backend + API + UI
```

for each feature.

---

## UI-First Verification After Each Feature

After implementing a feature:

1. Build backend.
2. Build frontend.
3. Run relevant automated tests.
4. Start the application.
5. Verify the actual UI flow.
6. Verify data persisted correctly.
7. Verify the next related screen reflects the change.
8. Verify error/validation cases through UI where practical.
9. Only then mark the feature as DONE.

If the environment prevents automatic browser/UI execution, still ensure the feature is fully wired and provide exact manual UI steps for verification.

---

## Cross-Feature Integration

A feature must also preserve previously completed flows.

Example:

After implementing Actual:

```text
Create Order
    ↓
Open Order
    ↓
Enter Actual
    ↓
Refresh / navigate
    ↓
Verify Actual
    ↓
Verify Progress
    ↓
Verify Order Status
    ↓
Verify Shortage if applicable
```

After implementing Adjustment:

```text
Create Order
    ↓
Enter Actual
    ↓
Create Shortage
    ↓
Preview Adjustment
    ↓
Apply Adjustment
    ↓
Verify target Production Plan increased
    ↓
Verify source shortage/history
    ↓
Refresh page
    ↓
Verify persisted state
```

Do not validate a feature in isolation if its business result is consumed by another already implemented feature.

---

## Implementation Reporting

When reporting progress, use:

```text
FEATURE: <name>

Status:
DONE / IN PROGRESS

Implemented:
- Database
- Backend
- API
- Frontend
- Validation
- Tests

UI Test:
1. ...
2. ...
3. ...

Verified:
- ...
```

Do not report a feature as DONE merely because its code compiles.


# 49. FINAL PRINCIPLE

The project already has an approved design.

Therefore:

> **Do not redesign. Implement.**

Use the files in:

```text
docs/
```

as the project knowledge base.

Use this prompt as the implementation operating procedure.

Preserve approved business rules.

Prefer simple solutions.

Avoid over-engineering.

When implementation choices are ambiguous but do not affect business behavior, make the simplest maintainable engineering decision yourself.

The final goal is a production-quality:

```text
React + TypeScript + Vite
        +
.NET Modular Monolith
        +
PostgreSQL
```

Production Management Web App that faithfully implements the approved business requirements and screen specifications.
