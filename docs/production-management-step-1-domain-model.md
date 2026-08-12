# Production Management Web App — Step 1 Summary
## Domain Model

> Status: **DONE / BASELINE CHỐT**
>
> Technical stack và business requirements được kế thừa từ prompt continuation đã lưu. Step này tập trung chốt Domain Model trước khi chuyển sang Data Model.

---

## 1. Vai trò & nguyên tắc

AI đóng vai:
- Business Analyst
- Solution Architect / System Design Expert

Nguyên tắc:
- Hiểu nghiệp vụ trước technical.
- Business Rule đã chốt là baseline.
- Không over-engineering.
- Phân biệt Domain Model, Data Model, API Contract, Technical Architecture.
- Chỉ dừng để hỏi khi gặp business decision cần chốt.

---

# 2. Domain Concepts

Domain model chính:

```text
Order
 ├── ProductionPlan
 ├── ProductionRecord
 └── PlanAdjustment
       └── PlanAdjustmentItem
```

Không tạo `AddOn` entity riêng.

Không tạo `AdjustmentHistory` entity riêng ở thời điểm này.

Lý do:
- `PlanAdjustment` bản thân là business record mang tính lịch sử.
- `PlanAdjustmentItem` mô tả phần add-on được phân bổ vào từng ngày.
- Tách thêm history riêng dễ tạo duplicate source cho cùng một adjustment.

---

# 3. Order

`Order` là Aggregate Root chính.

```text
Order
├── Id
├── OrderCode
├── Quantity
├── StartDate
├── DueDate
└── Status
```

### Rules

- `Quantity > 0`
- `StartDate <= DueDate`
- Status chỉ có:
  - `Incomplete`
  - `Completed`
- Khi `TotalActual == Order.Quantity` → `Completed`.
- Khi TotalActual giảm xuống dưới Order.Quantity → quay lại `Incomplete`.
- Adjustment không bao giờ làm tăng `Order.Quantity`.

### Không lưu trực tiếp

Không coi các field sau là source of truth:
- TotalActual
- RemainingQuantity
- Progress
- TotalPlan

Các giá trị này được derive.

```text
TotalActual = SUM(ProductionRecord.ActualQuantity)

Remaining = Order.Quantity - TotalActual

Progress = TotalActual / Order.Quantity
```

---

# 4. ProductionPlan

Mỗi Order có nhiều plan theo ngày.

```text
ProductionPlan
├── Id
├── OrderId
├── ProductionDate
├── InitialPlannedQuantity
└── PlannedQuantity
```

### Cardinality

```text
Order 1 ──── * ProductionPlan
```

### Constraint

```text
Unique(OrderId, ProductionDate)
```

=> Một Order chỉ có **1 ProductionPlan cho một ngày**.

### Ý nghĩa quantity

`InitialPlannedQuantity`:
- Kế hoạch ban đầu của ngày.

`PlannedQuantity`:
- Kế hoạch hiện tại sau các adjustment.

Ví dụ:

```text
InitialPlan = 100
Adjustment  = +20
CurrentPlan = 120
```

Tổng current plan có thể lớn hơn Order.Quantity sau adjustment.

---

# 5. ProductionRecord

Actual sản xuất cuối ngày.

```text
ProductionRecord
├── Id
├── OrderId
├── ProductionDate
└── ActualQuantity
```

### Cardinality

```text
Order 1 ──── * ProductionRecord
```

### Constraint

```text
Unique(OrderId, ProductionDate)
```

### Business rule

Một Order chỉ có **1 ProductionRecord cho một ngày**.

Quản lý:
- Cuối ngày nhập actual một lần.
- Nếu nhập sai → edit record hiện tại.
- Không tạo nhiều record để cộng dồn trong cùng ngày.

Ví dụ:

```text
01/08 → Actual = 80
```

Sửa:

```text
80 → 85
```

vẫn chỉ có một record.

---

# 6. Plan và Actual độc lập

Không model Actual nằm bên trong ProductionPlan.

Model:

```text
Order
 ├── ProductionPlan
 │      └── ProductionDate + PlannedQuantity
 │
 └── ProductionRecord
        └── ProductionDate + ActualQuantity
```

Ghép theo:

```text
OrderId + ProductionDate
```

để tính:

```text
Shortage = Plan - Actual
DailyDifference = Actual - Plan
```

Nếu chưa có ProductionRecord thì đó là **chưa nhập actual**, không mặc định đồng nghĩa với Actual = 0.

---

# 7. PlanAdjustment

Dùng để xử lý shortage.

```text
PlanAdjustment
├── Id
├── OrderId
├── SourceDate
├── ShortageQuantity
├── AdjustmentType
├── CreatedAt
└── AppliedAt
```

### AdjustmentType

Có 2 loại:

```text
ManualTargetDate
EvenDistribution
```

### Option 1

Ví dụ shortage 20:

```text
05/08 → +20
```

### Option 2

Ví dụ shortage 23, còn 4 ngày:

```text
02/08 → +6
03/08 → +6
04/08 → +6
05/08 → +5
```

---

# 8. PlanAdjustmentItem

Một adjustment có thể phân bổ vào nhiều ngày.

```text
PlanAdjustment
    1 ──── * PlanAdjustmentItem
```

```text
PlanAdjustmentItem
├── Id
├── PlanAdjustmentId
├── TargetDate
└── AddOnQuantity
```

### Invariants

```text
ShortageQuantity > 0

AddOnQuantity > 0

SUM(PlanAdjustmentItem.AddOnQuantity)
    =
PlanAdjustment.ShortageQuantity
```

Adjustment không được làm tăng Order.Quantity.

---

# 9. Aggregate Boundary

Đề xuất Aggregate:

```text
Order Aggregate
│
├── Order
├── ProductionPlan
├── ProductionRecord
└── PlanAdjustment
      └── PlanAdjustmentItem
```

Aggregate boundary dùng để bảo vệ business invariants.

Không có nghĩa application phải luôn load toàn bộ graph.

---

# 10. Core Domain Invariants

## Order

```text
Quantity > 0
StartDate <= DueDate
Status ∈ {Incomplete, Completed}
```

## ProductionPlan

```text
PlannedQuantity >= 0
Unique(OrderId, ProductionDate)
```

Initial planning:

```text
SUM(InitialPlan)
    =
Order.Quantity
```

## ProductionRecord

```text
ActualQuantity >= 0
Unique(OrderId, ProductionDate)
```

Quan trọng nhất:

```text
SUM(All Actual)
    <=
Order.Quantity
```

## PlanAdjustment

```text
ShortageQuantity > 0

SUM(All AddOnQuantity)
    =
ShortageQuantity
```

Target date phải tuân thủ business rule đã chốt về ngày được phép bù.

---

# 11. Actual Edit — Concurrency / Validation

Khi edit actual của một ngày, không chỉ validate giá trị mới độc lập.

Phải tính:

```text
NewTotalActual
=
CurrentTotalActual
-
OldRecordActual
+
NewRecordActual
```

Ví dụ:

```text
Order = 1,000

Day 1 = 100
Day 2 = 200
Day 3 = 300

Total = 600
```

Edit Day 2:

```text
200 → 450

NewTotal = 600 - 200 + 450
         = 850
```

Nếu NewTotal > Order.Quantity → reject.

---

# 12. QUYẾT ĐỊNH ĐÃ CHỐT: Actual Edit sau Adjustment

**Chốt Option B.**

Nếu một ngày đã có shortage và shortage đã được Apply Adjustment, sau đó quản lý sửa Actual khiến shortage thay đổi/biến mất:

> Hệ thống cho phép sửa Actual và phải xử lý **reversal / recalculation adjustment liên quan**.

Ví dụ:

```text
Order = 1,000

01/08:
Plan   = 100
Actual = 80
Shortage = 20
```

Đã Apply:

```text
05/08 → +20
```

Sau đó sửa:

```text
01/08:
Actual 80 → 100
```

Shortage mới = 0.

Hệ thống không được để adjustment +20 tồn tại một cách vô nghĩa.

### Domain implication

Adjustment lifecycle phải hỗ trợ việc:
- xác định adjustment nào bị ảnh hưởng,
- tính lại/reverse phần add-on liên quan,
- cập nhật current plan,
- giữ được lịch sử/audit,
- đảm bảo transaction atomic.

Chi tiết cơ chế reversal/recalculation sẽ được thiết kế ở các bước Data Model / Application / API, nhưng **business behavior đã chốt là Option B**.

---

# 13. Order Status

Chỉ có:

```text
Incomplete
Completed
```

Rule:

```text
TotalActual == Order.Quantity
    → Completed
```

Nếu edit actual làm:

```text
TotalActual < Order.Quantity
```

thì:

```text
→ Incomplete
```

Không cho phép TotalActual > Order.Quantity.

---

# 14. Derived Data

Không lưu cứng nếu có thể tính từ source of truth:

```text
TotalActual
Remaining
Progress
DailyShortage
DailyDifference
CumulativeActual
CumulativePlan
```

Source of truth chính:

```text
Order.Quantity
ProductionPlan.PlannedQuantity
ProductionRecord.ActualQuantity
PlanAdjustment / PlanAdjustmentItem
```

Lịch sử adjustment phải được giữ để giải thích vì sao current plan thay đổi.

---

# 15. Transaction Boundaries

## Create Order

```text
BEGIN
  Create Order
  Create Initial Plans
COMMIT
```

Invariant:

```text
SUM(InitialPlan) = Order.Quantity
```

## Record / Edit Production

```text
BEGIN
  Load Order
  Load today's ProductionRecord
  Create OR Update record
  Validate TotalActual <= Order.Quantity
  Recalculate Order Status
  Handle affected Adjustment if required
COMMIT
```

## Preview Adjustment

Không persist:

```text
Load current state
      ↓
Calculate shortage
      ↓
Generate proposal
      ↓
Return preview
```

## Apply Adjustment

```text
BEGIN
  Validate current state
  Validate proposed adjustment
  Create PlanAdjustment
  Create PlanAdjustmentItems
  Update ProductionPlan
  Commit

ROLLBACK nếu bất kỳ bước nào fail
```

---

# 16. Domain Model Diagram

```text
                           ┌─────────────────────┐
                           │       ORDER         │
                           │─────────────────────│
                           │ Id                  │
                           │ OrderCode           │
                           │ Quantity            │
                           │ StartDate            │
                           │ DueDate              │
                           │ Status               │
                           └──────────┬──────────┘
                                      │
             ┌────────────────────────┼────────────────────────┐
             │                        │                        │
             ▼                        ▼                        ▼
   ┌─────────────────┐      ┌──────────────────┐     ┌──────────────────┐
   │ ProductionPlan  │      │ProductionRecord  │     │ PlanAdjustment   │
   │─────────────────│      │──────────────────│     │──────────────────│
   │ Id              │      │ Id               │     │ Id               │
   │ OrderId         │      │ OrderId          │     │ OrderId          │
   │ ProductionDate  │      │ ProductionDate   │     │ SourceDate       │
   │ InitialPlanQty  │      │ ActualQuantity   │     │ ShortageQuantity │
   │ PlannedQuantity │      └──────────────────┘     │ AdjustmentType   │
   └─────────────────┘                               │ CreatedAt        │
                                                     │ AppliedAt        │
                                                     └────────┬─────────┘
                                                              │
                                                              ▼
                                                   ┌────────────────────┐
                                                   │PlanAdjustmentItem  │
                                                   │────────────────────│
                                                   │ Id                 │
                                                   │ AdjustmentId       │
                                                   │ TargetDate         │
                                                   │ AddOnQuantity      │
                                                   └────────────────────┘
```

---

# 17. Step 1 — DONE

Đã chốt:

- `Order` là Aggregate Root.
- `ProductionPlan` theo Order + Date.
- `ProductionRecord` theo Order + Date.
- Một Order/ngày chỉ có 1 Plan.
- Một Order/ngày chỉ có 1 Actual Record.
- Actual được edit trên record hiện tại.
- `PlanAdjustment` lưu lịch sử adjustment.
- `PlanAdjustmentItem` lưu phân bổ add-on.
- Không có AddOn entity riêng.
- Không có AdjustmentHistory entity riêng ở thời điểm này.
- Phân biệt Initial Plan và Current Plan.
- Adjustment không tăng Order Quantity.
- Total Actual không vượt Order Quantity.
- Status tự động theo Total Actual.
- Preview không persist.
- Apply persist trong transaction.
- Actual edit sau Adjustment: **Option B — reversal/recalculation adjustment liên quan**.
- Domain Model sẵn sàng chuyển sang **Step 2 — Data Model**.

## Next Step

```text
Domain Model
     ↓
[ DONE ]
     ↓
Data Model
     ↓
Database Schema
     ↓
API Contract
     ↓
Frontend Architecture
     ↓
Implementation
```
