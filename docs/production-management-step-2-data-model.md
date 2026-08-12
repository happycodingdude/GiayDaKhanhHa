# Production Management Web App — Step 2 Summary
## Data Model

> Status: **DONE / BASELINE CHỐT**

## 1. Data Model

5 bảng chính:

```text
orders
  ├── production_plans
  ├── production_records
  └── plan_adjustments
          └── plan_adjustment_items
```

Mapping:

| Domain | Table |
|---|---|
| Order | orders |
| ProductionPlan | production_plans |
| ProductionRecord | production_records |
| PlanAdjustment | plan_adjustments |
| PlanAdjustmentItem | plan_adjustment_items |

Không tạo bảng riêng cho AddOn, AdjustmentHistory, Shortage, Progress, Remaining.

---

## 2. orders

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

Rules:
- `quantity > 0`
- `start_date <= due_date`
- status: `Incomplete` / `Completed`
- `order_code` UNIQUE

Không lưu trực tiếp TotalActual, Remaining, Progress, TotalPlan.

Derived:

```text
TotalActual = SUM(ProductionRecord.ActualQuantity)
Remaining = Order.Quantity - TotalActual
Progress = TotalActual / Order.Quantity
```

---

## 3. production_plans

```text
id
order_id
production_date
initial_planned_quantity
planned_quantity
created_at
updated_at
```

Relationship:

```text
orders 1 ─── * production_plans
```

Constraint:

```text
UNIQUE(order_id, production_date)
```

Một Order chỉ có một Plan/ngày.

`initial_planned_quantity` = kế hoạch ban đầu.

`planned_quantity` = kế hoạch hiện tại sau adjustment.

Ví dụ:

```text
Initial = 100
Adjustment = +20
Current = 120
```

---

## 4. production_records

```text
id
order_id
production_date
actual_quantity
created_at
updated_at
```

Constraint:

```text
UNIQUE(order_id, production_date)
```

Một Order chỉ có một Actual Record/ngày.

Quản lý nhập cuối ngày; nếu sai thì edit record hiện tại, không tạo record thứ hai.

---

## 5. Plan và Actual

Plan và Actual là hai entity độc lập:

```text
Order
 ├── ProductionPlan
 └── ProductionRecord
```

Ghép theo Order + Date để tính:

```text
Shortage = Plan - Actual
DailyDifference = Actual - Plan
```

Không có ProductionRecord nghĩa là chưa nhập actual, không tự động coi là 0.

---

## 6. plan_adjustments

Final:

```text
id
source_production_plan_id
shortage_quantity
adjustment_type
status
created_at
applied_at
reversed_at
```

### Quyết định quan trọng

**Không lưu `order_id` trong `plan_adjustments`.**

Quan hệ:

```text
plan_adjustment
 → source_production_plan
   → order
```

Lý do:
- Tránh duplicate relationship.
- Giữ model normalized.
- Chuẩn bị tốt cho nhiều Order/ngày.

`source_production_plan_id` xác định trực tiếp Plan phát sinh shortage.

---

## 7. plan_adjustment_items

```text
id
plan_adjustment_id
production_plan_id
add_on_quantity
```

Relationship:

```text
plan_adjustments 1 ─── * plan_adjustment_items
```

`production_plan_id` là Target Plan nhận add-on.

Không chỉ lưu target date vì tương lai một ngày có thể có nhiều Order.

Invariant:

```text
SUM(items.add_on_quantity)
=
plan_adjustments.shortage_quantity
```

---

## 8. Adjustment Lifecycle

Preview không persist:

```text
Preview → Calculate → Return proposal
```

Apply mới tạo persistent Adjustment.

Lifecycle:

```text
Applied → Reversed
```

Applied Adjustment là immutable historical record.

Không mutate hoặc delete history.

---

## 9. Actual Edit + Adjustment

Đã chốt **Option B**:

> Cho phép edit Actual sau khi Adjustment đã Apply; hệ thống phải xử lý reversal/recalculation adjustment liên quan.

Ví dụ:

```text
Shortage = 20
Adjustment = +20
```

Actual thay đổi khiến shortage = 0:

```text
Adjustment #001 → Reversed
```

Nếu shortage mới = 10:

```text
#001 +20 → Reversed
#002 +10 → Applied
```

Không sửa Adjustment #001 thành +10.

---

## 10. Current Plan

`production_plans.planned_quantity` là source of truth của Current Plan.

Ví dụ:

```text
Initial = 100
+20
Current = 120
```

Reverse:

```text
Current = 100
```

Recalculation mới +10:

```text
Current = 110
```

Adjustment history giải thích vì sao Current Plan thay đổi.

---

## 11. Database Constraints

### orders

```text
PK(id)
UNIQUE(order_code)
CHECK(quantity > 0)
CHECK(start_date <= due_date)
```

### production_plans

```text
PK(id)
FK(order_id)
UNIQUE(order_id, production_date)
CHECK(initial_planned_quantity >= 0)
CHECK(planned_quantity >= 0)
```

### production_records

```text
PK(id)
FK(order_id)
UNIQUE(order_id, production_date)
CHECK(actual_quantity >= 0)
```

### plan_adjustments

```text
PK(id)
FK(source_production_plan_id)
CHECK(shortage_quantity > 0)
```

### plan_adjustment_items

```text
PK(id)
FK(plan_adjustment_id)
FK(production_plan_id)
CHECK(add_on_quantity > 0)
```

---

## 12. Cross-row Invariants

Không nhét vào CHECK:

```text
SUM(ProductionRecord.ActualQuantity)
<= Order.Quantity
```

và:

```text
SUM(AdjustmentItems)
= Adjustment.ShortageQuantity
```

Các invariant này do:

```text
Application
+
Transaction
+
Concurrency Control
```

bảo vệ.

---

## 13. Delete Strategy

Không hard-delete Order sau khi đã có production data.

Không dùng:

```text
DELETE Order → CASCADE history
```

Không thêm soft-delete hàng loạt ở Phase 1.

Nếu sau này cần Cancel Order thì đây sẽ là business state mới; hiện chưa thêm.

---

## 14. Data Types

Quantity là integer:

```text
Order.Quantity
ProductionPlan.InitialPlannedQuantity
ProductionPlan.PlannedQuantity
ProductionRecord.ActualQuantity
PlanAdjustment.ShortageQuantity
PlanAdjustmentItem.AddOnQuantity
```

Business dates dùng `date`:

```text
start_date
due_date
production_date
```

Audit timestamps dùng timezone-aware timestamp:

```text
created_at
updated_at
applied_at
reversed_at
```

---

## 15. Audit

Persistent entities có:

```text
created_at
updated_at
```

Adjustment có thêm:

```text
applied_at
reversed_at
```

Chưa thêm `created_by`, `applied_by`, `reversed_by` vì Authentication/Authorization chưa chốt.

---

## 16. Final Data Model

```text
┌──────────────────────┐
│       orders         │
├──────────────────────┤
│ id PK                │
│ order_code UNIQUE    │
│ quantity             │
│ start_date           │
│ due_date             │
│ status               │
│ created_at           │
│ updated_at           │
└──────────┬───────────┘
           │
     ┌─────┴─────────────────────┐
     │                           │
     ▼                           ▼
┌──────────────────────┐  ┌──────────────────────┐
│ production_plans     │  │ production_records   │
├──────────────────────┤  ├──────────────────────┤
│ id PK                │  │ id PK                │
│ order_id FK          │  │ order_id FK          │
│ production_date      │  │ production_date      │
│ initial_plan_qty     │  │ actual_quantity      │
│ planned_quantity     │  │ created_at           │
│ created_at           │  │ updated_at           │
│ updated_at           │  └──────────────────────┘
└──────────┬───────────┘
           │ source / target
           ▼
┌──────────────────────────────┐
│       plan_adjustments       │
├──────────────────────────────┤
│ id PK                        │
│ source_production_plan_id FK │
│ shortage_quantity            │
│ adjustment_type              │
│ status                       │
│ created_at                   │
│ applied_at                   │
│ reversed_at                  │
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│    plan_adjustment_items     │
├──────────────────────────────┤
│ id PK                        │
│ plan_adjustment_id FK        │
│ production_plan_id FK        │
│ add_on_quantity              │
└──────────────────────────────┘
```

---

# 17. Step 2 — DONE

Đã chốt:
- 5 bảng chính.
- Order là root data entity.
- Plan và Actual tách riêng.
- Unique Order + Date cho Plan và Actual.
- Initial Plan và Current Plan tách biệt.
- Adjustment reference trực tiếp Source ProductionPlan.
- Adjustment Item reference trực tiếp Target ProductionPlan.
- `order_id` **không lưu trong `plan_adjustments`**.
- Applied Adjustment immutable.
- Reversal không delete history.
- Recalculation tạo Adjustment mới.
- Không có AddOn/Shortage/Progress/Remaining/AdjustmentHistory table riêng.
- Cross-row invariants do Application + Transaction + Concurrency bảo vệ.
- Không cascade delete production history.
- Quantity là integer.
- Business date dùng `date`.
- Audit time dùng timezone-aware timestamp.

## Next Step

```text
Domain Model
     ↓ DONE
Data Model
     ↓ DONE
Database Schema
     ↓
API Contract
     ↓
Frontend Architecture
     ↓
Implementation
```

Next: **Step 3 — Database Schema**.
