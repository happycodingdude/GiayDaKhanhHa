# Production Management Web App — Step 5 Frontend Architecture

## Status

**STEP 5 — Frontend Architecture: DONE**

Date: 2026-08-12

---

# 1. Frontend Technology

Chosen stack:

```text
React
TypeScript
Vite
TanStack Router
TanStack Query
React Hook Form
Zod
```

The application is a React SPA.

Next.js is not used.

---

# 2. Architecture Overview

Recommended structure:

```text
React SPA
│
├── App Shell
│   ├── Authentication
│   ├── Layout
│   ├── Navigation
│   └── Global UI
│
├── Pages
│   ├── Dashboard
│   ├── Orders
│   ├── Order Detail
│   └── Login
│
├── Features
│   ├── Auth
│   ├── Orders
│   ├── Production
│   ├── Adjustments
│   └── Statistics
│
├── Shared
│   ├── UI
│   ├── Forms
│   ├── Dialogs
│   ├── Tables
│   └── Utilities
│
└── API Client
```

Architecture is feature-oriented rather than organizing the entire application only by technical type.

---

# 3. Folder Structure

Recommended:

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
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── types/
│   │   └── utils/
│   │
│   ├── orders/
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── pages/
│   │   ├── types/
│   │   └── utils/
│   │
│   ├── production/
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   └── types/
│   │
│   ├── adjustments/
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   └── types/
│   │
│   └── statistics/
│       ├── api/
│       ├── components/
│       ├── hooks/
│       └── types/
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

Rule:

> `features` owns business-specific UI and logic. `shared` contains only genuinely reusable functionality.

Do not turn `shared` into a catch-all folder.

---

# 4. Routing

## Chosen Router

**TanStack Router**

This was explicitly selected over React Router.

Reasons:

- Strong TypeScript type safety.
- Good fit with a TypeScript-heavy application.
- Consistent with TanStack Query.
- Type-safe route parameters and navigation.
- Suitable for the feature-based architecture.

---

# 5. Route Structure

Phase 1:

```text
/login

/dashboard

/orders
/orders/new
/orders/:orderId
```

Business entities such as ProductionPlan, ProductionRecord and Adjustment do not get independent top-level pages.

They are managed within the Order workflow.

---

# 6. Application Shell

After authentication:

```text
┌─────────────────────────────────────────────┐
│ Header                                      │
│ Logo        Production Management   User ▼ │
├────────────┬────────────────────────────────┤
│ Sidebar    │ Main Content                   │
│            │                                │
│ Dashboard  │                                │
│ Orders     │                                │
│            │                                │
└────────────┴────────────────────────────────┘
```

Phase 1 navigation:

```text
Dashboard
Orders
```

Production Plans, Actuals and Adjustments are accessed through Order Detail.

---

# 7. Server State

**TanStack Query** is the server-state solution.

Server state includes:

```text
Orders
Order Detail
Production Plans
Production Records
Adjustments
Statistics
Current User
```

Architecture:

```text
API
 ↓
TanStack Query Cache
 ↓
React UI
```

The frontend does not maintain an independent authoritative copy of these resources.

---

# 8. Local UI State

React state and local feature state are used for:

```text
Dialog open/close
Selected date
Adjustment target selection
Preview state
Form input
Filters
Pagination UI
Temporary UI state
```

These are not global application state.

---

# 9. Global Client State

No Redux or Zustand is introduced in Phase 1.

The chosen model is:

```text
TanStack Query
+
React State
+
React Context only when genuinely needed
```

This avoids unnecessary global-state infrastructure.

---

# 10. Authentication State

Current User is treated as server state.

Startup flow:

```text
Application startup
        ↓
GET /api/v1/auth/me
        ↓
Authenticated?
   ┌────┴────┐
   │         │
  Yes        No
   │         │
App Shell   /login
```

Logout:

```text
POST /api/v1/auth/logout
        ↓
Clear auth query
        ↓
Navigate /login
```

Authentication uses HttpOnly Cookie.

The frontend does not store authentication tokens in localStorage or sessionStorage.

---

# 11. API Client Architecture

Components must not directly call `fetch()`.

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
Backend API
```

Example:

```text
OrderPage
    ↓
useOrder(orderId)
    ↓
ordersApi.getOrder(orderId)
    ↓
apiClient.get(...)
```

This keeps UI independent of HTTP implementation details.

---

# 12. API Error Handling

Backend error contract:

```json
{
  "code": "ACTUAL_EXCEEDS_ORDER_QUANTITY",
  "message": "Total actual quantity cannot exceed order quantity.",
  "details": {}
}
```

Frontend maps business errors to meaningful UI feedback.

Examples:

```text
ACTUAL_EXCEEDS_ORDER_QUANTITY
→ Inline/form error

ADJUSTMENT_OUTDATED
→ Production data changed; request a new preview

ACTIVE_ADJUSTMENT_EXISTS
→ Active adjustment already exists

500
→ Generic unexpected-error message
```

Technical exception details must not be exposed to the user.

---

# 13. Order List Page

Route:

```text
/orders
```

Main columns:

```text
Order Code
Quantity
Production Period
Actual
Remaining
Progress
Status
Action
```

Filters:

```text
All
Incomplete
Completed
```

Pagination:

```text
page
pageSize
```

Pagination is server-side.

---

# 14. Create Order Page

Route:

```text
/orders/new
```

Flow:

```text
Order Information
        ↓
Production Period
        ↓
Production Plan
        ↓
Review
        ↓
Create
```

Order form:

```text
Order Code
Quantity
Start Date
Due Date
```

Initial Production Plan is created together with the Order.

Frontend validates:

```text
SUM(plannedQuantity) == order.quantity
```

Backend remains authoritative.

---

# 15. Order Detail Page

Route:

```text
/orders/:orderId
```

This is the central page of the application.

Recommended structure:

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

Order summary includes derived values:

```text
Plan
Actual
Remaining
Progress
Status
```

---

# 16. Production Timeline

Each ProductionPlan is rendered as a business-oriented daily row:

```text
Production Day
├── Date
├── Planned
├── Actual
├── Difference
├── Shortage
└── Action
```

Actions depend on state:

```text
No actual
→ Enter Actual

Actual exists
→ Edit Actual

Shortage > 0
→ Handle Shortage
```

---

# 17. Actual Input UX

Create:

```text
Enter Actual
```

Edit:

```text
Edit Actual
```

Dialog should show:

```text
Production Date
Planned Quantity
Actual Quantity
```

Important:

Actual is a value, not an increment.

The UI must not use a `+quantity` interaction for normal actual entry.

---

# 18. Actual = 0

Explicit zero is valid.

Frontend must distinguish:

```text
0
```

from:

```text
empty / null
```

Example request:

```json
{
  "actualQuantity": 0
}
```

---

# 19. Shortage UX

Example:

```text
Plan = 20
Actual = 18
Shortage = 2
```

UI:

```text
Shortage: 2
[Handle Shortage]
```

If there is no shortage, the adjustment action is not displayed.

---

# 20. Adjustment UX

Flow:

```text
Shortage
   ↓
Choose Adjustment Method
   ├── Manual
   └── Automatic
        ↓
Preview
        ↓
Review Proposal
        ↓
Apply
```

Preview is a separate UI state and is not treated as persisted data.

---

# 21. Manual Adjustment UI

Example:

```text
Shortage: 20

13/08    Current 20    Add [10]
14/08    Current 20    Add [10]
15/08    Current 20    Add [ 0]

Total Add-on: 20 / 20

[Preview]
```

Frontend should provide immediate feedback when:

```text
Total Add-on != Shortage
```

Backend still performs authoritative validation.

---

# 22. Automatic Adjustment UI

Example:

```text
Shortage: 23

System proposes:

13/08    +6
14/08    +6
15/08    +6
16/08    +5

Total    +23
```

Manager reviews the proposal before applying.

---

# 23. Apply Confirmation

Before applying:

```text
Confirm Production Adjustment

Shortage: 20

13/08    +10
14/08    +10

Total    +20

This action will be recorded in adjustment history.

[Cancel] [Apply Adjustment]
```

After success:

```text
Adjustment Applied
```

Affected queries are invalidated/refetched.

---

# 24. Adjustment Reversal UX

Adjustment History contains:

```text
Adjustment #001
Shortage: 20
Target: 14/08 +20
Status: Applied
```

Reverse requires confirmation.

After successful reverse:

```text
Applied → Reversed
```

Historical data remains visible.

---

# 25. Query Invalidation

### Actual create/edit

Invalidate:

```text
["orders", orderId]
["orders", orderId, "production-plans"]
["orders", orderId, "statistics"]
```

### Adjustment apply/reverse

Invalidate:

```text
["orders", orderId]
["orders", orderId, "production-plans"]
["orders", orderId, "statistics"]
["orders", orderId, "plan-adjustments"]
```

Goal:

```text
Mutation
 ↓
Invalidate affected queries
 ↓
Refetch
 ↓
Consistent UI
```

---

# 26. Form Architecture

Recommended:

```text
React Hook Form
+
Zod
```

Main forms:

```text
LoginForm
OrderForm
ProductionActualForm
ManualAdjustmentForm
```

Frontend validation improves UX.

Backend remains the business authority.

---

# 27. Type Architecture

Frontend types represent API contracts rather than database entities.

Examples:

```text
OrderDto
ProductionPlanDto
ProductionRecordDto
AdjustmentDto
AdjustmentItemDto
OrderStatisticsDto
CurrentUserDto
```

Form models may be different:

```text
CreateOrderForm
CreateOrderRequest
```

They do not have to be identical.

---

# 28. Feature Boundaries

## Auth

```text
Login
Logout
Current User
Authentication UI
```

## Orders

```text
Order list
Order creation
Order detail
Order summary
```

## Production

```text
Production plan
Actual input/edit
Daily production view
```

## Adjustments

```text
Shortage
Manual adjustment
Automatic adjustment
Preview
Apply
Reverse
History
```

## Statistics

```text
Dashboard statistics
Order statistics
Charts / KPI
```

---

# 29. Component Boundaries

Use components around meaningful UI responsibility:

```text
OrderSummary
ProductionTimeline
ProductionDayRow
ActualInputDialog
ShortagePanel
AdjustmentMethodSelector
ManualAdjustmentForm
AutomaticAdjustmentPreview
AdjustmentHistory
```

Do not create tiny components solely because a piece of JSX can be extracted.

---

# 30. Loading / Error / Empty States

Every server-driven screen must handle:

```text
Loading
Success
Empty
Error
```

Example:

```text
Loading → Skeleton

Empty → "No orders yet" + Create Order

Error → "Unable to load orders" + Try Again

Success → Data
```

Avoid blank screens during loading or error states.

---

# 31. Optimistic Updates

Do not use optimistic updates for:

```text
Actual create/edit
Adjustment Apply
Adjustment Reverse
```

These operations have cross-row business invariants and concurrency handling.

Chosen flow:

```text
Submit
 ↓
Loading
 ↓
Server transaction
 ↓
Success
 ↓
Refetch
```

This is safer and simpler for Phase 1.

---

# 32. Date Handling

Production business dates are date-only values:

```text
YYYY-MM-DD
```

Do not casually convert them into JavaScript `Date` objects.

For example:

```text
2026-08-12
```

must not be shifted because of browser timezone conversion.

Production dates:

```text
ProductionDate
StartDate
DueDate
```

are not timestamps.

---

# 33. Timestamp Handling

Backend timestamps use:

```text
timestamptz
```

and UTC.

Frontend converts timestamps such as:

```text
createdAt
appliedAt
reversedAt
```

to the user's local display timezone.

Do not timezone-convert business dates.

---

# 34. Responsive Scope

Phase 1 is:

**Desktop-first**

Primary target:

```text
Desktop
Laptop
```

Basic tablet support is desirable.

Full mobile optimization is not a Phase 1 priority.

---

# 35. Frontend Security

Do not store authentication tokens in:

```text
localStorage
sessionStorage
```

Authentication is handled by HttpOnly Cookie.

Frontend does not decode or interpret authentication tokens.

Server remains the authority for identity and authorization.

---

# 36. Frontend Architecture Diagram

```text
                         Backend API
                              │
                              ▼
                     ┌─────────────────┐
                     │   API Client    │
                     └────────┬────────┘
                              │
                    ┌─────────▼─────────┐
                    │  TanStack Query   │
                    │   Server State    │
                    └─────────┬─────────┘
                              │
              ┌───────────────┼────────────────┐
              │               │                │
              ▼               ▼                ▼
           Orders         Production       Adjustments
              │               │                │
              └───────────────┼────────────────┘
                              │
                              ▼
                       Order Detail
                              │
             ┌────────────────┼────────────────┐
             ▼                ▼                ▼
          Actual           Shortage        Statistics
             │                │                │
             └────────────────┼────────────────┘
                              ▼
                         React UI
```

---

# 37. Final Step 5 Decisions

The following are now baseline decisions:

1. Frontend is a React + TypeScript + Vite SPA.
2. Routing uses **TanStack Router**.
3. Server state uses **TanStack Query**.
4. No Redux/Zustand in Phase 1.
5. Authentication uses HttpOnly Cookie as established in Step 4.
6. Current User is server state.
7. API access is centralized through an API client.
8. Business-specific frontend code is feature-based.
9. Production Plans, Actuals and Adjustments are accessed through the Order workflow rather than top-level routes.
10. Order Detail is the central production-management screen.
11. Forms use React Hook Form + Zod.
12. Frontend DTOs represent API contracts, not database entities.
13. Backend remains the authoritative business-validation layer.
14. No optimistic updates for critical production mutations.
15. Production dates are treated as date-only values.
16. Backend timestamps are UTC and converted for display.
17. Desktop-first is the Phase 1 responsive target.
18. Authentication tokens are never stored in browser local/session storage.
19. Loading, empty and error states are required for server-driven UI.
20. Query invalidation/refetch is used after mutations.

---

# 38. Roadmap

```text
Step 1 — Domain Model          DONE
Step 2 — Data Model            DONE
Step 3 — Database Schema       DONE
Step 4 — API Contract          DONE
Step 5 — Frontend Architecture DONE
Step 6 — Implementation        NEXT
```

Step 6 will turn the approved business/domain/database/API/frontend baseline into the actual implementation plan and code structure.
