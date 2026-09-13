# CR-001 — Nhập hàng, Tiến độ và Dây chuyền sản xuất

| Mục | Nội dung |
|---|---|
| Mã CR | CR-001 |
| Ngày | 2026-09-07 |
| Trạng thái | Approved — đã triển khai |
| Cập nhật | 2026-09-13 — đồng bộ các thay đổi yêu cầu phát sinh sau khi duyệt (xem Mục 13) |
| Baseline | Step 1–5 (Domain, Data, Database, API, Frontend) + toàn bộ screen spec + **CR-01** (ghi nhận nhiều lần trong ngày & Xuất hàng) |
| Loại thay đổi | Breaking change (domain + schema + API + navigation) |
| Môi trường | Chưa có dữ liệu sản xuất thật — **được phép reset schema** |

---

# 1. Bối cảnh

Baseline hiện tại coi `Order` là một thực thể tạo trọn vẹn trong một lần: mã đơn + số lượng + ngày bắt đầu + ngày kết thúc + kế hoạch theo ngày, tất cả trong một transaction.

Thực tế nghiệp vụ tách làm hai thời điểm khác nhau:

1. **Nhập hàng** — hàng về, biết mã giày, có ảnh mẫu, biết số lượng cần làm. Chưa biết chạy dây chuyền nào, chưa biết bắt đầu ngày nào.
2. **Lập tiến độ** — quyết định dây chuyền, khoảng thời gian và phân bổ sản lượng theo từng ngày trên từng dây chuyền.

CR này tách hai bước đó, bổ sung danh mục dây chuyền sản xuất, và chuyển việc ghi nhận kế hoạch/sản lượng từ mức *ngày* sang mức *ngày × dây chuyền*.

> **Quan hệ với CR-01.** CR-01 (2026-09-02) đã thay mô hình sản lượng của baseline: sản lượng một ngày là **tổng nhiều lần ghi nhận**, ngày được **chốt sổ bằng Xuất hàng**, bảng `production_records` đổi tên thành `production_days`. CR-001 giữ nguyên toàn bộ ngữ nghĩa đó và chỉ hạ đơn vị từ *ngày của đơn* xuống *ô* = `(đơn hàng, ngày, dây chuyền)`. Mọi quy tắc CR-01 viết cho "một ngày sản xuất" nay áp cho **từng ô**. Chỗ nào trong tài liệu này còn khác CR-01 thì CR-01 được ưu tiên.

---

# 2. Các quyết định đã chốt

| # | Quyết định |
|---|---|
| 1 | Một đơn hàng có **đúng một tiến độ**. Không tạo entity/bảng `ProductionRun`. "Tiến độ" là phần kế hoạch của `Order`. |
| 2 | Một tiến độ chạy trên **nhiều dây chuyền**. |
| 3 | Kế hoạch và sản lượng thực tế ghi nhận theo **ngày × dây chuyền**. |
| 4 | **Mã giày thay luôn mã đơn hàng**, unique toàn hệ thống. `orders.order_code` → `orders.shoe_code`. |
| 5 | **Mỗi đơn hàng có tối đa 1 ảnh.** Không tạo bảng ảnh riêng; lưu trực tiếp trên `orders`. |
| 6 | Ảnh lưu trên **local disk** của server, DB chỉ giữ đường dẫn tương đối. Serve qua endpoint có xác thực cookie. Giới hạn ≤ 5MB, chỉ jpg/png/webp. |
| 7 | `orders.status` có **3 giá trị**: `Pending` (chưa lập tiến độ) → `Incomplete` → `Completed`. |
| 8 | `start_date` / `due_date` thuộc về **tiến độ**, nullable, chỉ có giá trị khi đã lập tiến độ. |
| 9 | Dây chuyền **không xoá cứng**, chỉ `Active` / `Inactive`. Chưa có năng suất/ngày. |
| 10 | **Bù sản lượng thiếu chỉ trong cùng dây chuyền.** Không bù chéo dây chuyền ở Phase này. |
| 11 | Giữ nguyên tên entity backend `Order`. Không đổi tên bảng/DTO chỉ vì nhãn UI. |
| 12 | Aggregate Root vẫn là `Order`. `production_plans` / `production_days` giữ nguyên `order_id`. |
| 13 | Số lượng kế hoạch **luôn thuộc về đơn hàng**. Lập tiến độ là việc **phân bổ 2 tầng**: đơn → dây chuyền → ngày. |
| 14 | Tầng đơn → dây chuyền có 2 mode: **Tự động chia đều** và **Nhập tay**. Tổng phân bổ **bắt buộc bằng** số lượng đơn. |
| 15 | Tầng dây chuyền → ngày: hệ thống chia đều làm giá trị khởi tạo, **user được sửa từng ô**, miễn tổng của dây chuyền khớp số đã phân cho dây chuyền đó. |
| 16 | Chia đều có dư thì **dồn phần dư vào các phần tử đầu**, áp dụng cho cả 2 tầng. |

---

# 3. Phạm vi

## In scope

- Màn hình Nhập hàng (danh sách + tạo/sửa + upload ảnh).
- Danh mục Dây chuyền sản xuất (cấu hình).
- Màn hình Tiến độ (đổi tên từ Đơn hàng) + màn hình tạo tiến độ.
- Ma trận kế hoạch/sản lượng theo ngày × dây chuyền.
- Điều chỉnh logic nhập sản lượng, xử lý thiếu và adjustment theo mức ô.
- Cập nhật navigation, dashboard, statistics.

## Out of scope

- Năng suất (capacity) của dây chuyền.
- Bù sản lượng chéo dây chuyền.
- Nhiều ảnh / thư viện ảnh cho một đơn hàng.
- Nhiều tiến độ cho một đơn hàng.
- Quản lý size, màu, nguyên vật liệu, công đoạn.
- Phân quyền theo dây chuyền.

---

# 4. Thay đổi nghiệp vụ

## 4.1 Vòng đời đơn hàng

```text
Nhập hàng                  Lập tiến độ                Sản xuất
─────────────              ─────────────              ─────────────
mã giày                    chọn dây chuyền            ghi nhận sản lượng theo ô
1 ảnh                      ngày bắt đầu / kết thúc    Xuất hàng từng ô
số lượng                   kế hoạch ngày × dây chuyền xử lý thiếu
     │                            │                         │
     ▼                            ▼                         ▼
  Pending      ──────────►    Incomplete    ◄────────►  Completed
```

- `Pending → Incomplete`: khi lập tiến độ thành công.
- Trạng thái đơn **chỉ được đánh giá tại thời điểm Xuất hàng một ô** (CR-01 OV-4): `SUM(actual) >= order.quantity` → `Completed`, ngược lại → `Incomplete`. Ghi nhận, sửa, xoá lần ghi nhận trong ngày không làm đổi trạng thái đơn.
- **Không bao giờ** quay lại `Pending`.

## 4.2 Business rule mới

| ID | Rule |
|---|---|
| BR-N01 | `shoe_code` bắt buộc, unique toàn hệ thống, không được trùng. |
| BR-N02 | Mỗi đơn hàng có tối đa 1 ảnh. Upload ảnh mới sẽ thay thế ảnh cũ. |
| BR-N03 | Ảnh: định dạng jpg/jpeg/png/webp, dung lượng ≤ 5MB. |
| BR-N04 | Đơn `Pending` chưa có `start_date`, `due_date`, `production_plans`, dây chuyền. |
| BR-N05 | Chỉ được lập tiến độ cho đơn ở trạng thái `Pending`. Lập tiến độ là thao tác **một lần**. |
| BR-N06 | Tiến độ phải chọn **ít nhất 1 dây chuyền** đang `Active`. |
| BR-N07 | Mỗi dây chuyền được chọn phải được phân bổ số lượng **> 0**. Không cho chọn dây chuyền rồi để trống. |
| BR-N08 | **Tầng 1** — `SUM(order_production_lines.allocated_quantity) = order.quantity`. Bắt buộc bằng, không được nhỏ hơn hay lớn hơn. |
| BR-N08b | **Tầng 2** — với mỗi dây chuyền: `SUM(initial_planned_quantity của dây chuyền đó) = allocated_quantity của chính nó`. |
| BR-N08c | Hai rule trên kéo theo `SUM(initial_planned_quantity)` toàn bộ ô `= order.quantity`, giữ đúng invariant baseline. |
| BR-N09 | Mỗi ô `(order, ngày, dây chuyền)` có tối đa 1 plan và tối đa 1 dòng `production_days`; một ô có nhiều lần ghi nhận (`production_entries`, CR-01). |
| BR-N10 | Ô không có kế hoạch (không có dòng plan, hoặc `planned_quantity = 0`) không ghi nhận sản lượng và không Xuất hàng được — `422 DAY_HAS_NO_PLAN`. |
| BR-N11 | Thiếu (shortage) tính theo **từng ô** và **chỉ tồn tại khi ô đã Xuất hàng** (CR-01 OV-5): `shortage = planned_quantity - actual_quantity` khi dương. Ô còn mở không có thiếu. |
| BR-N12 | Bù thiếu chỉ được chọn ngày đích **thuộc chính dây chuyền** phát sinh thiếu, và không phải ngày đã qua. |
| BR-N13 | Automatic adjustment chia đều trên các ngày còn lại **của cùng dây chuyền**. |
| BR-N14 | Dây chuyền `Inactive` không xuất hiện trong lựa chọn mới, nhưng dữ liệu lịch sử tham chiếu tới nó vẫn hiển thị bình thường. |
| BR-N15 | Không xoá dây chuyền đã được sử dụng trong bất kỳ đơn hàng nào. |
| BR-N16 | Mode phân bổ đơn → dây chuyền: `Even` (hệ thống chia đều) hoặc `Manual` (user nhập từng dây chuyền). Mode chỉ là công cụ nhập liệu, không lưu vào DB. |
| BR-N17 | Quy tắc chia đều có dư: phần dư `r = tổng mod n` được cộng 1 vào `r` phần tử đầu tiên. Dây chuyền sắp theo `sort_order`, ngày sắp tăng dần. |
| BR-N18 | `allocated_quantity` của dây chuyền là **giá trị bất biến sau khi tạo tiến độ**, dùng làm mốc đối chiếu. Adjustment làm `planned_quantity` tăng nhưng không đổi `allocated_quantity`. |
| BR-N19 | Chỉ xoá được đơn `Pending`. Xoá là **xoá cứng**, kèm file ảnh. Đơn đã lập tiến độ không xoá được — `422 ORDER_NOT_DELETABLE`. Luật "không hard-delete đơn đã có dữ liệu sản xuất" của baseline vẫn nguyên, vì đơn `Pending` chưa có kế hoạch hay sản lượng nào. |
| BR-N20 | Sửa thông tin nhập hàng: mã giày và ảnh sửa được ở mọi trạng thái; **số lượng chỉ sửa được khi đơn còn `Pending`** — sau khi lập tiến độ, số lượng là mốc của BR-N08, đổi riêng nó sẽ phá bất biến (`422 ORDER_SCHEDULE_ALREADY_EXISTS`). Đơn đã qua ngày kết thúc thì không sửa được gì (luật đóng băng của baseline, `ORDER_OVERDUE`). |

## 4.3 Business rule được giữ nguyên

Các invariant sau **không thay đổi** và không được nới lỏng vì bất kỳ lý do triển khai nào:

- `SUM(actual) <= order.quantity` — tính trên toàn bộ ô của đơn hàng.
- Sản lượng của một ô là **tổng các lần ghi nhận** trong ô đó, và tổng đó không vượt kế hoạch của ô (CR-01 OV-2, OV-3 — đã thay quy tắc "sản lượng là giá trị, không cộng dồn" của baseline).
- Ô đã Xuất hàng là **bất biến**: không ghi nhận thêm, không sửa, không xoá, không mở lại (CR-01).
- `initial_planned_quantity` không bao giờ thay đổi sau khi tạo.
- `SUM(planned_quantity)` sau adjustment **có thể lớn hơn** `order.quantity`. Không giảm kế hoạch ngày khác để cân lại.
- Adjustment đã Applied là lịch sử bất biến. Sửa thì Reverse rồi tạo mới.
- Một `source_production_plan` chỉ có tối đa một adjustment `Applied` tại một thời điểm.
- Preview không persist.
- Đơn `Completed`: không ghi nhận thêm sản lượng, không xử lý thiếu; **vẫn Xuất hàng được** các ô còn treo để dọn sạch cảnh báo (CR-01 §14.6).
- Đơn đã qua ngày kết thúc bị đóng băng: chỉ xem, mọi thao tác ghi đều bị chặn (`ORDER_OVERDUE`) — áp cho cả đơn đã hoàn thành.
- Activity/Audit không được xoá.

## 4.4 Rule bị thay thế

| Rule cũ | Thay bằng |
|---|---|
| Tạo Order kèm kế hoạch trong cùng một transaction | Tách 2 bước: tạo đơn (`Pending`) → lập tiến độ |
| `UNIQUE(order_id, production_date)` | `UNIQUE(order_id, production_date, production_line_id)` |
| `production_days` (CR-01): 1 dòng / (đơn, ngày), Xuất hàng theo ngày của đơn | 1 dòng / ô (đơn, ngày, dây chuyền), **Xuất hàng theo từng ô** |
| Shortage tính theo ngày | Shortage tính theo ô ngày × dây chuyền |
| Order status chỉ `Incomplete` / `Completed` | Thêm `Pending` |
| Tìm kiếm theo mã đơn hàng | Tìm kiếm theo mã giày |

---

# 5. Thay đổi Database

Quy ước giữ nguyên baseline: PostgreSQL, PK/FK `uuid` (UUIDv7 sinh ở application), `varchar + CHECK` thay cho native ENUM, `date` cho ngày nghiệp vụ, `timestamptz` cho audit, `ON DELETE RESTRICT` cho mọi quan hệ nghiệp vụ.

## 5.1 `production_lines` — bảng mới

```sql
CREATE TABLE production_lines (
    id          uuid PRIMARY KEY,
    code        varchar(30)  NOT NULL,
    name        varchar(100) NOT NULL,
    status      varchar(20)  NOT NULL,
    sort_order  integer      NOT NULL DEFAULT 0,
    note        varchar(500) NULL,
    created_at  timestamptz  NOT NULL,
    updated_at  timestamptz  NOT NULL,

    CONSTRAINT uq_production_lines_code UNIQUE (code),
    CONSTRAINT ck_production_lines_status
        CHECK (status IN ('Active', 'Inactive'))
);
```

## 5.2 `orders` — sửa

```sql
CREATE TABLE orders (
    id                  uuid PRIMARY KEY,
    shoe_code           varchar(50)  NOT NULL,
    quantity            integer      NOT NULL,

    image_path          varchar(500) NULL,
    image_file_name     varchar(255) NULL,
    image_content_type  varchar(100) NULL,
    image_size_bytes    integer      NULL,

    start_date          date         NULL,
    due_date            date         NULL,
    status              varchar(20)  NOT NULL,

    created_at          timestamptz  NOT NULL,
    updated_at          timestamptz  NOT NULL,

    CONSTRAINT uq_orders_shoe_code UNIQUE (shoe_code),
    CONSTRAINT ck_orders_quantity CHECK (quantity > 0),
    CONSTRAINT ck_orders_status
        CHECK (status IN ('Pending', 'Incomplete', 'Completed')),
    CONSTRAINT ck_orders_schedule_dates
        CHECK (
            (status =  'Pending' AND start_date IS NULL AND due_date IS NULL)
         OR (status <> 'Pending' AND start_date IS NOT NULL AND due_date IS NOT NULL
             AND start_date <= due_date)
        ),
    CONSTRAINT ck_orders_image
        CHECK (
            (image_path IS NULL AND image_file_name IS NULL
             AND image_content_type IS NULL AND image_size_bytes IS NULL)
         OR (image_path IS NOT NULL AND image_file_name IS NOT NULL
             AND image_content_type IS NOT NULL AND image_size_bytes IS NOT NULL
             AND image_size_bytes > 0)
        )
);
```

Thay đổi so với baseline: `order_code` → `shoe_code`; thêm 4 cột ảnh; `start_date` / `due_date` thành nullable; `status` thêm `Pending`.

## 5.3 `order_production_lines` — bảng mới

Dây chuyền được gán cho một đơn hàng.

```sql
CREATE TABLE order_production_lines (
    order_id            uuid NOT NULL,
    production_line_id  uuid NOT NULL,
    allocated_quantity  integer NOT NULL,
    created_at          timestamptz NOT NULL,

    CONSTRAINT pk_order_production_lines
        PRIMARY KEY (order_id, production_line_id),

    CONSTRAINT fk_order_production_lines_order
        FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE RESTRICT,

    CONSTRAINT fk_order_production_lines_line
        FOREIGN KEY (production_line_id) REFERENCES production_lines(id) ON DELETE RESTRICT,

    CONSTRAINT ck_order_production_lines_allocated
        CHECK (allocated_quantity > 0)
);
```

Bảng này giữ hai vai trò:

1. **Nguồn xác định dây chuyền nào thuộc tiến độ**. Không suy ra từ `production_plans`.
2. **Lưu số lượng phân bổ tầng 1** (`allocated_quantity`). Không thể suy ngược từ tổng `planned_quantity` vì adjustment sẽ làm con số đó tăng lên; mất `allocated_quantity` là mất mốc đối chiếu gốc.

Bất biến chéo bảng, do application + transaction bảo vệ:

```text
SUM(allocated_quantity theo order) = orders.quantity

với mỗi dây chuyền L:
SUM(production_plans.initial_planned_quantity WHERE line = L) = allocated_quantity(L)
```

## 5.4 `production_plans` — sửa

```sql
ALTER TABLE production_plans
    ADD COLUMN production_line_id uuid NOT NULL;

ALTER TABLE production_plans
    ADD CONSTRAINT fk_production_plans_line
        FOREIGN KEY (production_line_id)
        REFERENCES production_lines(id) ON DELETE RESTRICT;

-- Thay unique cũ
ALTER TABLE production_plans
    DROP CONSTRAINT uq_production_plans_order_date;

ALTER TABLE production_plans
    ADD CONSTRAINT uq_production_plans_order_date_line
        UNIQUE (order_id, production_date, production_line_id);
```

## 5.5 `production_days` — sửa

CR-01 đã đổi tên `production_records` thành `production_days` và thêm `production_entries` / `production_entry_logs` treo dưới nó. CR-001 chỉ gắn dây chuyền vào `production_days`; hai bảng entry **không đổi cấu trúc** vì chúng trỏ tới `production_day_id`, vốn đã mang thông tin dây chuyền.

```sql
ALTER TABLE production_days
    ADD COLUMN production_line_id uuid NOT NULL;

ALTER TABLE production_days
    ADD CONSTRAINT fk_production_days_line
        FOREIGN KEY (production_line_id)
        REFERENCES production_lines(id) ON DELETE RESTRICT;

ALTER TABLE production_days
    DROP CONSTRAINT uq_production_days_order_date;

ALTER TABLE production_days
    ADD CONSTRAINT uq_production_days_order_date_line
        UNIQUE (order_id, production_date, production_line_id);
```

Dòng `production_days` vẫn được tạo lazily như CR-01 §14.4, nay theo ô: ở lần ghi nhận đầu tiên của ô, hoặc khi Xuất hàng một ô chưa có dòng nào, và chỉ khi ô đó có kế hoạch > 0.

## 5.6 `plan_adjustments` / `plan_adjustment_items`

**Không thay đổi cấu trúc.** `source_production_plan_id` vốn trỏ tới một dòng plan; nay dòng đó đã mang thông tin dây chuyền. Chỉ bổ sung ràng buộc ở tầng application (BR-N12): mọi `production_plan_id` trong items phải cùng `production_line_id` với source plan.

## 5.7 Index bổ sung

```sql
CREATE INDEX ix_production_plans_line   ON production_plans (production_line_id);
CREATE INDEX ix_production_days_line    ON production_days (production_line_id);
CREATE INDEX ix_orders_status           ON orders (status);
CREATE INDEX ix_order_production_lines_line
    ON order_production_lines (production_line_id);
```

## 5.8 Chiến lược migration

Môi trường chưa có dữ liệu sản xuất thật, nên:

- Migration **xoá sạch dữ liệu nghiệp vụ rồi ALTER** thay vì cố gắng backfill: `plan_adjustment_items`, `plan_adjustments`, `production_entry_logs`, `production_entries`, `production_days`, `production_plans`, `orders`. Phải xoá trước vì cột `production_line_id NOT NULL` có khoá ngoại, và đơn cũ không thoả `ck_orders_schedule_dates`.
- Giữ nguyên `users` và `system_settings`.
- Seed sẵn 2–3 dây chuyền mẫu cho môi trường dev (`Active`), **không** hard-code trong migration production.
- Tạo thư mục lưu ảnh khi ứng dụng khởi động nếu chưa tồn tại.

---

# 6. Thay đổi API

Base path giữ nguyên `/api/v1`. Current user vẫn lấy từ authentication context; client không gửi `createdBy` / `updatedBy`.

## 6.1 Endpoint mới

```text
GET    /api/v1/production-lines
POST   /api/v1/production-lines
PUT    /api/v1/production-lines/{productionLineId}
PATCH  /api/v1/production-lines/{productionLineId}/status

PUT    /api/v1/orders/{orderId}
DELETE /api/v1/orders/{orderId}

PUT    /api/v1/orders/{orderId}/image
DELETE /api/v1/orders/{orderId}/image
GET    /api/v1/orders/{orderId}/image/content

POST   /api/v1/orders/{orderId}/production-schedule
```

## 6.2 Endpoint sửa (breaking)

```text
POST /api/v1/orders
GET  /api/v1/orders
GET  /api/v1/orders/{orderId}
GET  /api/v1/orders/{orderId}/production-plans

# Nhóm ô sản xuất — endpoint ngày của CR-01, nay định danh theo ô (thêm dây chuyền)
GET  /api/v1/orders/{orderId}/production-days/{productionDate}/lines/{productionLineId}
POST /api/v1/orders/{orderId}/production-days/{productionDate}/lines/{productionLineId}/entries
POST /api/v1/orders/{orderId}/production-days/{productionDate}/lines/{productionLineId}/close
PUT  /api/v1/production-entries/{entryId}
DELETE /api/v1/production-entries/{entryId}

POST /api/v1/production-plans/{productionPlanId}/adjustments/preview
POST /api/v1/production-plans/{productionPlanId}/adjustments
GET  /api/v1/orders/{orderId}/statistics
GET  /api/v1/statistics/dashboard
```

## 6.3 Dây chuyền sản xuất

**GET /api/v1/production-lines**

Query: `status` (`Active` | `Inactive` | bỏ trống = tất cả).

```json
{
  "items": [
    {
      "id": "uuid",
      "code": "DC-01",
      "name": "Dây chuyền 1",
      "status": "Active",
      "sortOrder": 1,
      "note": null,
      "inUse": true
    }
  ]
}
```

`inUse` = đã được gán cho ít nhất một đơn hàng. Frontend dùng để ẩn hành động xoá.

**POST / PUT**

```json
{
  "code": "DC-01",
  "name": "Dây chuyền 1",
  "sortOrder": 1,
  "note": null
}
```

**PATCH /{id}/status**

```json
{ "status": "Inactive" }
```

Rule: chuyển sang `Inactive` luôn được phép, kể cả khi đang được dùng — chỉ ảnh hưởng tới lựa chọn mới.

## 6.4 Tạo đơn hàng (Nhập hàng)

**POST /api/v1/orders** — `multipart/form-data`

| Field | Kiểu | Bắt buộc |
|---|---|---|
| `shoeCode` | text | Có |
| `quantity` | text (integer) | Có |
| `image` | file | Không |

Response `201`:

```json
{
  "id": "uuid",
  "shoeCode": "SH-2026-001",
  "quantity": 1000,
  "status": "Pending",
  "hasImage": true,
  "imageUrl": "/api/v1/orders/{orderId}/image/content",
  "startDate": null,
  "dueDate": null,
  "productionLines": [],
  "totalActual": 0,
  "remaining": 1000,
  "progressPercentage": 0
}
```

Không còn mảng `productionPlans` trong request.

**PUT /api/v1/orders/{orderId}** — `multipart/form-data`. Sửa thông tin nhập hàng và ảnh trong **một lần lưu**: hoặc mọi thay đổi được lưu, hoặc không thay đổi nào.

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `shoeCode` | text | Có | Unique, như lúc tạo |
| `quantity` | text (integer) | Có | Chỉ đổi được khi đơn còn `Pending` (BR-N20) |
| `image` | file | Không | Có thì thay ảnh cũ |
| `removeImage` | boolean | Không | `true` thì gỡ ảnh; gửi kèm `image` cùng lúc → `400 VALIDATION_ERROR` |

Thứ tự ghi giống lúc tạo: validate xong mới ghi file mới → commit database → xoá file cũ. Commit hỏng thì dọn file mới. Response `200` là chi tiết đơn hàng.

**DELETE /api/v1/orders/{orderId}** — xoá cứng một đơn `Pending` kèm file ảnh (BR-N19). Khoá dòng đơn trước khi kiểm trạng thái để không đua với request lập tiến độ chạy song song. Response `204`. Đơn đã lập tiến độ → `422 ORDER_NOT_DELETABLE`.

**PUT /api/v1/orders/{orderId}/image** — `multipart/form-data`, field `image`. Thay thế ảnh cũ, xoá file cũ khỏi disk sau khi commit thành công.

**DELETE /api/v1/orders/{orderId}/image** — xoá ảnh, set 4 cột ảnh về `NULL`.

**GET /api/v1/orders/{orderId}/image/content** — trả bytes, `Content-Type` theo `image_content_type`, yêu cầu authentication. Không expose đường dẫn vật lý ra ngoài.

## 6.5 Danh sách đơn hàng

**GET /api/v1/orders**

Query: `status`, `search` (theo `shoeCode`), `productionLineId`, `page`, `pageSize`.

`status` nhận thêm giá trị `Scheduled` = gộp `Incomplete` + `Completed` — đúng tập đơn mà màn Tiến độ hiển thị (§7.7); không truyền = tất cả.

Mỗi item bổ sung: `shoeCode`, `hasImage`, `imageUrl`, `productionLines[]`, `status` (3 giá trị), `startDate` / `dueDate` (null khi `Pending`), `hasUnclosedPastCell` (có ô đã qua mà chưa Xuất hàng).

## 6.6 Lập tiến độ

**POST /api/v1/orders/{orderId}/production-schedule**

Request lồng theo dây chuyền để phản ánh đúng cấu trúc phân bổ 2 tầng:

```json
{
  "startDate": "2026-09-10",
  "dueDate": "2026-09-12",
  "allocationMode": "Manual",
  "lines": [
    {
      "productionLineId": "line-1-uuid",
      "allocatedQuantity": 570,
      "plans": [
        { "productionDate": "2026-09-10", "plannedQuantity": 100 },
        { "productionDate": "2026-09-11", "plannedQuantity": 120 },
        { "productionDate": "2026-09-12", "plannedQuantity": 350 }
      ]
    },
    {
      "productionLineId": "line-2-uuid",
      "allocatedQuantity": 430,
      "plans": [
        { "productionDate": "2026-09-10", "plannedQuantity":  80 },
        { "productionDate": "2026-09-11", "plannedQuantity": 100 },
        { "productionDate": "2026-09-12", "plannedQuantity": 250 }
      ]
    }
  ]
}
```

`allocationMode` (`Even` | `Manual`) chỉ mang tính khai báo để backend log/audit; backend **luôn validate con số thật** trong `lines`, không tự tính lại theo mode.

Validation, theo thứ tự:

1. Đơn tồn tại và `status = Pending`, ngược lại `409 ORDER_SCHEDULE_ALREADY_EXISTS`.
2. `startDate <= dueDate`.
3. `lines` không rỗng, `productionLineId` không trùng, mọi dây chuyền tồn tại và `Active`.
4. Mọi `allocatedQuantity > 0`.
5. **Tầng 1**: `SUM(allocatedQuantity) == order.quantity`, ngược lại `422 LINE_ALLOCATION_MISMATCH`.
6. Mọi `productionDate` nằm trong `[startDate, dueDate]`, không trùng trong cùng một dây chuyền.
7. `plannedQuantity >= 0`.
8. **Tầng 2**: với mỗi dây chuyền, `SUM(plannedQuantity) == allocatedQuantity`, ngược lại `422 LINE_PLAN_TOTAL_MISMATCH` kèm `details` chỉ rõ dây chuyền nào lệch và lệch bao nhiêu.

Transaction:

```text
BEGIN
    Lock Order
    Validate status = Pending
    Insert order_production_lines (allocated_quantity)
    Insert production_plans (initial_planned_quantity = planned_quantity = plannedQuantity)
    Update orders.start_date, due_date, status = 'Incomplete'
COMMIT
```

## 6.6b Thuật toán chia đều

Dùng chung cho cả hai tầng và cho automatic adjustment.

```text
chiaDeu(tong, n):
    base = tong / n        (chia lấy nguyên)
    du   = tong % n
    trả về: du phần tử đầu nhận (base + 1), các phần tử còn lại nhận base
```

Tầng 1 — thứ tự phần tử theo `production_lines.sort_order`, rồi `code`:

```text
1.000 đôi / 3 dây chuyền → 334 / 333 / 333
```

Tầng 2 — thứ tự phần tử theo ngày tăng dần:

```text
334 đôi / 5 ngày → 67 / 67 / 67 / 67 / 66
```

Chia đều chỉ sinh **giá trị khởi tạo** ở tầng 2; user sửa lại từng ô được, backend chỉ kiểm tổng theo BR-N08b.

Các ô không gửi lên được coi là **không có kế hoạch** — không tự động tạo dòng plan = 0. Ô không có plan thì không ghi nhận sản lượng được (nhất quán BR-N10).

## 6.7 Kế hoạch & sản lượng

**GET /api/v1/orders/{orderId}/production-plans**

```json
{
  "orderId": "uuid",
  "startDate": "2026-09-10",
  "dueDate": "2026-09-14",
  "productionLines": [
    {
      "id": "line-1-uuid",
      "code": "DC-01",
      "name": "Dây chuyền 1",
      "status": "Active",
      "allocatedQuantity": 570,
      "currentPlanQuantity": 600,
      "actualQuantity": 480
    }
  ],
  "items": [
    {
      "id": "plan-uuid",
      "productionDate": "2026-09-10",
      "productionLineId": "line-1-uuid",
      "initialPlannedQuantity": 100,
      "addOnQuantity": 20,
      "plannedQuantity": 120,
      "dayStatus": "Closed",
      "actualQuantity": 90,
      "isProvisional": false,
      "productionDayId": "day-uuid",
      "shortageQuantity": 30,
      "difference": -30,
      "closedAt": "2026-09-10T10:05:00Z",
      "hasActiveAdjustment": false,
      "activeAdjustmentId": null,
      "lastRecordedBy": "admin",
      "lastRecordedAt": "2026-09-10T09:40:00Z"
    }
  ]
}
```

Frontend không phải tự join. Backend trả phẳng theo ô; frontend dựng ma trận.

- `dayStatus` do server suy ra theo CR-01 §14.3: `NoPlan` → `NotStarted` → `InProduction` → `Closed`. Frontend không tự tính.
- `actualQuantity = null` nghĩa là ô chưa ghi nhận lần nào, khác với `0`. Khi `isProvisional = true` (ô chưa Xuất hàng) đây là **số tạm tính** = tổng các lần ghi nhận hiện có.
- `shortageQuantity` và `difference` **chỉ có giá trị khi ô đã Xuất hàng**; ô còn mở trả `null` (CR-01 OV-5).

**Ghi nhận, sửa, xoá, Xuất hàng theo ô** — dùng đúng contract và luật của CR-01 §6.3–§6.6, chỉ khác là ô được định danh bằng `productionDate` + `productionLineId`:

| Endpoint | Việc | Ghi chú |
|---|---|---|
| `GET …/production-days/{date}/lines/{lineId}` | Toàn bộ state của ô: kế hoạch, số tạm tính, "còn được nhập", danh sách lần ghi nhận kèm tổng lũy kế | Màn hình chính của dialog nhập sản lượng |
| `POST …/production-days/{date}/lines/{lineId}/entries` | Ghi nhận thêm một lần: `{ "quantity": 15, "note": null }` | `quantity > 0`; tổng các lần ≤ kế hoạch ô và tổng đơn ≤ `order.quantity`; trả lại state ô |
| `PUT /production-entries/{entryId}` | Sửa một lần ghi nhận: `{ "quantity": 12, "note": null }` | Chỉ khi ô còn mở; ghi `production_entry_logs` |
| `DELETE /production-entries/{entryId}` | Xoá mềm một lần ghi nhận | Chỉ khi ô còn mở |
| `POST …/production-days/{date}/lines/{lineId}/close` | Xuất hàng — chốt sổ ô | Không nhận `actualQuantity` từ client; server tính từ các lần ghi nhận. Là thời điểm duy nhất đánh giá lại trạng thái đơn. Gọi lần hai → `409 DAY_ALREADY_CLOSED` |

Ô không có kế hoạch → `422 DAY_HAS_NO_PLAN` cho cả ghi nhận lẫn Xuất hàng (BR-N10). Transaction khoá dòng đơn hàng trước khi đọc tổng, như CR-01 §4.6.

## 6.8 Adjustment

Contract request/response **không đổi**. Bổ sung validation phía server:

- Mọi `productionPlanId` trong `targets` phải có `production_line_id` bằng của source plan → nếu sai: `422 ADJUSTMENT_TARGET_LINE_MISMATCH`.
- Ngày đích phải `>` ngày của source plan và không phải ngày đã qua.
- Automatic: chia đều trên các ngày còn lại **của chính dây chuyền đó**, theo đúng quy tắc chia dư đã chốt (phần dư dồn vào các ngày đầu).

Preview response bổ sung `productionLineId` và `productionLineName` cho từng item để UI hiển thị rõ đang bù trên dây chuyền nào.

## 6.9 Statistics

**GET /api/v1/orders/{orderId}/statistics** — bổ sung:

```json
{
  "byProductionLine": [
    {
      "productionLineId": "uuid",
      "productionLineName": "Dây chuyền 1",
      "totalPlan": 520,
      "totalActual": 480,
      "shortage": 40
    }
  ]
}
```

**GET /api/v1/statistics/dashboard** — bổ sung `pendingOrderCount` (số đơn chưa lập tiến độ). Các chỉ số tiến độ hiện có chỉ tính trên đơn `Incomplete` / `Completed`; đơn `Pending` không tham gia tính tiến độ vì chưa có kế hoạch.

## 6.10 Error code mới

| Code | HTTP | Ý nghĩa |
|---|---|---|
| `SHOE_CODE_ALREADY_EXISTS` | 409 | Mã giày đã tồn tại |
| `ORDER_SCHEDULE_ALREADY_EXISTS` | 409 | Đơn đã lập tiến độ |
| `ORDER_NOT_SCHEDULED` | 422 | Thao tác yêu cầu đơn đã có tiến độ |
| `LINE_ALLOCATION_MISMATCH` | 422 | Tầng 1: tổng phân bổ cho các dây chuyền khác số lượng đơn |
| `LINE_PLAN_TOTAL_MISMATCH` | 422 | Tầng 2: tổng kế hoạch theo ngày của một dây chuyền khác số đã phân cho dây chuyền đó |
| `PRODUCTION_LINE_NOT_FOUND` | 404 | Dây chuyền không tồn tại |
| `PRODUCTION_LINE_INACTIVE` | 422 | Dây chuyền đang ngừng hoạt động |
| `PRODUCTION_LINE_CODE_ALREADY_EXISTS` | 409 | Mã dây chuyền trùng |
| `PRODUCTION_LINE_EMPTY_ALLOCATION` | 422 | Dây chuyền được chọn nhưng phân bổ = 0 |
| `ADJUSTMENT_TARGET_LINE_MISMATCH` | 422 | Bù sang dây chuyền khác |
| `ORDER_NOT_DELETABLE` | 422 | Xoá đơn đã lập tiến độ (BR-N19) |
| `IMAGE_NOT_FOUND` | 404 | Đơn chưa có ảnh mẫu mà vẫn yêu cầu nội dung ảnh |
| `IMAGE_TYPE_NOT_SUPPORTED` | 400 | Định dạng ảnh không hợp lệ |
| `IMAGE_TOO_LARGE` | 400 | Ảnh vượt 5MB |

Ghi nhận/Xuất hàng vào ô không có kế hoạch dùng lại mã `DAY_HAS_NO_PLAN` của CR-01, **không** tạo mã riêng (bản duyệt đầu đặt tên `ACTUAL_ON_ZERO_PLAN`, nay bỏ).

Giữ nguyên: `ACTUAL_EXCEEDS_ORDER_QUANTITY`, `ADJUSTMENT_OUTDATED`, `ACTIVE_ADJUSTMENT_EXISTS`, `VALIDATION_ERROR`, và toàn bộ mã của CR-01 (`DAY_ALREADY_CLOSED`, `ENTRY_EXCEEDS_DAILY_PLAN`, `FUTURE_DATE_NOT_ALLOWED`, `ORDER_ALREADY_COMPLETED`, `SOURCE_DAY_NOT_CLOSED`, `TARGET_DAY_CLOSED`…).

---

# 7. Thay đổi Frontend

## 7.1 Navigation

```text
Trước                Sau
─────                ─────
Dashboard            Dashboard
Đơn hàng             Nhập hàng
                     Tiến độ
                     Cấu hình
                       └─ Dây chuyền sản xuất
```

## 7.2 Route

| Route | Trạng thái |
|---|---|
| `/dashboard` | Giữ, cập nhật nội dung |
| `/goods-receipt` | Mới — danh sách Nhập hàng |
| `/goods-receipt/new` | Mới — tạo đơn nhập hàng |
| `/goods-receipt/:orderId` | Mới — chi tiết/sửa đơn nhập hàng |
| `/progress` | Đổi từ `/orders` |
| `/progress/new` | Đổi từ `/orders/new`, đổi hoàn toàn nội dung |
| `/progress/:orderId` | Đổi từ `/orders/:orderId` |
| `/settings/production-lines` | Mới |

`/orders*` redirect sang `/progress*` để không vỡ bookmark cũ.

## 7.3 Cấu trúc feature

```text
src/features/
├── orders/            → đổi thành nghiệp vụ Nhập hàng
│   ├── api/
│   ├── components/    OrderImageUploader, OrderForm, OrderList
│   └── pages/
├── production-lines/  → MỚI
│   ├── api/
│   ├── components/    ProductionLineTable, ProductionLineDialog
│   └── pages/
├── production/        → mở rộng: ma trận ngày × dây chuyền
│   └── components/    ProductionMatrix, ProductionCell, ScheduleForm
├── adjustments/       → bổ sung hiển thị dây chuyền
└── statistics/        → bổ sung breakdown theo dây chuyền
```

Component dùng chung thêm vào `shared/`: `ImageUploader` (drag-drop, preview, xoá, giới hạn type/size) và `ImageLightbox`.

## 7.4 Màn hình Nhập hàng — danh sách

```text
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│ Nhập hàng                                                                  [+ Nhập hàng]  │
│ Quản lý hàng nhận về theo mã giày                                                         │
│                                                                                           │
│ [ Tất cả ] [ Chưa lập tiến độ ] [ Đang sản xuất ] [ Hoàn thành ]   🔍 Tìm mã giày… [Tìm]  │
├───────────────────────────────────────────────────────────────────────────────────────────┤
│ Ảnh   Mã giày      Số lượng  Dây chuyền    Thời gian      Trạng thái   Thao tác           │
│ [img] SH-2026-001    1,000   DC-01, DC-02  10/09/2026     Đang SX      [Xem tiến độ][Sửa] │
│                                            → 20/09/2026                                   │
│ [img] SH-2026-002      800   —             Chưa lên lịch  Chưa lập TĐ  [Lập tiến độ][Sửa] │
│                                                                        [Xoá]              │
└───────────────────────────────────────────────────────────────────────────────────────────┘
```

- Cột `Thời gian` = ngày bắt đầu → ngày kết thúc; đơn chưa lập tiến độ hiện `Chưa lên lịch`.
- Cột `Trạng thái` kèm badge `Quá hạn` khi đơn đã qua ngày kết thúc mà chưa hoàn thành.
- Dòng **không bấm được**; mọi điều hướng đi qua cột `Thao tác`:
  - Đơn `Chưa lập tiến độ`: `Lập tiến độ` (sang `/progress/new?orderId=...`), `Sửa`, `Xoá`.
  - Đơn đã lập tiến độ: `Xem tiến độ` (sang `/progress/:orderId`), `Sửa`. Không có `Xoá` (BR-N19).
- `Xoá` hỏi xác nhận trước (`Xoá đơn nhập hàng?`).

## 7.5 Màn hình tạo đơn nhập hàng

```text
┌─────────────────────────────────────────────┐
│ NHẬP HÀNG                                   │
│                                             │
│ Mã giày *                                   │
│ [ SH-2026-001                              ]│
│                                             │
│ Số lượng *                                  │
│ [ 1,000 ] đôi                               │
│                                             │
│ Hình ảnh                                    │
│ ┌───────────────────────────────┐           │
│ │  Kéo thả ảnh hoặc bấm chọn    │           │
│ │  jpg / png / webp, tối đa 5MB │           │
│ └───────────────────────────────┘           │
│                                             │
│              [ Huỷ ]  [ Nhập hàng ]         │
└─────────────────────────────────────────────┘
```

Validation phía client (Zod): mã giày bắt buộc, số lượng nguyên > 0, ảnh đúng type và ≤ 5MB. Backend vẫn là nơi phán quyết cuối cùng.

## 7.5b Màn hình sửa đơn nhập hàng — `/goods-receipt/:orderId`

- Cùng bố cục với màn tạo: ảnh mẫu, mã giày, số lượng.
- Mọi thay đổi, kể cả thay/gỡ ảnh, chỉ nằm ở client cho tới khi bấm `Lưu thay đổi`, rồi được lưu trong **một request** `PUT /orders/{orderId}` (§6.4). Khi có thay đổi chưa lưu, form hiện `Có thay đổi chưa lưu` và bật nút `Huỷ thay đổi` để quay về dữ liệu đang lưu.
- Đơn đã lập tiến độ: ô số lượng bị khoá (BR-N20). Header có nút `Xem tiến độ`; đơn chưa lập tiến độ có nút `+ Lập tiến độ`.
- Đơn đã qua ngày kết thúc: cả form bị khoá, kèm thông báo `Đơn hàng đã qua ngày kết thúc nên chỉ được xem lại.`

## 7.6 Màn hình tạo Tiến độ

Flow 5 bước, phản ánh đúng hai tầng phân bổ:

```text
Chọn mã giày
      ↓
Chọn dây chuyền + khoảng ngày
      ↓
Phân bổ đơn → dây chuyền      (tầng 1)
      ↓
Phân bổ dây chuyền → ngày     (tầng 2)
      ↓
Xem lại → Tạo
```

**Bước 1** — danh sách chọn (kèm ô tìm mã giày) chỉ liệt kê đơn `Pending`, hiển thị ảnh, mã giày, số lượng. Vào từ nút `Lập tiến độ` thì đơn được chọn sẵn và **bỏ qua luôn bước 1**.

**Bước 2** — multi-select dây chuyền `Active` + ngày bắt đầu + ngày kết thúc. Ngày kết thúc phải bằng hoặc sau ngày bắt đầu; form hiện số ngày sản xuất.

**Bước 3 — phân bổ cho dây chuyền**

```text
PHÂN BỔ SẢN LƯỢNG — SH-2026-001 — 1,000 đôi

Cách phân bổ:  (•) Tự động chia đều   ( ) Nhập tay

┌──────────┬────────────────┐
│ DC-01    │ [       334 ]  │
│ DC-02    │ [       333 ]  │
│ DC-03    │ [       333 ]  │
├──────────┼────────────────┤
│ Tổng     │        1,000   │
└──────────┴────────────────┘

Đã phân bổ: 1,000 / 1,000 ✓

[ Quay lại ]  [ Tiếp tục ]
```

- `Tự động chia đều`: ô nhập bị khoá, hệ thống tự điền theo BR-N17.
- `Nhập tay`: mở khoá toàn bộ ô, tổng phải bằng đúng 1.000 mới cho `Tiếp tục`.
- Đổi mode qua lại thì `Tự động` ghi đè con số user đã nhập — cần confirm trước khi ghi đè.
- Thông báo lệch: `Còn thiếu 50 đôi chưa được phân bổ` / `Vượt 30 đôi so với số lượng đơn`.

**Bước 4 — phân bổ theo ngày**

Ma trận được **khởi tạo sẵn** bằng cách chia đều số của từng dây chuyền cho các ngày. User sửa lại tuỳ ý, ràng buộc duy nhất là tổng mỗi cột phải khớp số đã phân ở bước 3.

```text
KẾ HOẠCH THEO NGÀY
10/09/2026 → 12/09/2026

┌──────────┬──────────┬──────────┬──────────┬──────────┐
│ Ngày     │  DC-01   │  DC-02   │  DC-03   │  Tổng    │
├──────────┼──────────┼──────────┼──────────┼──────────┤
│ 10/09    │ [  112 ] │ [  111 ] │ [  111 ] │    334   │
│ 11/09    │ [  111 ] │ [  111 ] │ [  111 ] │    333   │
│ 12/09    │ [  111 ] │ [  111 ] │ [  111 ] │    333   │
├──────────┼──────────┼──────────┼──────────┼──────────┤
│ Tổng     │    334   │    333   │    333   │  1,000   │
│ Phân bổ  │    334 ✓ │    333 ✓ │    333 ✓ │  1,000 ✓ │
└──────────┴──────────┴──────────┴──────────┴──────────┘

[ Chia đều lại ]              [ Quay lại ]  [ Xem lại ]
```

- Dòng `Phân bổ` là mốc từ bước 3, read-only. Cột nào lệch thì tô đỏ kèm số lệch, ví dụ `330 / 334 (thiếu 4)`.
- Nút `Chia đều lại` reset toàn bộ ma trận về giá trị chia đều, có confirm.
- Chỉ cho `Xem lại` khi **mọi cột** đều khớp.
- Ô có thể để `0` — nghĩa là dây chuyền đó nghỉ ngày đó. Ô `0` sẽ **không tạo dòng plan** và không nhập sản lượng được (BR-N10).

**Bước 5** — review read-only toàn bộ hai tầng rồi xác nhận tạo.

**Giữ nguyên trạng thái khi reload trang.** Reload giữa chừng phải quay lại **đúng bước đang làm cùng toàn bộ dữ liệu đã nhập** (đơn đã chọn, dây chuyền, ngày, cách phân bổ, số phân bổ, ma trận ngày) — không được quay về bước 1 hay bước 2.

- Bản nháp lưu trong `sessionStorage`: chỉ sống trong tab hiện tại, đóng tab là mất.
- Rời màn này bằng điều hướng trong app (về danh sách, bấm menu, nút Back, tạo tiến độ thành công) là kết thúc lần lập tiến độ — bản nháp bị xoá. Bản nháp chỉ phục vụ reload, không khôi phục một lần lập dở từ trước.
- Bản nháp gắn với `orderId` trên URL lúc vào trang: vào lại bằng nút `Lập tiến độ` của một đơn khác thì không dùng bản nháp cũ.
- Đơn trong bản nháp không còn `Pending` khi reload xong (vd. vừa được lập tiến độ ở tab khác) → quay về bước 1, bỏ chọn đơn, báo `Đơn hàng đang lập dở không còn chờ lập tiến độ. Vui lòng chọn lại đơn.`
- Không lưu các trạng thái tạm của giao diện: lỗi validate, dialog xác nhận, ô tìm kiếm.

## 7.7 Màn hình Tiến độ — danh sách

Đổi từ Danh sách đơn hàng. Cột: **Ảnh, Mã giày, Dây chuyền, Tổng SL, Đã làm, Ngày bắt đầu, Ngày kết thúc, Tiến độ, Trạng thái, Tình trạng**. Chỉ hiển thị đơn `Incomplete` / `Completed`; đơn `Pending` thuộc màn Nhập hàng.

- Không có cột `Còn lại` (đã bỏ, thay bằng `Ngày bắt đầu`).
- Cột `Ngày kết thúc` kèm badge `Quá hạn` khi đơn trễ, và badge `Có ô chưa xuất hàng` khi còn ô đã qua mà chưa Xuất hàng.
- `Trạng thái` là trạng thái đơn (`Chưa hoàn thành` / `Hoàn thành`); `Tình trạng` là đúng/chậm tiến độ — hai cột tách riêng.
- Click dòng → `/progress/:orderId`.

Filter: `Tất cả | Chưa hoàn thành | Hoàn thành` + filter theo dây chuyền (dropdown, gồm cả dây chuyền đã ngừng hoạt động để lọc được đơn cũ). Search theo mã giày.

Giữ nguyên nguyên tắc đã chốt: **"Chậm" không phải trạng thái đơn hàng**, tách khỏi `Chưa hoàn thành` / `Hoàn thành`.

## 7.8 Màn hình Chi tiết tiến độ

Giữ nguyên cấu trúc của `order-detail-screen-spec.md`, bổ sung:

- Header thêm ảnh mẫu (click mở lightbox), khoảng ngày bắt đầu → ngày kết thúc, danh sách dây chuyền kèm số phân bổ, và badge `Chậm tiến độ: N đôi` khi chậm. Có nút sang màn sửa nhập hàng.
- Ô thống kê `Ngày kết thúc` kèm gợi ý `Còn N ngày` hoặc `Đã quá hạn`.
- Production Timeline chuyển thành **ma trận ngày × dây chuyền**:

```text
┌──────────┬─────────────────────┬─────────────────────┬────────┐
│ Ngày     │ DC-01    170 / 220  │ DC-02    80 / 260   │ Tổng   │
│          │  KH   TT    Lệch    │  KH   TT    Lệch    │ TT     │
├──────────┼─────────────────────┼─────────────────────┼────────┤
│ 10/09    │ 100    90    -10 🔴 │  80    80      0    │  170   │
│ 11/09    │ 120    80      —    │ 100     —      —    │   80   │
│          │      Tạm tính       │                     │        │
│ 12/09    │         —           │  80     —      —    │    —   │
└──────────┴─────────────────────┴─────────────────────┴────────┘
```

- Tiêu đề mỗi dây chuyền ghi `thực tế / kế hoạch hiện tại` của cả dây chuyền.
- `KH` hiện kèm phần được bù (`+N`) nếu có. `TT` của ô chưa Xuất hàng là số **tạm tính**. `Lệch` **chỉ có số khi ô đã Xuất hàng** (CR-01 OV-5).
- Ô không có kế hoạch hiển thị `—` gộp ba cột, không có thao tác.
- Header bảng dính lại khi cuộn dọc; cột ngày được ghim khi cuộn ngang (§10).

**Thao tác tách khỏi ma trận.** Ma trận chỉ để đọc số; nhét nút vào ô sẽ làm mỗi ô cao gấp ba và bảng không còn đọc được theo cột. Ngay dưới ma trận là danh sách **chỉ các ô còn việc**, sắp theo ngày rồi thứ tự dây chuyền:

| Tình trạng ô | Thao tác |
|---|---|
| Đang sản xuất (đã tới ngày, chưa Xuất hàng) | `Nhập sản lượng`, `Xuất hàng` |
| Đã Xuất hàng, còn thiếu | `Xem chi tiết`, `Xử lý thiếu` (ẩn khi đã có lần bù đang áp dụng — hiện badge `Đã bù`) |
| Chưa tới ngày, không có kế hoạch, đã Xuất hàng đủ | Không nằm trong danh sách |

Không còn ô nào cần thao tác → `✓ Không còn ô nào cần thao tác…`.

- Đơn `Completed`: ẩn `Nhập sản lượng` và `Xử lý thiếu`, **vẫn cho `Xuất hàng`** các ô còn treo (CR-01 §14.6).
- Đơn đã qua ngày kết thúc: ẩn mọi thao tác, hiện thông báo `Đơn hàng đã qua ngày kết thúc (…) nên chỉ được xem lại`.
- Có ô đã qua mà chưa Xuất hàng → cảnh báo `N ô đã qua chưa xuất hàng (10/09 · DC-01, …)`, vì số liệu của chúng vẫn chỉ là tạm tính.
- Dialog `Nhập sản lượng` theo MH5 của CR-01, áp cho ô: Ngày, **Dây chuyền**, Kế hoạch, số tạm tính, "còn được nhập", form ghi nhận thêm một lần, lịch sử các lần ghi nhận (sửa/xoá được khi ô còn mở). Ô đã Xuất hàng mở cùng dialog ở chế độ `Chi tiết ô sản xuất`, chỉ đọc.
- `Xuất hàng` hỏi xác nhận trước và nói rõ ô sẽ bị chốt sổ vĩnh viễn; ô chưa ghi nhận lần nào thì Xuất hàng nghĩa là sản lượng ô bằng 0.
- Khối xử lý thiếu và preview adjustment hiển thị rõ dây chuyền, và chỉ liệt kê ngày đích thuộc dây chuyền đó.
- Statistics thêm bảng breakdown theo dây chuyền.

## 7.9 Màn hình Cấu hình dây chuyền

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Dây chuyền sản xuất                                          [+ Thêm]        │
│                                                                              │
│ Mã     Tên           Thứ tự  Ghi chú  Trạng thái  Đang dùng  Thao tác        │
│ DC-01  Dây chuyền 1       1  —        ● Active    Có         Sửa  Ngừng      │
│ DC-02  Dây chuyền 2       2  —        ● Active    Có         Sửa  Ngừng      │
│ DC-03  Dây chuyền 3       3  —        ○ Inactive  Không      Sửa  Bật lại    │
└──────────────────────────────────────────────────────────────────────────────┘
```

Dialog thêm/sửa: Mã, Tên, Thứ tự, Ghi chú. Toggle Active/Inactive ngay trên bảng, có xác nhận. Không có nút xoá ở Phase này.

## 7.10 Query invalidation

Bổ sung so với baseline:

| Mutation | Invalidate |
|---|---|
| Tạo/sửa/xoá ảnh đơn hàng | `["orders"]`, `["orders", orderId]` |
| Nhập hàng / sửa đơn nhập hàng | ghi đè `["orders", orderId]` bằng response; invalidate `["orders","list"]`, `["statistics","dashboard"]` |
| Xoá đơn nhập hàng | bỏ hẳn cache `["orders", orderId]`; invalidate `["orders","list"]`, `["statistics","dashboard"]` |
| Tạo tiến độ | `["orders"]`, `["orders", orderId]`, `["orders", orderId, "production-plans"]`, `["statistics","dashboard"]` |
| CRUD dây chuyền | `["production-lines"]` |
| Ghi nhận / sửa / xoá lần ghi nhận, Xuất hàng ô | như CR-01 §7.3, thêm key của ô `(orderId, ngày, dây chuyền)`; kèm `["orders", orderId]`, `production-plans`, statistics của đơn, `["orders","list"]`, `["statistics","dashboard"]` |

## 7.11 Trạng thái UI bắt buộc

Mọi màn hình mới phải có đủ Loading / Empty / Error / Success:

- Nhập hàng rỗng → `Chưa có đơn hàng nào` + CTA `Nhập hàng`.
- Tiến độ rỗng → `Chưa có tiến độ nào` + CTA `Lập tiến độ`.
- Dây chuyền rỗng → `Chưa cấu hình dây chuyền` + CTA `Thêm dây chuyền`.
- Vào `/progress/new` khi chưa có dây chuyền `Active` → chặn ngay từ đầu, hiện `Chưa có dây chuyền nào đang hoạt động` + nút `Đi tới cấu hình dây chuyền`.
- Vào `/progress/new` khi không còn đơn `Pending` nào → `Không có đơn hàng nào chờ lập tiến độ` + CTA `+ Nhập hàng`.

---

# 8. Thứ tự triển khai

Theo nguyên tắc vertical slice đã chốt: mỗi slice phải test được end-to-end trên UI thật trước khi sang slice sau.

### Slice 1 — Dây chuyền sản xuất
DB `production_lines` → domain/application → 4 endpoint → API client + hooks → màn hình cấu hình → validation → loading/empty/error.
Không phụ thuộc gì, làm trước.

### Slice 2 — Nhập hàng + ảnh
Sửa `orders` (shoe_code, cột ảnh, status `Pending`, ngày nullable) → hạ tầng lưu file → `POST /orders` multipart + 3 endpoint ảnh → màn hình danh sách + tạo + uploader.

### Slice 3 — Lập tiến độ
`order_production_lines` (kèm `allocated_quantity`) + `production_line_id` trên `production_plans` → thuật toán chia đều dùng chung → `POST /production-schedule` + transaction validate 2 tầng → màn hình tạo tiến độ 5 bước với khối phân bổ dây chuyền và ma trận ngày.

### Slice 4 — Ma trận sản lượng
`production_line_id` trên `production_days` → endpoint ô sản xuất (ghi nhận, sửa/xoá lần ghi nhận, Xuất hàng) → Production Timeline dạng ma trận + danh sách ô còn việc → dialog ghi nhận theo ô.

### Slice 5 — Xử lý thiếu theo dây chuyền
Validation cùng dây chuyền cho preview/apply → automatic chia đều trong dây chuyền → UI shortage/preview/history hiển thị dây chuyền.

### Slice 6 — Đổi tên, navigation, dashboard, statistics
Route + redirect → sidebar → rà toàn bộ label → dashboard `pendingOrderCount` → statistics breakdown.

---

# 9. Test checklist

## UI TEST — Slice 1: Dây chuyền
1. Đăng nhập, mở Cấu hình → Dây chuyền sản xuất.
2. Trạng thái rỗng hiển thị đúng.
3. Thêm `DC-01 / Dây chuyền 1` → xuất hiện trong bảng.
4. Thêm trùng mã `DC-01` → báo lỗi mã trùng, không tạo.
5. Sửa tên → cập nhật đúng.
6. Chuyển `DC-01` sang Inactive → badge đổi trạng thái.

## UI TEST — Slice 2: Nhập hàng
1. Mở Nhập hàng → trạng thái rỗng đúng.
2. Tạo `SH-2026-001`, SL 1000, kèm ảnh jpg 1MB → thành công, thumbnail hiển thị.
3. Tạo trùng mã giày → báo lỗi.
4. Nhập SL `0`, `-10`, `100.5` → đều bị chặn.
5. Upload file `.pdf` → bị chặn tại client và cả server.
6. Upload ảnh 8MB → bị chặn.
7. Thay ảnh → ảnh mới hiển thị, ảnh cũ không còn truy cập được.
8. Xoá ảnh → về placeholder.
9. Đơn hiển thị trạng thái `Chưa lập tiến độ`.
10. Sửa mã giày, số lượng và thay ảnh của đơn `Pending` rồi `Lưu thay đổi` → cả ba được lưu cùng lúc.
11. Đơn đã lập tiến độ → ô số lượng bị khoá; gọi API đổi số lượng → `422 ORDER_SCHEDULE_ALREADY_EXISTS`.
12. Xoá đơn `Pending` → hỏi xác nhận, đơn biến khỏi danh sách, ảnh không còn truy cập được.
13. Đơn đã lập tiến độ không có nút `Xoá`; gọi API xoá → `422 ORDER_NOT_DELETABLE`.

## UI TEST — Slice 3: Lập tiến độ
1. Từ đơn `SH-2026-001` (1,000 đôi) bấm `Lập tiến độ`.
2. Chọn DC-01 + DC-02 + DC-03, ngày 10/09 → 12/09.
3. Mode `Tự động chia đều` → hiện `334 / 333 / 333`, ô nhập bị khoá.
4. Chuyển sang `Nhập tay` → mở khoá; nhập `300 / 300 / 300` → chặn, báo thiếu 100.
5. Nhập `400 / 400 / 400` → chặn, báo vượt 200.
6. Nhập `500 / 300 / 200` → cho `Tiếp tục`.
7. Ma trận ngày khởi tạo sẵn: DC-01 `167/167/166`, DC-02 `100/100/100`, DC-03 `67/67/66`.
8. Sửa DC-01 thành `200/200/100` (tổng vẫn 500) → cột vẫn xanh.
9. Sửa DC-01 thành `200/200/50` (tổng 450) → cột đỏ, báo thiếu 50, chặn `Xem lại`.
10. Bấm `Chia đều lại` → ma trận về giá trị chia đều, có confirm trước khi ghi đè.
11. Đưa mọi cột về khớp → Xem lại → Tạo thành công.
12. Đơn chuyển `Đang sản xuất`, biến mất khỏi filter `Chưa lập tiến độ`.
13. Xuất hiện trong màn Tiến độ với đúng 3 dây chuyền.
14. Gọi lại `POST /production-schedule` cho chính đơn đó → `409`.
15. Gọi API với `SUM(allocatedQuantity) = 900` → `422 LINE_ALLOCATION_MISMATCH`.
16. Gọi API với tổng plan của DC-02 lệch so với `allocatedQuantity` → `422 LINE_PLAN_TOTAL_MISMATCH`, `details` chỉ đúng DC-02.
17. Đặt `allocatedQuantity = 0` cho một dây chuyền → bị chặn.
18. Đang ở bước 3, 4 hoặc 5, reload trang → quay lại đúng bước đó với toàn bộ dữ liệu đã nhập; quay về bước 1 rồi reload → vẫn ở bước 1.
19. Rời màn lập tiến độ (về danh sách, bấm menu) rồi vào lại → bắt đầu mới, không dùng bản nháp cũ.

## UI TEST — Slice 4: Sản lượng
1. Mở Chi tiết tiến độ → ma trận hiển thị đúng KH từng ô; ô chưa tới ngày không nằm trong danh sách thao tác.
2. Ghi nhận `50` rồi `40` cho `10/09 × DC-01` (KH 100) → TT tạm tính `90`, lịch sử có 2 lần; cột `Lệch` vẫn trống.
3. Ghi nhận thêm `20` → bị chặn `ENTRY_EXCEEDS_DAILY_PLAN`, báo còn được nhập `10`.
4. Sửa lần ghi nhận `40` thành `30`, xoá một lần ghi nhận → tổng tạm tính cập nhật đúng.
5. Ô không có kế hoạch → không có thao tác; gọi API ghi nhận → `422 DAY_HAS_NO_PLAN`.
6. `Xuất hàng` ô `10/09 × DC-01` → ô bị chốt sổ, `Lệch` hiện `-10`, xuất hiện `Xử lý thiếu`; Xuất hàng lần hai → `409 DAY_ALREADY_CLOSED`.
7. `Xuất hàng` một ô chưa ghi nhận lần nào → sản lượng ô bằng `0`.
8. Ghi nhận vượt tổng đơn 1,000 → `ACTUAL_EXCEEDS_ORDER_QUANTITY`, hiển thị số còn lại.
9. Tổng tạm tính đủ 1,000 nhưng chưa Xuất hàng → đơn vẫn `Chưa hoàn thành`; Xuất hàng ô cuối → `Hoàn thành`.
10. Đơn `Hoàn thành` còn ô treo → không có `Nhập sản lượng`, vẫn `Xuất hàng` được.

## UI TEST — Slice 5: Xử lý thiếu
1. Ô `10/09 × DC-01` thiếu 10: trước khi Xuất hàng không có `Xử lý thiếu`; sau khi Xuất hàng thì có.
2. Manual: danh sách ngày đích **chỉ gồm ngày của DC-01**, không có DC-02.
3. Bù +10 vào `12/09 × DC-01` → preview → apply → KH ô đó tăng 10.
4. Tổng kế hoạch giờ lớn hơn 1,000 — đúng thiết kế, không cảnh báo sai.
5. Automatic với thiếu 23 trên 4 ngày còn lại → `+6/+6/+6/+5`, tất cả cùng DC-01.
6. Gọi API bù sang ô của DC-02 → `422 ADJUSTMENT_TARGET_LINE_MISMATCH`.
7. Chọn ô đích đã Xuất hàng → `TARGET_DAY_CLOSED`.
8. Hoàn tác (Reverse) lần bù → KH ô đích trở lại như trước, lịch sử vẫn còn, `Xử lý thiếu` hiện lại cho ô nguồn.

Kịch bản cũ "sửa sản lượng làm thiếu về 0 → adjustment tự Reverse" **không còn xảy ra được**: bù chỉ tạo được khi ô nguồn đã Xuất hàng, mà ô đã Xuất hàng thì bất biến (CR-01 §3.1).

## Automated test bắt buộc
- Tầng 1: `SUM(allocated_quantity) = order.quantity` khi lập tiến độ.
- Tầng 2: với mỗi dây chuyền, `SUM(initial_planned_quantity) = allocated_quantity`.
- Thuật toán chia đều: `1000/3 → 334/333/333`, `334/5 → 67/67/67/67/66`, `10/4 → 3/3/2/2`, `5/5 → 1/1/1/1/1`.
- `SUM(initial_planned_quantity)` toàn bộ ô `= order.quantity` (hệ quả của 2 tầng trên).
- `SUM(actual) <= order.quantity` dưới điều kiện đồng thời (2 request song song).
- Không nhập được sản lượng cho ô không có plan.
- Adjustment không được vượt sang dây chuyền khác.
- Chuyển trạng thái `Pending → Incomplete → Completed → Incomplete` đúng.
- Không lập tiến độ lần hai cho cùng một đơn.
- Unique `(order_id, production_date, production_line_id)` ở cả `production_plans` và `production_days`.
- Không xoá được đơn đã lập tiến độ; không đổi được số lượng đơn đã lập tiến độ.

---

# 10. Rủi ro

| Rủi ro | Mức | Xử lý |
|---|---|---|
| Ma trận ngày × dây chuyền phức tạp khi nhiều ngày/nhiều dây chuyền | Cao | Cố định cột ngày, cuộn ngang; cân nhắc chuyển sang layout theo nhóm dây chuyền nếu quá 5 dây chuyền |
| File ảnh mồ côi trên disk khi transaction rollback | Trung bình | Ghi DB trước, ghi file sau khi commit; job dọn file không có tham chiếu |
| Toàn bộ API sản xuất đổi contract cùng lúc | Trung bình | Triển khai theo slice, mỗi slice test UI đầy đủ trước khi sang bước sau |
| Dây chuyền chuyển Inactive khi đang có đơn chạy dở | Thấp | Chấp nhận; chỉ ảnh hưởng lựa chọn mới, dữ liệu cũ hiển thị bình thường |
| Nhầm lẫn thuật ngữ "đơn hàng" vs "tiến độ" trong code và tài liệu | Trung bình | Quy ước: backend luôn dùng `Order`; UI dùng "Nhập hàng" cho bước tạo, "Tiến độ" cho bước theo dõi |

## Rollback

Môi trường chưa có dữ liệu thật nên rollback là **revert migration + revert code branch**. Không cần kịch bản khôi phục dữ liệu. Thư mục ảnh có thể xoá sạch.

---

# 11. Tài liệu baseline cần cập nhật sau khi triển khai

| File | Nội dung cần sửa |
|---|---|
| `production-management-step-1-domain-model.md` | `Order` có `ShoeCode`, ảnh, 3 trạng thái; `ProductionLine` là entity mới; Plan/ProductionDay theo Order + Date + Line |
| `production-management-step-2-data-model.md` | Thêm `production_lines`, `order_production_lines`; unique key mới |
| `Production_Management_Step_3_Database_Schema.md` | DDL của `production_lines`, `order_production_lines`; sửa `orders`, `production_plans`, `production_days` |
| `Production_Management_Step_4_API_Contract.md` | Endpoint mới + endpoint sửa + error code mới |
| `Production_Management_Step_5_Frontend_Architecture.md` | Route mới, feature mới, navigation mới |
| `production-order-list-screen.md` | Đổi thành spec màn Tiến độ |
| `create-order-production-plan-screen.md` | Tách thành spec Nhập hàng + spec Lập tiến độ |
| `order-detail-screen-spec.md` | Timeline → ma trận ngày × dây chuyền |
| `production-quantity-entry-screen-spec.md` | Nhập sản lượng theo ô |
| `production-shortage-option-1-screen-spec.md`, `man_hinh_6_option_2_spec_vi.md` | Ràng buộc cùng dây chuyền |
| `production-management-master-summary.md` | Cập nhật business rule đã đổi |

Tình trạng tại 2026-09-13: **chưa viết lại** các tài liệu baseline trên theo CR-001; mới chỉ đổi thuật ngữ "Deadline" / "Hạn hoàn thành" thành "Ngày kết thúc" (C-08). Cho tới khi viết lại, tài liệu này cùng CR-01 là nguồn đúng.

---

# 12. Định nghĩa hoàn thành CR

- Cấu hình được danh sách dây chuyền và bật/tắt trạng thái.
- Nhập hàng được với mã giày, 1 ảnh, số lượng; sửa và xoá đơn đúng theo BR-N19, BR-N20.
- Lập được tiến độ với nhiều dây chuyền, phân bổ 2 tầng (đơn → dây chuyền → ngày), cả mode chia đều lẫn nhập tay, tổng khớp ở cả hai tầng; reload giữa chừng không mất bước đang làm.
- Ghi nhận nhiều lần, sửa/xoá lần ghi nhận và Xuất hàng được theo từng ô.
- Xử lý được thiếu trong phạm vi cùng dây chuyền, cả manual và automatic, chỉ sau khi ô đã Xuất hàng.
- Đơn tự chuyển `Hoàn thành` khi Xuất hàng mà tổng thực tế đủ số lượng; sau đó không ghi nhận hay bù thêm được, nhưng vẫn Xuất hàng được ô còn treo.
- Không thao tác nào làm tổng thực tế vượt tổng số lượng đơn.
- Navigation, nhãn và dashboard đã phản ánh đúng Nhập hàng / Tiến độ.
- Toàn bộ UI test checklist ở mục 9 chạy qua trên UI thật với backend và database thật.

---

# 13. Thay đổi yêu cầu sau khi duyệt

Các nội dung dưới đây đã được sửa thẳng vào các mục tương ứng ở trên. Bảng này giữ dấu vết để biết mục nào khác bản duyệt đầu (2026-09-07).

| # | Thời điểm | Thay đổi | Mục đã sửa |
|---|---|---|---|
| C-01 | Trong lúc triển khai | Áp CR-001 lên nền CR-01: ô sản xuất dùng `production_days` với nhiều lần ghi nhận và Xuất hàng theo từng ô; bỏ mô hình `production_records` "một giá trị mỗi ô" của bản duyệt đầu. Ô không có kế hoạch dùng mã `DAY_HAS_NO_PLAN` thay cho `ACTUAL_ON_ZERO_PLAN`. Trạng thái đơn chỉ đánh giá lúc Xuất hàng; đơn `Completed` vẫn Xuất hàng được ô còn treo. | §1, §4.1–§4.4, §5.5, §5.7, §5.8, §6.2, §6.7, §6.10, §7.8, §7.10, §9, §12 |
| C-02 | Trong lúc triển khai | Thêm sửa đơn nhập hàng (`PUT /orders/{orderId}`, lưu mọi thay đổi kể cả ảnh trong một request) và xoá đơn `Pending` (`DELETE /orders/{orderId}`). Số lượng khoá sau khi lập tiến độ. | §4.2 (BR-N19, BR-N20), §6.1, §6.4, §6.10, §7.4, §7.5b, §7.10, §9 |
| C-03 | Trong lúc triển khai | Danh sách Nhập hàng: thêm cột `Thời gian`, badge `Quá hạn`; dòng không bấm được, mọi điều hướng qua cột `Thao tác`. | §7.4 |
| C-04 | Trong lúc triển khai | Chi tiết tiến độ: thao tác tách khỏi ma trận thành danh sách các ô còn việc; ma trận chỉ để đọc số. | §7.8 |
| C-05 | Trong lúc triển khai | Danh sách Tiến độ có cột `Trạng thái` (trạng thái đơn) tách khỏi `Tình trạng`; cấu hình dây chuyền hiển thị thêm `Thứ tự`, `Ghi chú`. | §7.7, §7.9 |
| C-06 | 2026-09-13 | Lập tiến độ phải giữ nguyên bước đang làm và dữ liệu đã nhập khi reload trang. | §7.6, §9, §12 |
| C-07 | 2026-09-13 | Danh sách Tiến độ: bỏ cột `Còn lại`, thay bằng cột `Ngày bắt đầu`. | §7.7 |
| C-08 | 2026-09-13 | Đổi khái niệm "Deadline" / "Hạn hoàn thành" thành **"Ngày kết thúc"** trên toàn bộ giao diện và tài liệu. Tên kỹ thuật (`due_date`, `dueDate`) giữ nguyên. | §1, §4.1, §7.6, §7.7, §7.8, §11 |
