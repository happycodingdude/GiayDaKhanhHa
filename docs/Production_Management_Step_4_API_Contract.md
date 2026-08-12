# Production Management Web App — Step 4 API Contract

## Status

**STEP 4 — API Contract: DONE**

Date: 2026-08-12

---

## 1. API Design Principles

Base path:

```text
/api/v1
```

API is organized around business resources rather than directly exposing database tables.

Main resources:

```text
/auth
/orders
/production-plans
/production-records
/plan-adjustments
/statistics
```

Authentication is required for business operations.

Current user identity is always taken from the authenticated context. The client must not send audit user IDs such as `createdBy` or `updatedBy`.

---

## 2. Authentication

### Chosen architecture

**HttpOnly Cookie Authentication**

This was explicitly chosen over JWT for Phase 1.

Reasons:

- Internal web application.
- One manager in Phase 1.
- React SPA works normally with cookie authentication.
- HttpOnly cookie avoids exposing authentication tokens to JavaScript.
- No refresh-token persistence is required.
- Logout is simple.
- Avoids unnecessary authentication infrastructure.

### Endpoints

```text
POST /api/v1/auth/login
POST /api/v1/auth/logout
GET  /api/v1/auth/me
```

### Login

Request:

```json
{
  "username": "manager01",
  "password": "********"
}
```

Successful response returns user information. Authentication cookie is established by the server.

The password is never persisted as plaintext.

### Authentication failures

- `401 Unauthorized` — invalid username/password.
- `403 Forbidden` — authenticated user is inactive.
- `401 Unauthorized` — unauthenticated access to protected API.

---

## 3. Current User

The server derives the current user from the authentication context.

Do not accept:

```json
{
  "createdBy": "...",
  "updatedBy": "..."
}
```

from the client.

The server sets:

```text
ProductionRecord.created_by
ProductionRecord.updated_by

PlanAdjustment.created_by
PlanAdjustment.applied_by
PlanAdjustment.reversed_by
```

based on the authenticated user.

---

## 4. Common Error Model

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

| HTTP | Meaning |
|---|---|
| 400 | Invalid request / validation |
| 401 | Not authenticated |
| 403 | Authenticated but not allowed |
| 404 | Resource not found |
| 409 | Business or concurrency conflict |
| 422 | Business rule validation |
| 500 | Unexpected server error |

---

## 5. Order APIs

### Create Order

```text
POST /api/v1/orders
```

Order creation includes the initial production plans.

Request:

```json
{
  "orderCode": "ORD-2026-001",
  "quantity": 100,
  "startDate": "2026-08-12",
  "dueDate": "2026-08-16",
  "productionPlans": [
    {
      "productionDate": "2026-08-12",
      "plannedQuantity": 20
    },
    {
      "productionDate": "2026-08-13",
      "plannedQuantity": 20
    },
    {
      "productionDate": "2026-08-14",
      "plannedQuantity": 20
    },
    {
      "productionDate": "2026-08-15",
      "plannedQuantity": 20
    },
    {
      "productionDate": "2026-08-16",
      "plannedQuantity": 20
    }
  ]
}
```

Initial validation:

```text
SUM(plannedQuantity) == Order.quantity
```

Order + initial ProductionPlans are created in one transaction.

### List Orders

```text
GET /api/v1/orders
```

Supported query parameters in Phase 1:

```text
status
page
pageSize
```

### Get Order

```text
GET /api/v1/orders/{orderId}
```

Response may include derived business values:

```text
totalActual
remaining
totalPlan
progressPercentage
```

These values are calculated and are not persisted.

---

## 6. Production Plan APIs

### Get Production Plans

```text
GET /api/v1/orders/{orderId}/production-plans
```

Response combines:

```text
ProductionPlan
ProductionRecord
Derived shortage/difference
```

Example:

```json
{
  "orderId": "uuid",
  "items": [
    {
      "id": "uuid",
      "productionDate": "2026-08-12",
      "initialPlannedQuantity": 20,
      "plannedQuantity": 20,
      "actualQuantity": 18,
      "shortageQuantity": 2,
      "difference": -2
    }
  ]
}
```

The frontend should not have to manually join several APIs to construct the daily production view.

---

## 7. Production Actual APIs

### Create Actual

```text
POST /api/v1/orders/{orderId}/production-records
```

Request:

```json
{
  "productionDate": "2026-08-12",
  "actualQuantity": 18
}
```

Rules:

- One ProductionRecord per Order + ProductionDate.
- `actualQuantity >= 0`.
- Actual may explicitly be zero.
- Current authenticated user is used for audit fields.
- Duplicate daily record is rejected.

### Edit Actual

```text
PUT /api/v1/orders/{orderId}/production-records/{productionRecordId}
```

Request:

```json
{
  "actualQuantity": 20
}
```

Actual is edited rather than accumulated.

There is intentionally no API such as:

```text
POST /production-records/{id}/add
```

### Total actual validation

For edit:

```text
NewTotal =
CurrentTotal
- OldActual
+ NewActual
```

Must satisfy:

```text
NewTotal <= Order.Quantity
```

If violated:

```text
422 Unprocessable Entity
```

Example error code:

```text
ACTUAL_EXCEEDS_ORDER_QUANTITY
```

### Order status

After create/edit:

```text
TotalActual == Order.Quantity
    → Completed

TotalActual < Order.Quantity
    → Incomplete
```

---

## 8. Adjustment Preview API

Preview never persists data.

```text
POST /api/v1/production-plans/{productionPlanId}/adjustments/preview
```

### Manual request

```json
{
  "adjustmentType": "Manual",
  "targets": [
    {
      "productionPlanId": "uuid",
      "addOnQuantity": 20
    }
  ]
}
```

### Automatic request

```json
{
  "adjustmentType": "Automatic"
}
```

Preview returns:

```text
sourceProductionPlanId
shortageQuantity
adjustmentType
items
totalAddOnQuantity
valid
```

Manual validation:

```text
SUM(Item.AddOnQuantity)
=
ShortageQuantity
```

Duplicate target ProductionPlan within one adjustment is not allowed.

---

## 9. Automatic Adjustment

Automatic adjustment calculates the allocation according to the agreed business rule.

Example:

```text
Shortage = 23

02/08 +6
03/08 +6
04/08 +6
05/08 +5
```

Preview only proposes the allocation.

It does not change `PlannedQuantity`.

---

## 10. Apply Adjustment API

```text
POST /api/v1/production-plans/{productionPlanId}/adjustments
```

The client submits the intended adjustment proposal.

The server MUST NOT blindly trust a previous preview.

Before applying, the server recalculates and validates:

```text
Current shortage
Current source plan
Current target plans
Current adjustment state
```

Apply transaction:

```text
Lock source plan
Lock target plans
Recalculate shortage
Validate proposal
Create PlanAdjustment
Create PlanAdjustmentItems
Increase target PlannedQuantity
Commit
```

All changes are atomic.

---

## 11. Preview Staleness

If state changed after preview:

```text
Preview:
Shortage = 20

Actual changes

Current shortage = 10
```

and the client tries to apply `+20`, the server rejects the outdated proposal.

Recommended response:

```text
409 Conflict
```

Error:

```json
{
  "code": "ADJUSTMENT_OUTDATED",
  "message": "The adjustment proposal is no longer valid because the source production state has changed."
}
```

The frontend must request a new preview.

The server must not silently alter the manager's submitted proposal.

---

## 12. Adjustment Apply Invariant

The following architectural/business decision is explicitly CHOSEN:

> A SourceProductionPlan may have at most one `Applied` PlanAdjustment at a time.

Lifecycle:

```text
Adjustment #001
    Applied
       ↓
    Reversed
       ↓
Adjustment #002
    Applied
```

But:

```text
Adjustment #001
    Applied

Adjustment #002
    Applied
```

for the same source ProductionPlan is not allowed.

If an active adjustment already exists:

```text
409 Conflict
```

Example:

```text
ACTIVE_ADJUSTMENT_EXISTS
```

This also protects against accidental duplicate application after network retries without introducing an Idempotency table in Phase 1.

---

## 13. Adjustment Reverse API

```text
POST /api/v1/plan-adjustments/{adjustmentId}/reverse
```

Reverse transaction:

```text
Lock Adjustment
Lock affected ProductionPlans
Validate status = Applied
Subtract AddOnQuantity
Set status = Reversed
Set reversed_by
Set reversed_at
Commit
```

Adjustment history is preserved.

There is intentionally no:

```text
PUT /plan-adjustments/{id}
```

and no editing of historical Adjustment records.

State rules:

| Current | Operation | Result |
|---|---|---|
| Applied | Reverse | Reversed |
| Reversed | Reverse | Reject |
| Applied | Edit | Reject |
| Reversed | Edit | Reject |

---

## 14. Adjustment History API

```text
GET /api/v1/orders/{orderId}/plan-adjustments
```

Returns historical adjustments including:

```text
source
shortage
adjustment type
status
target items
created by
created at
applied/reversed information
```

---

## 15. Actual Change After Adjustment

Example:

```text
Plan = 100
Actual = 80
Shortage = 20
```

Adjustment:

```text
+20
```

Later actual changes:

```text
Actual = 90
Shortage = 10
```

The existing Adjustment is not edited.

Instead:

```text
Applied Adjustment
        ↓
Reversed
        ↓
New Preview
        ↓
New Adjustment if required
```

Historical facts remain immutable.

---

## 16. Statistics APIs

### Order Statistics

```text
GET /api/v1/orders/{orderId}/statistics
```

Returns:

```text
orderQuantity
totalActual
remaining
totalPlan
progressPercentage
daily[]
```

Daily statistics may include:

```text
productionDate
plannedQuantity
actualQuantity
difference
shortageQuantity
cumulativePlan
cumulativeActual
```

All are derived values.

### Dashboard Statistics

```text
GET /api/v1/statistics/dashboard
```

Phase 1 can expose:

```text
totalOrders
incompleteOrders
completedOrders
totalOrderQuantity
totalActualQuantity
```

No Dashboard entity/table is required.

---

## 17. Idempotency Strategy

Phase 1 does NOT introduce a generic Idempotency table.

### Actual

Database uniqueness:

```text
UNIQUE(order_id, production_date)
```

prevents duplicate daily ProductionRecords.

### Adjustment

Duplicate application is prevented through:

```text
At most one Applied Adjustment per source ProductionPlan
```

plus transaction + row locking.

If the active Adjustment is reversed, a new Adjustment can be created.

This is sufficient for Phase 1 without over-engineering.

---

## 18. Concurrency Strategy

Chosen strategy:

```text
Database Transaction
+
Row Locking
```

### Actual create/edit

Lock:

```text
Order
```

Then:

```text
Read current total actual
Validate
Insert/Update ProductionRecord
Update Order.Status
Commit
```

### Apply Adjustment

Lock:

```text
Source ProductionPlan
+
Target ProductionPlans
```

Then validate and apply atomically.

### Reverse Adjustment

Lock:

```text
PlanAdjustment
+
Affected ProductionPlans
```

Then reverse atomically.

When multiple ProductionPlan rows are locked, they should be acquired in a deterministic order to reduce deadlock risk.

No explicit `version` column is introduced in Phase 1.

---

## 19. Transaction Boundaries

| API | Transaction |
|---|---|
| Login | No |
| Create Order | Yes |
| Get Order | No |
| Get Production Plans | No |
| Create Actual | Yes |
| Edit Actual | Yes |
| Preview Adjustment | No persistence |
| Apply Adjustment | Yes |
| Reverse Adjustment | Yes |
| Statistics | No |

Important atomic operations:

### Create Order

```text
Order
+
Initial ProductionPlans
```

### Apply Adjustment

```text
PlanAdjustment
+
PlanAdjustmentItems
+
ProductionPlan.PlannedQuantity
```

### Reverse Adjustment

```text
PlanAdjustment
+
Affected ProductionPlans
```

---

## 20. Complete Endpoint List

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

---

## 21. APIs Intentionally Excluded

No:

```text
POST /production-records/{id}/add
```

because Actual is a daily value, not a cumulative transaction.

No:

```text
DELETE /production-records/{id}
```

because actual correction uses edit.

No:

```text
PUT /plan-adjustments/{id}
```

because Adjustment is immutable history.

No:

```text
DELETE /orders/{id}
```

after production history exists.

No:

```text
/shortages
```

because Shortage is derived.

No Role/Permission APIs in Phase 1.

No direct User management API until user administration is actually required.

---

## 22. API DTO Boundary

API DTOs are not database entities.

The API should return business-oriented representations.

For example, the daily production UI should receive:

```json
{
  "productionDate": "2026-08-12",
  "plannedQuantity": 20,
  "actualQuantity": 18,
  "difference": -2,
  "shortageQuantity": 2
}
```

instead of forcing the frontend to directly reconstruct the business state from database-shaped objects.

Audit information is exposed only where the UI requires it.

---

## 23. End-to-End Business Flows

### Authentication

```text
POST /auth/login
        ↓
HttpOnly Authentication Cookie
        ↓
GET /auth/me
        ↓
Current User
```

### Create/Edit Actual

```text
Current User
        ↓
Create/Edit ProductionRecord
        ↓
Validate SUM(Actual) <= Order.Quantity
        ↓
Update Order.Status
```

### Adjustment

```text
Current User
        ↓
Adjustment Preview
        ↓
Manager confirms
        ↓
Apply Adjustment
        ↓
Lock source + targets
        ↓
Recalculate
        ↓
Validate
        ↓
Create immutable Adjustment
        ↓
Increase target plans
```

### Adjustment becoming invalid

```text
Applied Adjustment
        ↓
Actual changes
        ↓
Adjustment no longer suitable
        ↓
Reverse
        ↓
New Preview
        ↓
New Adjustment
```

---

# Final Step 4 Decisions

The following are now baseline decisions:

1. **Authentication:** HttpOnly Cookie Authentication.
2. **API versioning:** `/api/v1`.
3. **Current user:** derived from authentication context.
4. **Order creation:** Order + initial ProductionPlans in one transaction.
5. **Actual:** create once per Order + ProductionDate, edit existing record when correcting.
6. **Actual invariant:** total actual must never exceed Order.Quantity.
7. **Adjustment Preview:** non-persistent.
8. **Adjustment Apply:** server recalculates and validates the proposal.
9. **Stale Preview:** reject with `409 Conflict`; do not silently modify the proposal.
10. **Adjustment history:** immutable.
11. **Adjustment correction:** Reverse old Adjustment, then create a new one.
12. **Active Adjustment invariant:** one Applied Adjustment maximum per SourceProductionPlan.
13. **Idempotency:** no generic Idempotency table in Phase 1.
14. **Concurrency:** transaction + database row locking.
15. **Statistics:** derived, never persisted.
16. **DTOs:** business-oriented, not direct database entities.
17. **No over-engineered Role/Permission/User-management API in Phase 1.**

# STEP 4 — DONE

Next roadmap:

```text
Step 1 — Domain Model          DONE
Step 2 — Data Model            DONE
Step 3 — Database Schema       DONE
Step 4 — API Contract          DONE
Step 5 — Frontend Architecture NEXT
Step 6 — Implementation        PENDING
```
