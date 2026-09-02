# CR-01 — Intraday Production Entries & Day Close (Xuất hàng)

> **Loại tài liệu:** Change Request — áp lên baseline Step 1 → Step 5 đã DONE  
> **Ngày:** 2026-09-02  
> **Trạng thái:** Nghiệp vụ đã chốt · Tác động kỹ thuật đã chốt · Sẵn sàng triển khai  
> **Vai trò soạn:** Product Owner
>
> ⚠ **CR này đảo ngược một số quyết định đã chốt ở Step 1, Step 3, Step 4, Step 5 và spec Màn hình 5.** Xem Mục 3. Theo quy tắc "Conflict rule" trong `Production_Management_Implementation_Prompt_for_Claude.md`, các quyết định cũ bị CR này ghi đè **không được dùng lại** sau khi CR được duyệt.

---

# 1. Yêu cầu nghiệp vụ

Trong một ngày sản xuất, quản lý ghi nhận sản lượng thực tế **nhiều lần** theo một mốc thời gian **cấu hình được** (ví dụ mỗi 1 tiếng). Tổng các lần đã nhập trong ngày **không được vượt kế hoạch của ngày đó**. Mỗi lần nhập, quản lý xem được **lịch sử đã nhập trước đó của đơn trong ngày**.

Cuối ngày có nút **Xuất hàng** để **đóng ngày sản xuất của đơn**. Lúc này mới ra con số tổng thực tế đã nhập, so với kế hoạch, và **từ đó mới hiển thị Xử lý thiếu**.

---

# 2. Tóm tắt tác động

Baseline hiện tại mô hình hoá Actual là **một giá trị duy nhất trong ngày** (`ProductionRecord`, `Unique(order_id, production_date)`, "Actual is a value, not an increment").

CR-01 đổi Actual thành **một tập bản ghi trong ngày cộng với một hành động chốt sổ**.

| Tầng | Mức tác động |
|---|---|
| Domain Model (Step 1) | **Cao** — thêm khái niệm ProductionDay có vòng đời, đổi cardinality của Actual |
| Data Model / DB Schema (Step 2, 3) | **Cao** — thêm 2 bảng, sửa 1 bảng |
| API Contract (Step 4) | **Cao** — thay nhóm endpoint Actual, thêm endpoint Close & Settings |
| Frontend Architecture (Step 5) | **Trung bình** — thêm route/feature/query key, đổi invalidation |
| Screen Specs | **Cao** với MH5, **trung bình** với MH4/MH6/Dashboard |
| Implementation Prompt | **Cao** — nhiều mục baseline phải viết lại |

---

# 3. Các quyết định baseline bị CR-01 ghi đè

Đây là mục quan trọng nhất của tài liệu. Mỗi dòng là một quyết định **đã chốt trước đây** nay bị thay thế.

| # | Tài liệu & vị trí | Quyết định cũ | Sau CR-01 |
|---|---|---|---|
| OV-1 | Step 1 §5, Step 3 §4.4, Step 4 §7 | Một Order chỉ có **1 ProductionRecord cho một ngày**; "Không tạo nhiều record để cộng dồn trong cùng ngày" | Một ngày có **N bản ghi** (`production_entries`). `ProductionRecord` được **đổi tên thành `ProductionDay`** (bảng `production_days`), đại diện cho ngày sản xuất |
| OV-2 | Step 4 §21, Step 5 §17 | Cố ý **không có** API `POST /production-records/{id}/add`; "Actual is a value, not an increment"; UI không được dùng tương tác `+quantity` | **Đảo ngược.** Actual **chính là** increment. UI dùng đúng mô hình cộng dồn |
| OV-3 | MH5 spec §5 "Vượt kế hoạch" | Thực tế **được phép** vượt kế hoạch ngày, miễn không vượt tổng đơn | **Cấm.** Tổng thực tế ngày ≤ kế hoạch ngày. `DailyDifference` từ nay luôn ≤ 0 |
| OV-4 | Step 4 §7 "Order status", Step 1 §13 | Sau mỗi create/edit Actual → đánh giá lại và chuyển `Completed` | Chỉ đánh giá **tại thời điểm Close** |
| OV-5 | Step 1 §7, Step 4 §8, MH5 spec §8 | Shortage là derived, phát sinh ngay khi Actual < Plan | Shortage **chỉ tồn tại khi ngày đã Closed**. Ngày Open không có shortage |
| OV-6 | Step 4 §21 | Cố ý **không có** `DELETE /production-records/{id}` vì sửa Actual bằng edit | Có `DELETE` cho **entry** (chỉ khi ngày Open). Ngày Closed vẫn không xoá được gì |
| OV-7 | MH5 spec §4.5 | Ngày đã qua: cho nhập/sửa bình thường | Ngày đã qua **chưa Close**: cho nhập bù + Close muộn, có cảnh báo. Ngày đã Close: khoá tuyệt đối |
| OV-8 | MH5 spec §4.6 | Sửa sản lượng **bắt buộc nhập lý do** + ghi Activity | Sửa/xoá entry khi ngày Open: **không bắt buộc lý do**, vẫn ghi `production_entry_logs` |
| OV-9 | Step 3 §4.4 | `actual_quantity NOT NULL`, `>= 0`; "actual_quantity = 0 is valid if explicitly entered" | `actual_quantity` **nullable**, chỉ có giá trị khi ngày Closed |
| OV-10 | Step 1 §6 | "Nếu chưa có ProductionRecord thì đó là chưa nhập actual" | Vẫn đúng, nhưng row `production_days` nay được tạo **lazily ở lần entry đầu tiên** hoặc khi Close-với-0 |
| OV-11 | Step 3 §4.4, §16, §17, Step 4 §7, §21, Step 1 §5 | Entity/bảng tên `ProductionRecord` / `production_records` | **Đổi tên** thành `ProductionDay` / `production_days`. Tên cũ không được dùng lại ở bất kỳ tài liệu hay code nào |
| OV-12 | **Implementation Prompt §47 — NON-NEGOTIABLE BUSINESS RULES** | Danh sách "không bao giờ được thay đổi" gồm: `Actual is a value, not an increment`; `One ProductionRecord per Order + ProductionDate`; `Actual = 0 is valid`; `Order status derived from total actual` | **4 dòng này bị CR-01 gỡ khỏi danh sách non-negotiable.** Xem 14.1 để biết danh sách thay thế. Các dòng còn lại của §47 **giữ nguyên hiệu lực** |
| OV-13 | Implementation Prompt §31 | "Correct: `Actual Quantity: 18`. Incorrect: `+5`" | **Đảo ngược.** `+5` chính là tương tác đúng của MH5 sau CR-01 |
| OV-14 | Implementation Prompt §43 | Test list nhóm Actual: `One record per Order + ProductionDate`, `Edit replaces old value`, `Actual = 0 is valid`; nhóm Shortage: `Shortage = max(Plan - Actual, 0)` | **Thay thế** bằng test list ở 14.8 |

## 3.1. Hệ quả lớn: kịch bản Option B gần như biến mất

Step 1 §12 chốt **Option B**: khi quản lý sửa Actual của một ngày đã có Adjustment Applied, hệ thống phải reverse/recalculate adjustment liên quan. Step 4 §15 và §11 (`ADJUSTMENT_OUTDATED`) xây dựng trên giả định này.

Sau CR-01:

```text
Adjustment chỉ tạo được khi source day đã Closed
        +
Ngày đã Closed thì Actual bị khoá vĩnh viễn
        ⇓
Shortage của source day KHÔNG BAO GIỜ thay đổi sau khi Adjustment được Apply
```

**Kết luận:** luồng "Actual thay đổi làm Adjustment mất hiệu lực" **không còn xảy ra được**.

**Xử lý đã chốt:**

- **Giữ** API `POST /plan-adjustments/{id}/reverse` — vẫn cần cho trường hợp quản lý chọn nhầm ngày bù và muốn làm lại.
- **Giữ** `ADJUSTMENT_OUTDATED` và bước server recalculate trước khi Apply — vẫn cần vì shortage có thể đã được xử lý bởi một request khác, hoặc target day đã bị Close trong lúc quản lý còn đang xem preview.
- **Gỡ khỏi tài liệu** toàn bộ mô tả kịch bản "Actual thay đổi làm Adjustment mất hiệu lực". Chi tiết những đoạn phải xoá: xem Mục 13.

Đây là **điểm đơn giản hoá đáng kể** mà CR-01 mang lại, không phải chi phí.

---

# 4. Domain Model — Delta (Step 1)

## 4.1. Khái niệm mới: ProductionDay

```text
ProductionDay              ← ĐỔI TÊN từ ProductionRecord (bảng production_days)
├── Id
├── OrderId
├── ProductionDate
├── Status                 ← MỚI: Open | Closed
├── ActualQuantity         ← ĐỔI: nullable, chỉ set khi Close (snapshot)
├── ClosedAt               ← MỚI
├── ClosedBy               ← MỚI
├── CreatedBy / UpdatedBy
└── CreatedAt / UpdatedAt

ProductionEntry            ← ENTITY MỚI
├── Id
├── ProductionDayId
├── Quantity               ← > 0
├── RecordedAt
├── Note                   ← optional
├── DeletedAt              ← soft delete
├── CreatedBy / UpdatedBy
└── CreatedAt / UpdatedAt
```

## 4.2. Cardinality

```text
Order 1 ──── * ProductionDay           (giữ nguyên Unique(OrderId, ProductionDate))
ProductionDay 1 ──── * ProductionEntry        ← MỚI
```

`Plan` và `Actual` vẫn **độc lập** (Step 1 §6 giữ nguyên). ProductionEntry treo dưới ProductionDay, không treo dưới ProductionPlan.

## 4.3. Trạng thái hiển thị của một ngày

Không lưu enum 3 giá trị. Suy ra từ dữ liệu:

```text
plan.planned_quantity = 0                       → NoPlan
production_date > Today                         → NotStarted
row IS NULL hoặc status = 'Open'                → InProduction
status = 'Closed'                               → Closed
```

Thứ tự kiểm tra quan trọng: `NoPlan` xét trước `NotStarted`. `Today` định nghĩa theo múi giờ nghiệp vụ — xem 14.2.

Lý do không lưu `NoPlan` thành trạng thái: khi Adjustment cộng add-on làm `planned_quantity` từ 0 thành 40, trạng thái lưu cứng sẽ lệch.

## 4.4. Domain Invariants — bổ sung

```text
ProductionEntry.Quantity > 0

SUM(entries của 1 ngày, chưa xoá)
    <= ProductionPlan.PlannedQuantity của ngày đó        ← MỚI

SUM(All Actual toàn đơn)
    <= Order.Quantity                                    ← giữ nguyên

Entry chỉ được create/update/delete khi record.status = 'Open'

record.status = 'Closed'
    ⇒ actual_quantity = SUM(entries) tại thời điểm đóng, và bất biến
```

## 4.5. Định nghĩa lại các derived data

| Giá trị | Công thức sau CR-01 |
|---|---|
| `DayActual` | `SUM(entries WHERE deleted_at IS NULL)` của ngày |
| `RemainingAllowance` | `MIN(plannedQuantity − DayActual, Order.Quantity − TotalActual)` |
| `TotalActual` | `SUM(tất cả entries chưa xoá của đơn)` — **bao gồm cả ngày đang Open** |
| `DailyShortage` | Chỉ tính khi `status = 'Closed'`: `plannedQuantity − actualQuantity`. Ngày Open trả `null` |
| `DailyDifference` | Chỉ tính khi Closed. Luôn ≤ 0 |
| `IsProvisional` | `status = 'Open'` → số liệu là **tạm tính** |

**Lưu ý cho người implement:** `TotalActual` bao gồm cả ngày đang mở là **cố ý** — nếu không, quản lý có thể nhập vượt tổng đơn trong ngày cuối. Nhưng `Order.Status` **không** được đánh giá lại theo giá trị này ngoài thời điểm Close (OV-4).

## 4.6. Transaction Boundaries — cập nhật Step 1 §15

```text
## Create Entry
BEGIN
  Lock Order (row lock)
  Load/Create ProductionDay (status Open)
  Validate record.status = 'Open'
  Validate plan.planned_quantity > 0
  Validate production_date <= today
  Read DayActual, TotalActual
  Validate quantity <= MIN(plan − DayActual, order.quantity − TotalActual)
  Insert ProductionEntry
  Insert ProductionEntryLog (CREATE)
COMMIT

## Update / Delete Entry
BEGIN
  Lock Order
  Validate record.status = 'Open'
  Validate lại toàn bộ ràng buộc như trên với giá trị mới
  Update / soft-delete entry
  Insert ProductionEntryLog (UPDATE | DELETE)
COMMIT

## Close Day  (Xuất hàng)
BEGIN
  Lock Order
  Lock ProductionDay
  Validate status = 'Open'
  actual := SUM(entries chưa xoá)
  Update record: status='Closed', actual_quantity=actual, closed_at, closed_by
  TotalActual := SUM toàn đơn
  IF TotalActual = Order.Quantity → Order.Status = 'Completed'
COMMIT
```

Bước đánh giá `Order.Status` **bắt buộc nằm trong cùng transaction** với bước Close.

---

# 5. Database Schema — Delta (Step 3)

Giữ nguyên toàn bộ convention của Step 3: `uuid` PK do ứng dụng sinh (UUIDv7), `varchar + CHECK` thay vì native ENUM, `timestamptz` cho audit, `date` cho business date, `ON DELETE RESTRICT`.

## 5.1. RENAME + ALTER `production_records` → `production_days`

Bảng đổi tên vì ngữ nghĩa đã đổi: nó không còn là "bản ghi sản lượng nhập tay" mà là **ngày sản xuất của đơn**, có vòng đời đóng/mở. Entity trong domain đổi tên tương ứng thành `ProductionDay`.

```sql
-- 1. Đổi tên bảng
ALTER TABLE production_records RENAME TO production_days;

-- 2. Đổi tên các constraint & index đi kèm
ALTER TABLE production_days
    RENAME CONSTRAINT fk_production_records_order            TO fk_production_days_order;
ALTER TABLE production_days
    RENAME CONSTRAINT fk_production_records_created_by       TO fk_production_days_created_by;
ALTER TABLE production_days
    RENAME CONSTRAINT fk_production_records_updated_by       TO fk_production_days_updated_by;
ALTER TABLE production_days
    RENAME CONSTRAINT uq_production_records_order_date       TO uq_production_days_order_date;
ALTER TABLE production_days
    RENAME CONSTRAINT ck_production_records_actual_quantity  TO ck_production_days_actual_quantity;

-- 3. actual_quantity chỉ có giá trị khi ngày đã đóng
ALTER TABLE production_days
    ALTER COLUMN actual_quantity DROP NOT NULL;

-- 4. Cột mới cho vòng đời ngày
ALTER TABLE production_days
    ADD COLUMN status     varchar(20) NOT NULL DEFAULT 'Open',
    ADD COLUMN closed_at  timestamptz NULL,
    ADD COLUMN closed_by  uuid        NULL;

ALTER TABLE production_days
    ALTER COLUMN status DROP DEFAULT;

ALTER TABLE production_days
    ADD CONSTRAINT fk_production_days_closed_by
        FOREIGN KEY (closed_by) REFERENCES users(id) ON DELETE RESTRICT;

ALTER TABLE production_days
    ADD CONSTRAINT ck_production_days_status
        CHECK (status IN ('Open', 'Closed'));

ALTER TABLE production_days
    ADD CONSTRAINT ck_production_days_closed_consistency
        CHECK (
            (status = 'Closed'
                AND closed_at IS NOT NULL
                AND closed_by IS NOT NULL
                AND actual_quantity IS NOT NULL)
            OR
            (status = 'Open'
                AND closed_at IS NULL
                AND closed_by IS NULL
                AND actual_quantity IS NULL)
        );

CREATE INDEX ix_production_days_status_date
    ON production_days (status, production_date);
```

`uq_production_days_order_date` giữ nguyên ràng buộc `UNIQUE(order_id, production_date)` — vẫn một dòng cho mỗi đơn mỗi ngày.

> **Nếu Step 6 chưa bắt đầu:** không cần script `RENAME` ở trên. Sửa thẳng migration gốc của Step 3 để bảng ra đời với tên `production_days`. Script `RENAME` chỉ cần khi database đã tồn tại.

**Phạm vi ảnh hưởng của việc đổi tên** — phải sửa đồng bộ ở:

| Nơi | Nội dung |
|---|---|
| Step 3 §4.4, §6, §16, §17, §18 | Tên bảng, index, sơ đồ quan hệ, danh sách quyết định |
| Step 1 §5, §6, §10, §14, §15 | Entity `ProductionRecord` → `ProductionDay` |
| Step 4 §3, §7, §17, §19, §21, §23 | DTO, audit fields, transaction table, endpoint đã loại bỏ |
| Step 5 §7, §17, §28 | Server state list, feature boundary |
| Implementation Prompt §15, §20, §31 | Bảng baseline, transaction, màn hình |
| EF Core | `DbSet<ProductionDay>`, entity configuration, tên navigation property |

## 5.2. Bảng mới `production_entries`

```sql
CREATE TABLE production_entries (
    id                  uuid PRIMARY KEY,
    production_day_id   uuid        NOT NULL,
    quantity            integer     NOT NULL,
    recorded_at         timestamptz NOT NULL,
    note                varchar(255) NULL,
    deleted_at          timestamptz NULL,
    created_by          uuid        NOT NULL,
    updated_by          uuid        NOT NULL,
    created_at          timestamptz NOT NULL,
    updated_at          timestamptz NOT NULL,

    CONSTRAINT fk_production_entries_day
        FOREIGN KEY (production_day_id)
        REFERENCES production_days(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_production_entries_created_by
        FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE RESTRICT,

    CONSTRAINT fk_production_entries_updated_by
        FOREIGN KEY (updated_by) REFERENCES users(id) ON DELETE RESTRICT,

    CONSTRAINT ck_production_entries_quantity_positive
        CHECK (quantity > 0)
);

CREATE INDEX ix_production_entries_day_recorded_at
    ON production_entries (production_day_id, recorded_at DESC);

CREATE INDEX ix_production_entries_day_active
    ON production_entries (production_day_id)
    WHERE deleted_at IS NULL;
```

`quantity > 0` chứ không `>= 0`: một lần ghi nhận bằng 0 là vô nghĩa. "Cả ngày không sản xuất được" thể hiện bằng **Close với 0 entry**, không phải bằng entry = 0.

## 5.3. Bảng mới `production_entry_logs`

```sql
CREATE TABLE production_entry_logs (
    id                      uuid PRIMARY KEY,
    production_entry_id     uuid        NOT NULL,
    action                  varchar(20) NOT NULL,
    old_quantity            integer     NULL,
    new_quantity            integer     NULL,
    old_note                varchar(255) NULL,
    new_note                varchar(255) NULL,
    changed_by              uuid        NOT NULL,
    changed_at              timestamptz NOT NULL,

    CONSTRAINT fk_production_entry_logs_entry
        FOREIGN KEY (production_entry_id)
        REFERENCES production_entries(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_production_entry_logs_changed_by
        FOREIGN KEY (changed_by) REFERENCES users(id) ON DELETE RESTRICT,

    CONSTRAINT ck_production_entry_logs_action
        CHECK (action IN ('Create', 'Update', 'Delete'))
);

CREATE INDEX ix_production_entry_logs_entry
    ON production_entry_logs (production_entry_id, changed_at);
```

Đáp ứng yêu cầu "xem lịch sử đã nhập trước đó" kể cả khi có sửa/xoá giữa chừng, và giữ nguyên nguyên tắc Step 3 §14 (chỉ audit nơi thực sự cần).

## 5.4. Bảng mới `system_settings`

```sql
CREATE TABLE system_settings (
    id                          uuid PRIMARY KEY,
    recording_interval_minutes  integer     NOT NULL,
    day_start_time              time        NOT NULL,
    day_end_time                time        NOT NULL,
    updated_by                  uuid        NOT NULL,
    updated_at                  timestamptz NOT NULL,

    CONSTRAINT fk_system_settings_updated_by
        FOREIGN KEY (updated_by) REFERENCES users(id) ON DELETE RESTRICT,

    CONSTRAINT ck_system_settings_interval
        CHECK (recording_interval_minutes BETWEEN 5 AND 480),

    CONSTRAINT ck_system_settings_time_range
        CHECK (day_end_time > day_start_time)
);
```

Một dòng duy nhất trong Phase 1, tạo bởi bootstrap của ứng dụng (cùng cơ chế tạo user đầu tiên ở Step 3 §15). Không hard-code giá trị trong migration nếu tránh được; mặc định đề xuất: `60`, `08:00`, `17:00`.

## 5.5. Enum values bổ sung — Step 3 §5

```text
ProductionDayStatus          (trên bảng production_days)
    Open
    Closed

ProductionEntryLogAction
    Create
    Update
    Delete
```

## 5.6. Cross-row invariants bổ sung — Step 3 §9

```text
SUM(ProductionEntry.Quantity WHERE deleted_at IS NULL, cùng record)
    <= ProductionPlan.PlannedQuantity của (order_id, production_date)
```

Không thể diễn đạt bằng CHECK constraint vì liên quan nhiều dòng và bảng khác. Thực thi bằng transaction + row lock, đúng chiến lược Step 3 §10.

**Cảnh báo cho người implement:** khoá `Order` là chưa đủ cho ràng buộc này khi hệ thống mở rộng nhiều người dùng. Phải khoá thêm `production_days` của ngày đang thao tác. Thứ tự khoá thống nhất: **Order → ProductionDay → ProductionPlan** để tránh deadlock (bổ sung cho Step 4 §18).

## 5.7. Final schema sau CR-01

```text
users                    (không đổi)
orders                   (không đổi)
production_plans         (không đổi)
production_days          ← ĐỔI TÊN từ production_records
                         (+ status, closed_at, closed_by; actual_quantity → nullable)
production_entries       ← MỚI
production_entry_logs    ← MỚI
plan_adjustments         (không đổi)
plan_adjustment_items    (không đổi)
system_settings          ← MỚI
```

Từ 6 bảng lên **9 bảng**.

---

# 6. API Contract — Delta (Step 4)

Giữ nguyên: base path `/api/v1`, HttpOnly Cookie Auth, current user lấy từ auth context, error model, HTTP semantics.

## 6.1. Endpoint bị loại bỏ

```text
POST /api/v1/orders/{orderId}/production-records
PUT  /api/v1/orders/{orderId}/production-records/{productionRecordId}
```

Hai endpoint này dựa trên mô hình "một giá trị/ngày" đã bị OV-1, OV-2 thay thế.

## 6.2. Endpoint mới

```text
GET    /api/v1/orders/{orderId}/production-days/{productionDate}
POST   /api/v1/orders/{orderId}/production-days/{productionDate}/entries
PUT    /api/v1/production-entries/{entryId}
DELETE /api/v1/production-entries/{entryId}
POST   /api/v1/orders/{orderId}/production-days/{productionDate}/close

GET    /api/v1/settings
PUT    /api/v1/settings
```

## 6.3. GET production day — màn hình chính của MH5

```text
GET /api/v1/orders/{orderId}/production-days/2026-08-13
```

Response:

```json
{
  "orderId": "uuid",
  "orderCode": "ORD-2026-001",
  "productionDate": "2026-08-13",
  "dayStatus": "InProduction",
  "initialPlannedQuantity": 200,
  "plannedQuantity": 200,
  "addOnQuantity": 0,
  "dayActualQuantity": 55,
  "isProvisional": true,
  "remainingAllowance": 145,
  "remainingAllowanceReason": "DailyPlan",
  "orderRemainingQuantity": 625,
  "lastRecordedAt": "2026-08-13T10:02:00+07:00",
  "closedAt": null,
  "closedBy": null,
  "shortageQuantity": null,
  "difference": null,
  "entries": [
    {
      "id": "uuid",
      "quantity": 15,
      "recordedAt": "2026-08-13T10:02:00+07:00",
      "note": null,
      "runningTotal": 55,
      "isEdited": false
    }
  ]
}
```

Ghi chú thiết kế, theo đúng nguyên tắc Step 4 §22 (DTO hướng nghiệp vụ, frontend không phải tự ghép):

- `dayStatus` ∈ `NoPlan | NotStarted | InProduction | Closed` — server suy ra, frontend không tự tính. Quy tắc suy ra đầy đủ: xem 14.3.
- `remainingAllowance` là số hiển thị trên ô "Còn được nhập". `remainingAllowanceReason` ∈ `DailyPlan | OrderQuantity` để UI chọn đúng câu thông báo.
- `shortageQuantity` và `difference` trả `null` khi ngày chưa Closed — đây là cách contract thực thi OV-5 ở tầng API, tránh frontend vô tình hiển thị thiếu giả.
- `runningTotal` do server tính để UI khỏi cộng dồn phía client.

## 6.4. POST entry

```json
{
  "quantity": 15,
  "note": "Tổ 2 vào ca"
}
```

Validate theo thứ tự (trả về lỗi đầu tiên gặp phải):

| Điều kiện | HTTP | Error code |
|---|---|---|
| Ngày > hôm nay | 422 | `FUTURE_DATE_NOT_ALLOWED` |
| Không có `production_plans` cho ngày, hoặc `plannedQuantity = 0` | 422 | `DAY_HAS_NO_PLAN` |
| `record.status = 'Closed'` | 409 | `DAY_ALREADY_CLOSED` |
| `quantity <= 0` | 400 | `VALIDATION_ERROR` |
| `DayActual + quantity > plannedQuantity` | 422 | `ENTRY_EXCEEDS_DAILY_PLAN` |
| `TotalActual + quantity > order.quantity` | 422 | `ACTUAL_EXCEEDS_ORDER_QUANTITY` |
| Order đã `Completed` | 409 | `ORDER_ALREADY_COMPLETED` |

`ACTUAL_EXCEEDS_ORDER_QUANTITY` **tái sử dụng** error code đã có ở Step 4 §7 — không đặt code mới cho cùng một nghiệp vụ.

Response lỗi vượt trần nên kèm `details` để UI hiển thị đúng con số:

```json
{
  "code": "ENTRY_EXCEEDS_DAILY_PLAN",
  "message": "The entry exceeds the remaining allowance for this production day.",
  "details": [
    { "field": "quantity", "code": "MAX_ALLOWED", "message": "145" }
  ]
}
```

## 6.5. PUT / DELETE entry

Cả hai chỉ hợp lệ khi `record.status = 'Open'`, ngược lại `409 DAY_ALREADY_CLOSED`.

`PUT` validate lại toàn bộ ràng buộc với công thức thay thế, tương tự logic edit Actual cũ ở Step 4 §7:

```text
NewDayActual = DayActual − OldQuantity + NewQuantity
NewTotalActual = TotalActual − OldQuantity + NewQuantity
```

`DELETE` là **soft delete**, giữ `production_entry_logs`. Không có hard delete.

Việc bổ sung `DELETE` này **thu hẹp** quyết định Step 4 §21: chỉ entry trong ngày Open mới xoá được; `production_days` và `plan_adjustments` vẫn tuyệt đối không xoá.

## 6.6. POST close — Xuất hàng

```text
POST /api/v1/orders/{orderId}/production-days/{productionDate}/close
```

Request body rỗng. Không cho client gửi `actualQuantity` — server tự tính từ entries. Đây là điểm bảo vệ tính toàn vẹn quan trọng nhất của CR.

Response:

```json
{
  "productionDate": "2026-08-13",
  "dayStatus": "Closed",
  "plannedQuantity": 200,
  "actualQuantity": 160,
  "shortageQuantity": 40,
  "difference": -40,
  "closedAt": "2026-08-13T17:45:00+07:00",
  "orderStatus": "Incomplete",
  "orderCompleted": false,
  "hasShortage": true
}
```

`hasShortage` là tín hiệu để frontend mở luồng Xử lý thiếu ngay sau khi đóng ngày.

Lỗi:

| Điều kiện | HTTP | Error code |
|---|---|---|
| Đã Closed | 409 | `DAY_ALREADY_CLOSED` |
| Ngày > hôm nay | 422 | `FUTURE_DATE_NOT_ALLOWED` |
| `plannedQuantity = 0` | 422 | `DAY_HAS_NO_PLAN` |

Close với **0 entry** là hợp lệ: `actualQuantity = 0`, `shortageQuantity = plannedQuantity`. UI phải xác nhận rõ trước khi gọi.

**Không có** endpoint reopen. Theo quyết định nghiệp vụ, ngày đã Close là bất biến.

## 6.7. Thay đổi trong nhóm Adjustment

Endpoint giữ nguyên đường dẫn, bổ sung điều kiện:

**Preview & Apply** (`POST /production-plans/{productionPlanId}/adjustments/preview` và `.../adjustments`):

| Điều kiện mới | HTTP | Error code |
|---|---|---|
| Source day chưa Closed | 422 | `SOURCE_DAY_NOT_CLOSED` |
| Target plan thuộc ngày đã Closed | 422 | `TARGET_DAY_CLOSED` |
| Target plan thuộc ngày đã qua | 422 | `TARGET_DATE_IN_PAST` |
| Không còn ngày nào hợp lệ để bù | 422 | `NO_ELIGIBLE_TARGET_DAY` |

**Automatic allocation** (Step 4 §9): tập ngày ứng viên đổi từ "các ngày liên tiếp còn lại" thành **"các ngày liên tiếp còn lại chưa Closed"**. Thuật toán chia dư giữ nguyên (23/4 → 6/6/6/5).

`NO_ELIGIBLE_TARGET_DAY` là trường hợp biên mới do CR-01 tạo ra: ngày cuối cùng của đơn bị thiếu và không còn ngày nào phía sau. Baseline cũ không gặp vì shortage phát sinh sớm hơn.

## 6.8. Settings API

```text
GET /api/v1/settings
PUT /api/v1/settings
```

```json
{
  "recordingIntervalMinutes": 60,
  "dayStartTime": "08:00",
  "dayEndTime": "17:00"
}
```

Cấu hình **không hồi tố** dữ liệu đã ghi. Chu kỳ chỉ dùng để nhắc, server **không** dùng nó để từ chối request nào.

## 6.9. Statistics — bổ sung

`GET /api/v1/orders/{orderId}/statistics` — mỗi phần tử `daily[]` thêm:

```text
dayStatus
isProvisional
closedAt
```

và `shortageQuantity` / `difference` trả `null` cho ngày Open.

`GET /api/v1/statistics/dashboard` — thêm:

```text
todayProduction[]        (orderId, orderCode, plannedQuantity, dayActualQuantity, lastRecordedAt)
unclosedPastDays[]       (orderId, orderCode, productionDate, plannedQuantity)
openShortages[]          (orderId, orderCode, productionDate, shortageQuantity)
```

`totalActualQuantity` hiện có: cần ghi rõ trong contract rằng giá trị này **bao gồm cả sản lượng tạm tính** của ngày đang mở, để frontend gắn nhãn phù hợp.

## 6.10. Transaction boundaries — cập nhật Step 4 §19

| API | Transaction |
|---|---|
| Get Production Day | No |
| Create Entry | **Yes** |
| Update Entry | **Yes** |
| Delete Entry | **Yes** |
| Close Day | **Yes** |
| Get / Put Settings | No / Yes |

## 6.11. Endpoint thay đổi bởi CR-01

Chỉ liệt kê phần bị ảnh hưởng. Các endpoint không xuất hiện ở đây giữ nguyên như Step 4 §20.

**Loại bỏ:**

```text
POST   /api/v1/orders/{orderId}/production-records
PUT    /api/v1/orders/{orderId}/production-records/{productionRecordId}
```

**Thêm mới:**

```text
GET    /api/v1/orders/{orderId}/production-days/{productionDate}
POST   /api/v1/orders/{orderId}/production-days/{productionDate}/entries
PUT    /api/v1/production-entries/{entryId}
DELETE /api/v1/production-entries/{entryId}
POST   /api/v1/orders/{orderId}/production-days/{productionDate}/close

GET    /api/v1/settings
PUT    /api/v1/settings
```

**Giữ nguyên path, đổi hành vi:**

```text
POST   /api/v1/production-plans/{productionPlanId}/adjustments/preview
POST   /api/v1/production-plans/{productionPlanId}/adjustments
       → thêm điều kiện SOURCE_DAY_NOT_CLOSED / TARGET_DAY_CLOSED
       → TARGET_DATE_IN_PAST / NO_ELIGIBLE_TARGET_DAY  (Mục 6.7)

GET    /api/v1/orders/{orderId}/statistics
       → daily[] thêm dayStatus / isProvisional / closedAt
       → shortageQuantity, difference = null khi ngày Open  (Mục 6.9)

GET    /api/v1/statistics/dashboard
       → thêm todayProduction[] / unclosedPastDays[] / openShortages[]  (Mục 6.9)
```

---

# 7. Frontend Architecture — Delta (Step 5)

Giữ nguyên stack: React + TypeScript + Vite + TanStack Router + TanStack Query + React Hook Form + Zod. Không thêm global state, không thêm thư viện mới.

## 7.1. Routes

```text
/login
/dashboard
/orders
/orders/new
/orders/:orderId
/orders/:orderId/days/:productionDate      ← MỚI
/settings                                   ← MỚI
```

Đây là **ngoại lệ có chủ ý** với nguyên tắc Step 5 §5 ("ProductionRecord không có page độc lập"). Lý do: MH5 nay là màn hình thao tác nhiều nhất trong ngày, cần URL riêng để vào thẳng từ Dashboard trong 1 click và để refresh không mất ngữ cảnh. Các entity khác vẫn không có page riêng.

## 7.2. Folder structure

```text
features/production/
├── api/
│   └── productionApi.ts             ← FILE SẴN CÓ, bổ sung hàm mới vào đây
├── components/
│   ├── DayStatusBadge.tsx           ← MỚI
│   ├── EntryQuickForm.tsx           ← MỚI
│   ├── EntryHistoryTable.tsx        ← MỚI
│   ├── RemainingAllowance.tsx       ← MỚI
│   └── CloseDayDialog.tsx           ← MỚI
├── hooks/
│   ├── useProductionDay.ts          ← MỚI
│   └── useRecordingReminder.ts      ← MỚI
├── pages/
│   └── ProductionDayPage.tsx        ← MỚI
└── types/

features/settings/                    ← FEATURE MỚI
├── api/ · components/ · hooks/ · pages/ · types/
```

**Quy ước API của feature `production`:** toàn bộ hàm gọi API nằm trong **một file duy nhất `productionApi.ts`**. Không tách thành file riêng cho từng endpoint.

Bổ sung vào `productionApi.ts`:

```text
getProductionDay(orderId, productionDate)
createProductionEntry(orderId, productionDate, payload)
updateProductionEntry(entryId, payload)
deleteProductionEntry(entryId)
closeProductionDay(orderId, productionDate)
```

Gỡ khỏi `productionApi.ts` (theo Mục 6.11):

```text
createProductionRecord(...)
updateProductionRecord(...)
```

Feature `settings` là feature mới nên tạo file `settingsApi.ts` theo cùng quy ước một-file-một-feature.

`features/production` trước đây không có `pages/`; nay có, theo route mới ở 7.1.

## 7.3. Query keys & invalidation — cập nhật Step 5 §25

Query key mới:

```text
["orders", orderId, "production-days", productionDate]
["settings"]
```

Sau **create / update / delete entry**, invalidate:

```text
["orders", orderId, "production-days", productionDate]
["orders", orderId]
["orders", orderId, "statistics"]
["statistics", "dashboard"]
```

Sau **close day**, invalidate thêm:

```text
["orders", orderId, "production-plans"]
```

vì bảng timeline ở MH4 mới đổi trạng thái ngày và mới xuất hiện shortage.

## 7.4. Optimistic update — khuyến nghị KHÔNG dùng cho entry

Implementation Prompt §40 đã cấm optimistic update cho Actual create/edit. CR-01 **giữ nguyên** lệnh cấm đó và mở rộng sang toàn bộ entry + close. Lý do vì sao lệnh cấm này đặc biệt quan trọng với CR-01:

`remainingAllowance` là giá trị server tính từ hai ràng buộc chéo bảng. Nếu client tự trừ để hiển thị ngay, sẽ có khoảnh khắc con số trên màn hình cho phép nhập tiếp trong khi server đã từ chối. Với màn hình dùng 8–10 lần/ngày, một lần lệch là đủ làm mất niềm tin vào con số.

Khuyến nghị: `mutate` → chờ response → cập nhật từ payload server trả về. Bù lại tốc độ bằng cách để endpoint POST entry trả luôn **state đầy đủ của ngày** (giống response GET ở 6.3), tránh một round trip refetch.

## 7.5. Hook nhắc chu kỳ

`useRecordingReminder(lastRecordedAt, intervalMinutes)` — thuần client, tính từ `["settings"]` và `lastRecordedAt`. **Không** chặn form, chỉ đổi hiển thị. Không gọi API định kỳ.

---

# 8. Screen Specs — Delta

| Màn hình | File spec | Thay đổi |
|---|---|---|
| MH5 Nhập sản lượng | `production-quantity-entry-screen-spec.md` | **Viết lại.** Xem 8.1 |
| MH4 Chi tiết đơn | `order-detail-screen-spec.md` | Thêm cột trạng thái ngày; ngày Open hiển thị *tạm tính*, cột chênh lệch để trống; drill-down entries; lối vào Xử lý thiếu chỉ ở ngày Closed |
| MH6 Option 1 | `production-shortage-option-1-screen-spec.md` | Danh sách ngày ứng viên loại bỏ ngày đã Closed, hiển thị kèm lý do bị loại; thêm trạng thái rỗng `NO_ELIGIBLE_TARGET_DAY` |
| MH6 Option 2 | `man_hinh_6_option_2_spec_vi.md` | Tập ngày chia đều đổi thành "ngày liên tiếp còn lại **chưa Closed**" |
| Dashboard | `dashboard-screen-spec.md` | Thêm khối *Cần xử lý ngay* (ngày chưa Xuất hàng + shortage chưa xử lý) và khối *Đang sản xuất hôm nay* |
| MH1 Danh sách đơn | `production-order-list-screen.md` | Thêm cột tiến độ hôm nay + chỉ báo có ngày chưa Xuất hàng |
| MH3 Lập kế hoạch | `create-order-production-plan-screen.md` | Cảnh báo khi một ngày có `plannedQuantity = 0`: ngày đó sẽ không nhập được sản lượng |
| MH7 Cấu hình | *(chưa có)* | **Spec mới** |

## 8.1. MH5 — cấu trúc mới

Bố cục 4 khối, trên xuống:

1. **Tổng quan ngày** — Kế hoạch ngày · Đã nhập · **Còn được nhập** · Trạng thái + thời điểm ghi nhận gần nhất.
2. **Form ghi nhận nhanh** — ô số lượng auto-focus, ghi chú tuỳ chọn, Enter = Ghi nhận.
3. **Lịch sử các lần nhập trong ngày** — hiển thị sẵn, không cần bấm mở; mới nhất trên cùng; mỗi dòng có Sửa/Xoá khi ngày Open.
4. **Khu vực Xuất hàng** — tách biệt, ở đáy màn hình.

Trạng thái UI: `NoPlan` · `InProduction` · `Closed` · `PastDayNotClosed` (banner cảnh báo, vẫn thao tác đầy đủ).

**Ràng buộc UX bắt buộc** (xuất phát từ quyết định "đã Close thì không mở lại"):

- Nút Xuất hàng đặt sau bảng lịch sử, buộc người dùng lướt qua toàn bộ số đã nhập.
- Enter trong form nhập **không** được trigger Xuất hàng.
- Dialog xác nhận hiển thị **đầy đủ** danh sách entries, không thu gọn.
- Nút mặc định khi Enter trong dialog là *Quay lại*, không phải *Xác nhận*.
- Câu cảnh báo phải nêu rõ: không sửa, không xoá, không mở lại.

Các mục **giữ nguyên** từ spec cũ: §4.1 (kế hoạch = 0), §4.4 (không vượt tổng đơn), §4.7 (đơn hoàn thành → read-only), §6 (không confirm ngay khi gõ số).

---

# 9. Business Rules — ma trận đầy đủ

## 9.1. Giữ nguyên

| Mã | Nội dung | Nguồn |
|---|---|---|
| K-01 | `SUM(InitialPlannedQuantity) = Order.Quantity` | Step 1 §10, Step 3 §12 |
| K-02 | `SUM(All Actual) <= Order.Quantity` | Step 1 §10 |
| K-03 | Kế hoạch ngày = 0 → không nhập được, kể cả 0 | MH5 §4.1 |
| K-04 | Thiếu chỉ cảnh báo, không bắt buộc xử lý ngay | Master §6 |
| K-05 | Add-on bù toàn bộ, không nhập số tuỳ ý | Master §8 Rule 1 |
| K-06 | Không giảm kế hoạch các ngày khác | Master §8 Rule 3 |
| K-07 | `SUM(PlannedQuantity)` sau adjustment có thể > `Order.Quantity` | Step 3 §12 |
| K-08 | Preview không persist; Apply mới tạo Adjustment | Step 4 §8 |
| K-09 | Applied Adjustment là lịch sử bất biến | Step 4 §12 |
| K-10 | Tối đa 1 Applied Adjustment / source plan | Step 4 §12 |
| K-11 | `SUM(AddOnQuantity) = ShortageQuantity` | Step 1 §10 |
| K-12 | Option 2 chia vào các ngày **liên tiếp** | Master §9 |

## 9.2. Bị sửa

| Mã | Cũ | Mới | OV |
|---|---|---|---|
| M-01 | 1 Actual/ngày, sửa bằng edit | N entries/ngày | OV-1 |
| M-02 | Actual là giá trị, không phải increment | Actual là increment | OV-2 |
| M-03 | Actual được vượt kế hoạch ngày | Không được vượt | OV-3 |
| M-04 | Completed đánh giá sau mỗi lần nhập | Chỉ đánh giá khi Close | OV-4 |
| M-05 | Shortage tồn tại ngay khi Actual < Plan | Chỉ khi ngày Closed | OV-5 |
| M-06 | Sửa Actual bất kỳ lúc nào | Chỉ khi ngày Open | OV-7 |
| M-07 | Target day chỉ cần "chưa qua" | Chưa qua **và** chưa Closed | §6.7 |

## 9.3. Thêm mới

| Mã | Nội dung |
|---|---|
| N-01 | `ProductionEntry.Quantity > 0` |
| N-02 | `SUM(entries của ngày) <= PlannedQuantity của ngày` |
| N-03 | `RemainingAllowance = MIN(trần ngày, trần đơn)` |
| N-04 | Entry chỉ create/update/delete khi `status = 'Open'` |
| N-05 | Không ghi nhận cho ngày tương lai |
| N-06 | Close là một chiều, không có reopen |
| N-07 | Ngày Open không có shortage, không có difference |
| N-08 | Close với 0 entry là hợp lệ |
| N-09 | Ngày quá khứ chưa Close: cảnh báo, không tự đóng |
| N-10 | Chu kỳ ghi nhận chỉ nhắc, không chặn |
| N-11 | Server không nhận `actualQuantity` từ client khi Close |
| N-12 | Add-on làm tăng `plannedQuantity` → trần nhập của ngày tăng theo |

---

# 10. Acceptance Criteria

| # | Kịch bản | Kết quả |
|---|---|---|
| AC-01 | Plan 120, đã nhập 90, POST entry 40 | 422 `ENTRY_EXCEEDS_DAILY_PLAN`, details `MAX_ALLOWED = 30` |
| AC-02 | Plan 120, đã nhập 90, POST entry 30 | 201, `dayActualQuantity = 120`, `remainingAllowance = 0` |
| AC-03 | Plan 120, đã nhập 90, đơn chỉ còn 15 | `remainingAllowance = 15`, `reason = OrderQuantity`; POST 30 → 422 `ACTUAL_EXCEEDS_ORDER_QUANTITY` |
| AC-04 | `plannedQuantity = 0`, POST entry bất kỳ | 422 `DAY_HAS_NO_PLAN` |
| AC-05 | POST entry `quantity = 0` | 400 `VALIDATION_ERROR` |
| AC-06 | GET day khi Open, đã nhập 60/200 | `shortageQuantity = null`, `difference = null`, `isProvisional = true` |
| AC-07 | Close với 160/200 | `shortageQuantity = 40`, `hasShortage = true`, `dayStatus = Closed` |
| AC-08 | Close lần 2 cùng ngày | 409 `DAY_ALREADY_CLOSED` |
| AC-09 | POST/PUT/DELETE entry sau khi Close | 409 `DAY_ALREADY_CLOSED` |
| AC-10 | Close với 0 entry | 200, `actualQuantity = 0`, `shortageQuantity = plannedQuantity` |
| AC-11 | Close và `TotalActual = Order.Quantity` | `orderStatus = Completed`, `orderCompleted = true` |
| AC-12 | `TotalActual = Order.Quantity` nhưng chưa Close | `Order.Status` vẫn `Incomplete` |
| AC-13 | Preview adjustment khi source day còn Open | 422 `SOURCE_DAY_NOT_CLOSED` |
| AC-14 | Apply adjustment với target là ngày đã Closed | 422 `TARGET_DAY_CLOSED` |
| AC-15 | Thiếu ở ngày cuối, không còn ngày sau | 422 `NO_ELIGIBLE_TARGET_DAY` |
| AC-16 | Apply add-on +40 vào ngày plan 250 | `plannedQuantity = 290`, `remainingAllowance` của ngày đó tăng tương ứng |
| AC-17 | PUT entry 25 → 10 khi ngày Open | 200, `dayActualQuantity` giảm 15, ghi `production_entry_logs` action `Update` |
| AC-18 | DELETE entry khi ngày Open | 200, soft delete, entry biến khỏi `entries[]`, log action `Delete` |
| AC-19 | Ngày hôm qua chưa Close | Xuất hiện trong `unclosedPastDays[]`; vẫn POST entry và Close được |
| AC-20 | POST entry cho ngày mai | 422 `FUTURE_DATE_NOT_ALLOWED` |
| AC-21 | Đã quá `recordingIntervalMinutes` chưa nhập | Chỉ hiển thị nhắc; POST entry vẫn 201 |
| AC-22 | Hai request POST entry đồng thời, tổng vượt trần | Đúng một request thành công, request còn lại 422 |

---

# 11. Thứ tự triển khai — Vertical Slice

Implementation Prompt §50 quy định **bắt buộc** mọi feature phải là một lát cắt dọc chạy được đến UI, không được làm xong toàn bộ backend rồi mới làm frontend. Thứ tự dưới đây tuân thủ ràng buộc đó.

Mỗi slice phải đủ 17 mục Definition of Done ở §50 trước khi sang slice kế tiếp.

## Slice 1 — Đổi tên & mở vòng đời ngày *(nền tảng, chưa có UI mới)*

```text
Migration: RENAME production_records → production_days
           + status / closed_at / closed_by
           + actual_quantity nullable
Domain:    ProductionRecord → ProductionDay
EF Core:   DbSet, entity config, navigation property
Frontend:  đổi tên type/DTO đang tham chiếu
```

Slice này không tạo UI mới. Kiểm tra bằng cách: ứng dụng build được, các màn hình cũ còn chạy, migration up/down sạch.

## Slice 2 — Ghi nhận nhiều lần trong ngày

```text
DB       production_entries + production_entry_logs
Domain   ProductionEntry, invariant N-01..N-05, RemainingAllowance
App      CreateEntry (transaction + lock Order → ProductionDay)
API      GET production-day, POST entries
FE       productionApi.ts: +getProductionDay, +createProductionEntry
         route /orders/:orderId/days/:date, page, form nhập nhanh,
         bảng lịch sử trong ngày, RemainingAllowance
Test     AC-01..AC-06, AC-20, AC-22
```

**Kiểm thử UI được ngay:** mở màn hình, nhập 3 lần, thấy lịch sử và số "Còn được nhập" giảm đúng.

## Slice 3 — Sửa / xoá lần ghi nhận

```text
App      UpdateEntry, DeleteEntry (soft delete + log)
API      PUT / DELETE production-entries/{id}
FE       productionApi.ts: +updateProductionEntry, +deleteProductionEntry
         nút Sửa / Xoá trên từng dòng, dialog xác nhận xoá
Test     AC-17, AC-18
```

## Slice 4 — Xuất hàng

```text
App      CloseDay (transaction, snapshot actual, đánh giá Order.Status)
API      POST .../close
FE       productionApi.ts: +closeProductionDay,
         gỡ createProductionRecord / updateProductionRecord
         nút Xuất hàng tách biệt + dialog xác nhận đầy đủ entries,
         trạng thái read-only sau khi đóng
Test     AC-07..AC-12
```

Sau slice này, luồng nghiệp vụ lõi của CR đã chạy trọn vẹn end-to-end.

## Slice 5 — Xử lý thiếu theo trạng thái ngày

```text
App/API  SOURCE_DAY_NOT_CLOSED, TARGET_DAY_CLOSED,
         TARGET_DATE_IN_PAST, NO_ELIGIBLE_TARGET_DAY
         Automatic allocation lọc ngày chưa Closed
FE       MH6 Option 1 + Option 2: lọc ứng viên, hiển thị lý do bị loại,
         empty state khi không còn ngày hợp lệ
Test     AC-13..AC-16
```

## Slice 6 — Cấu hình

```text
DB/API   system_settings + GET/PUT /settings + bootstrap giá trị mặc định
FE       /settings page, hook useRecordingReminder, hiển thị nhắc trên MH5
Test     AC-21
```

## Slice 7 — Hiển thị trạng thái ngày ở các màn hình còn lại

```text
API      statistics daily[] thêm dayStatus/isProvisional/closedAt;
         dashboard thêm todayProduction / unclosedPastDays / openShortages
FE       MH4 cột trạng thái + drill-down, Dashboard 2 khối mới,
         MH1 cột hôm nay, MH3 cảnh báo plan = 0
Test     AC-19
```

## Slice 8 — Dọn tài liệu

Thực hiện các mục ở Mục 13. **Không để đến cuối cùng** — mỗi slice hoàn tất thì dọn ngay phần tài liệu tương ứng, nếu không sẽ bị bỏ quên.

---

# 12. Quyết định đã chốt trong CR

| # | Vấn đề | Quyết định |
|---|---|---|
| 12.1 | Sửa/xoá entry trong ngày Open có bắt buộc nhập lý do? | **Không bắt buộc.** MH5 spec §4.6 (bắt buộc lý do) bị ghi đè — xem OV-8. Không thêm field `reason` vào `PUT` / `DELETE` entry. Vẫn ghi đầy đủ `production_entry_logs` để truy vết. Lý do: đây là sửa nháp trước khi chốt sổ, và màn hình cần nhanh vì dùng 8–10 lần mỗi ngày |
| 12.2 | Có đổi tên bảng `production_records`? | **Có.** Đổi thành `production_days`, entity đổi thành `ProductionDay` — xem OV-11 và Mục 5.1 |
| 12.3 | Có gỡ kịch bản Option B khỏi tài liệu? | **Có.** Gỡ mô tả kịch bản, giữ API reverse và bước server recalculate — xem Mục 3.1 và Mục 13 |

---

# 13. Tài liệu cần dọn dẹp

Sau khi CR-01 được duyệt, các đoạn dưới đây mô tả những nhánh **không còn đạt tới được**. Giữ lại chúng sẽ khiến người implement viết code phòng thủ cho luồng chết, hoặc tệ hơn là làm theo baseline cũ vì nó vẫn nằm trong tài liệu "DONE".

## 13.1. Phải xoá

| Tài liệu | Đoạn | Lý do |
|---|---|---|
| `production-management-step-1-domain-model.md` §12 | Toàn bộ ví dụ "sửa Actual 80 → 100 khiến shortage biến mất" và phần "Domain implication" | Actual không thể đổi sau khi ngày Closed, mà Adjustment chỉ tạo được sau khi Closed |
| `Production_Management_Step_4_API_Contract.md` §15 | Mục "Actual Change After Adjustment" | Cùng lý do |
| `Production_Management_Step_4_API_Contract.md` §23 | Sơ đồ flow "Adjustment becoming invalid" | Cùng lý do |
| `production-quantity-entry-screen-spec.md` §5 | Mục "Vượt kế hoạch" | Bị OV-3 cấm |
| `production-quantity-entry-screen-spec.md` §4.6 | Yêu cầu bắt buộc nhập lý do khi sửa | Bị OV-8 và quyết định 12.1 ghi đè |
| `Production_Management_Step_4_API_Contract.md` §21 | Hai dòng lý do loại bỏ `POST /production-records/{id}/add` và `DELETE /production-records/{id}` | Bị OV-2 và OV-6 đảo ngược |
| `Production_Management_Step_5_Frontend_Architecture.md` §17 | Câu "Actual is a value, not an increment" và "UI must not use a `+quantity` interaction" | Bị OV-2 đảo ngược |
| `Production_Management_Implementation_Prompt_for_Claude.md` §31 | Khối "Correct / Incorrect" với ví dụ `+5` là sai | Bị OV-13 đảo ngược |
| `Production_Management_Implementation_Prompt_for_Claude.md` §47 | 4 dòng nêu ở OV-12 | Thay bằng danh sách ở 14.1 |
| `Production_Management_Implementation_Prompt_for_Claude.md` §43 | Nhóm test `Actual` và `Shortage` | Thay bằng test list ở 14.8 |

## 13.2. Phải giữ nhưng viết lại

| Tài liệu | Đoạn | Viết lại thành |
|---|---|---|
| Step 1 §12 | Tiêu đề "Actual Edit sau Adjustment" | Đổi thành mô tả: Adjustment chỉ reverse thủ công khi quản lý chọn nhầm ngày bù |
| Step 4 §11 | "Preview Staleness" | Giữ `ADJUSTMENT_OUTDATED`, đổi ví dụ: nguyên nhân stale nay là target day bị Close hoặc shortage đã được xử lý bởi request khác, không phải Actual thay đổi |
| Step 4 §7 | "Order status — sau create/edit Actual" | Đổi thành: đánh giá `Completed` **chỉ tại thời điểm Close Day** |
| Step 3 §10 | Ví dụ transaction "khi editing Actual" | Đổi thành ba transaction ở Mục 4.6 của CR này |
| Step 1 §5 | "Business rule — một Order chỉ có 1 ProductionRecord cho một ngày" | Đổi thành: một Order có 1 `ProductionDay` cho một ngày, và mỗi `ProductionDay` có N `ProductionEntry` |

## 13.3. Nguyên tắc áp dụng

Theo "Conflict rule" trong `Production_Management_Implementation_Prompt_for_Claude.md`: khi tài liệu cũ và CR-01 mâu thuẫn, **CR-01 thắng**. Nhưng vì các tài liệu Step 1–5 đều đang gắn nhãn **DONE**, người đọc sau này sẽ mặc định tin chúng. Vì vậy việc dọn dẹp ở 13.1 và 13.2 là **bắt buộc**, không phải tuỳ chọn — nếu chưa dọn xong thì mọi tài liệu bị ảnh hưởng phải gắn nhãn:

```text
⚠ SUPERSEDED IN PART BY CR-01 — see CR-01 §3
```

---

# 14. Bổ sung sau rà soát cuối

Các điểm dưới đây là lỗ hổng phát hiện khi rà CR đối chiếu với `Production_Management_Implementation_Prompt_for_Claude.md`. Nếu thiếu, người implement sẽ phải tự đoán.

## 14.1. Danh sách NON-NEGOTIABLE sau CR-01

Thay thế 4 dòng bị gỡ ở OV-12. Danh sách non-negotiable **sau CR-01** gồm các dòng cũ còn hiệu lực, cộng thêm:

```text
Actual is an increment; one production day has N entries
One ProductionDay per Order + ProductionDate
ProductionEntry.Quantity > 0  (entry = 0 is NOT valid)
Closing a day with zero entries IS valid
SUM(entries of a day) <= PlannedQuantity of that day
Order status is evaluated ONLY at Close Day
Shortage exists ONLY for a Closed day
A Closed day is immutable — no edit, no delete, no reopen
Server never accepts actualQuantity from client on Close
```

Các dòng còn lại của §47 — `Actual total <= Order.Quantity`, các invariant Adjustment, `Preview does not persist`, `HttpOnly Cookie Authentication`, `PostgreSQL`, `Transaction + Row Locking` — **giữ nguyên hiệu lực tuyệt đối**.

## 14.2. Định nghĩa "hôm nay" — múi giờ

Đây là lỗ hổng dễ gây bug nhất của CR, vì ba rule đều phụ thuộc vào nó: `FUTURE_DATE_NOT_ALLOWED`, `unclosedPastDays`, và ngày ứng viên nhận add-on.

```text
Business timezone: Asia/Ho_Chi_Minh (UTC+7)

Today := ngày lịch hiện tại theo Asia/Ho_Chi_Minh,
         KHÔNG phải theo UTC, KHÔNG phải theo timezone của server
```

Cụ thể:

- So sánh `production_date` với `Today` phải thực hiện ở **backend**, không để frontend tự tính rồi gửi lên.
- Không dùng `DateTime.UtcNow.Date` trực tiếp. Dùng `TimeZoneInfo` cố định của nghiệp vụ, cấu hình một chỗ duy nhất.
- Lý do: server chạy UTC, lúc 00:30 giờ Việt Nam thì UTC vẫn là ngày hôm trước. Nếu so sánh bằng UTC, quản lý ghi nhận sớm sẽ bị `FUTURE_DATE_NOT_ALLOWED` một cách vô lý.
- `recorded_at`, `closed_at` vẫn lưu `timestamptz` UTC, frontend chỉ đổi khi hiển thị — đúng Implementation Prompt §38.
- `closed_at` khi đóng ngày quá khứ = **thời điểm thực tế bấm nút**, không backdate về ngày đó.

## 14.3. `dayStatus` cần giá trị thứ tư: `NotStarted`

Enum ở Mục 4.3 và 6.3 thiếu một trường hợp: **ngày tương lai đã có kế hoạch nhưng chưa tới**. MH4 phải hiển thị "Chưa tới" cho các ngày này.

Bảng suy ra đầy đủ:

```text
plannedQuantity = 0                                  → NoPlan
productionDate > Today                               → NotStarted
row production_days NULL hoặc status = 'Open'        → InProduction
status = 'Closed'                                    → Closed
```

Thứ tự kiểm tra quan trọng: `NoPlan` xét trước `NotStarted`.

`NotStarted` chỉ tồn tại ở tầng DTO, **không lưu vào DB**.

## 14.4. Quy tắc tạo dòng `production_days`

```text
Tạo lazily, chỉ khi:
    - entry đầu tiên của ngày được ghi nhận, HOẶC
    - người dùng bấm Xuất hàng cho ngày chưa có dòng nào

Điều kiện bắt buộc trước khi tạo:
    tồn tại production_plans cho (order_id, production_date)
    AND planned_quantity > 0
```

Không tạo sẵn dòng cho toàn bộ khoảng `start_date → due_date` lúc tạo đơn. Lý do: sẽ sinh rác cho ngày không có kế hoạch, và làm `unclosedPastDays` báo sai.

## 14.5. `unclosedPastDays` phải tính cả ngày CHƯA có dòng

Hệ quả trực tiếp của 14.4. Nếu chỉ query `production_days WHERE status = 'Open'`, ngày quá khứ có kế hoạch mà **hoàn toàn không nhập gì** sẽ không xuất hiện — đúng cái case cần cảnh báo nhất.

```sql
SELECT o.id, o.order_code, pp.production_date, pp.planned_quantity
FROM production_plans pp
JOIN orders o ON o.id = pp.order_id
LEFT JOIN production_days pd
       ON pd.order_id = pp.order_id
      AND pd.production_date = pp.production_date
WHERE pp.planned_quantity > 0
  AND pp.production_date < :today          -- :today theo 14.2
  AND o.status = 'Incomplete'
  AND (pd.id IS NULL OR pd.status = 'Open')
ORDER BY pp.production_date;
```

Nguồn dữ liệu là `production_plans`, **không phải** `production_days`.

## 14.6. Đơn đã `Completed` nhưng còn ngày kế hoạch phía sau

Trường hợp biên mới do OV-4 tạo ra: đơn hoàn thành ở ngày N, nhưng ngày N+1, N+2 vẫn còn `planned_quantity > 0` do add-on trước đó.

Quy tắc:

| Thao tác trên ngày sau khi Order = Completed | Kết quả |
|---|---|
| POST entry | 409 `ORDER_ALREADY_COMPLETED` |
| Close day | **Cho phép**, để quản lý dọn sạch các ngày còn treo |
| Shortage sinh ra từ việc close đó | **Bỏ qua** — không hiển thị lối vào Xử lý thiếu |
| Preview/Apply adjustment | 422 `ORDER_ALREADY_COMPLETED` |

Lý do cho phép Close: nếu không, các ngày đó nằm mãi trong `unclosedPastDays` và cảnh báo đỏ trên Dashboard không bao giờ tắt được.

MH4 hiển thị các ngày này với nhãn *Không cần sản xuất*.

## 14.7. Chống double-submit khi ghi nhận

`production_entries` **cố ý không có** unique constraint — hai lần nhập 15 đôi cách nhau 5 phút là hợp lệ và phải ghi thành 2 dòng. Nghĩa là cơ chế chống trùng dựa trên unique index ở Implementation Prompt §24 **không áp dụng được** cho entry.

Chốt:

- **Frontend:** disable nút Ghi nhận trong lúc mutation pending; không cho Enter lặp. Đây là tuyến phòng thủ chính.
- **Backend:** không thêm Idempotency table (giữ nguyên nguyên tắc không over-engineer của Phase 1).
- **Chấp nhận rủi ro:** nếu double-submit lọt qua, quản lý **xoá được** entry thừa vì ngày còn Open. Đây là lý do rủi ro này chấp nhận được.

Ngược lại, **Close day có** bảo vệ ở tầng server: lần gọi thứ hai trả `409 DAY_ALREADY_CLOSED` (AC-08). Đây mới là thao tác không hoàn tác được nên phải chặn ở backend.

## 14.8. Testing requirements — thay thế Implementation Prompt §43

Nhóm **Order** và nhóm **Adjustment** giữ nguyên, trừ việc bổ sung điều kiện ngày Closed. Nhóm **Actual** và **Shortage** thay bằng:

```text
## Production Entry
Entry quantity > 0 (entry = 0 bị từ chối)
N entries hợp lệ trong cùng một ngày
SUM(entries) <= plannedQuantity của ngày
SUM(entries) <= remaining của Order (ràng buộc nào chặt hơn thì thắng)
Không ghi nhận được khi plannedQuantity = 0
Không ghi nhận được cho ngày tương lai
Không ghi nhận / sửa / xoá được khi ngày Closed
Soft delete: entry đã xoá không tính vào mọi phép SUM
Update entry validate lại bằng công thức thay thế

## Production Day
Dòng production_days tạo lazily, chỉ khi có plan > 0
Close snapshot đúng SUM(entries) tại thời điểm đóng
Close hai lần → 409
Close với 0 entry hợp lệ, shortage = plannedQuantity
Order chuyển Completed CHỈ tại thời điểm Close
TotalActual = Order.Quantity mà chưa Close → Order vẫn Incomplete

## Shortage
Ngày Open → shortage = null (KHÔNG phải 0)
Ngày Closed → shortage = plannedQuantity - actualQuantity, luôn >= 0

## Concurrency  (bổ sung cho §43)
Hai POST entry đồng thời làm tổng vượt trần ngày → đúng 1 request thành công
POST entry đồng thời với Close day → không tạo được entry sau khi đã đóng
Hai Close day đồng thời → đúng 1 request thành công
```

Test `Ngày Open → shortage = null (KHÔNG phải 0)` là test quan trọng nhất của CR này: nhầm `null` thành `0` sẽ khiến toàn bộ Dashboard báo "đạt kế hoạch" cho những ngày còn đang sản xuất.

## 14.9. EF Core — hai bẫy cụ thể

**Soft delete:** khai báo global query filter cho `ProductionEntry`:

```csharp
modelBuilder.Entity<ProductionEntry>()
    .HasQueryFilter(e => e.DeletedAt == null);
```

Nếu quên, mọi phép `SUM` sẽ cộng cả entry đã xoá và toàn bộ số liệu sai. Nhưng lưu ý: khi truy vấn `production_entry_logs` để dựng lịch sử đầy đủ, phải `IgnoreQueryFilters()` để thấy cả entry đã xoá.

**Row lock:** thứ tự khoá thống nhất toàn hệ thống là **Order → ProductionDay → ProductionPlan**. EF Core không có API khoá trực tiếp, dùng raw SQL `SELECT ... FOR UPDATE` theo đúng thứ tự này ở đầu mỗi transaction. Khoá sai thứ tự giữa CreateEntry và ApplyAdjustment sẽ gây deadlock.
