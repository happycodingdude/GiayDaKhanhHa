# Production Management Web App

Web app quản lý tiến độ sản xuất giày, triển khai theo bộ tài liệu thiết kế đã chốt trong `docs/`.

```text
React + TypeScript + Vite   →  frontend/
.NET modular monolith       →  backend/
PostgreSQL                  →  docker-compose.yml
```

---

## 1. Yêu cầu môi trường

| Thành phần | Phiên bản |
|---|---|
| .NET SDK | 10.0.1xx |
| Node.js | 20+ |
| Docker | để chạy PostgreSQL |

---

## 2. Chạy lần đầu

### 2.1. Tạo file `.env`

Repo **không chứa bất kỳ file cấu hình nào** — không có `appsettings.json`, không có mật khẩu,
không có thông tin server. Toàn bộ config đến từ biến môi trường.

Tạo file `.env` ở thư mục gốc, thay giá trị trong `<...>` bằng của bạn:

```bash
# PostgreSQL (docker-compose.yml đọc trực tiếp file .env)
POSTGRES_DB=production_management
POSTGRES_USER=postgres
POSTGRES_PORT=5432
POSTGRES_PASSWORD=<mật-khẩu-postgres>

# Bắt buộc: không còn appsettings.json nên chuỗi kết nối phải đến từ đây.
ConnectionStrings__Default=Host=<host>;Port=5432;Database=production_management;Username=postgres;Password=<mật-khẩu-postgres>

# Mật khẩu tài khoản quản lý đầu tiên (username mặc định trong code: manager).
# Bỏ trống -> hệ thống sinh ngẫu nhiên và in ra log một lần duy nhất.
Bootstrap__Password=<mật-khẩu-quản-lý>

# Integration test: mật khẩu server Postgres, Npgsql đọc từ PGPASSWORD.
PM_TEST_POSTGRES=Host=<host>;Port=5432;Username=postgres
PGPASSWORD=<mật-khẩu-postgres>

# mock-data/seed.mjs, phải trùng Bootstrap__Password.
PM_PASSWORD=<mật-khẩu-quản-lý>
```

`.env` và `appsettings*.json` đều nằm trong `.gitignore` — không bao giờ commit.

Các khoá tuỳ chọn, code đã có mặc định hợp lý nên chỉ đặt khi cần khác đi:
`Bootstrap__Username` (mặc định `manager`), `Business__TimeZone` (mặc định `Asia/Ho_Chi_Minh`,
định nghĩa trong `SystemClock.DefaultBusinessTimeZoneId`), `Database__AutoMigrate` (mặc định `true`).

### 2.2. Khởi động PostgreSQL

```bash
docker compose up -d
```

`docker-compose.yml` đọc `.env` tự động và sẽ báo lỗi nếu `POSTGRES_PASSWORD` còn trống.

### 2.3. Khởi động backend

Backend tự động chạy migration và tạo tài khoản quản lý đầu tiên khi bảng `users` còn trống.

Không có `appsettings.json`, nên **bắt buộc** nạp biến môi trường từ `.env` trước khi chạy —
thiếu `ConnectionStrings__Default` thì app dừng ngay với lỗi rõ ràng:

```bash
# Linux/macOS
set -a; source .env; set +a

# Windows PowerShell
Get-Content .env | Where-Object { $_ -match '^[^#].*=' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    [Environment]::SetEnvironmentVariable($k, $v, 'Process')
}
```

Trên production, thay vì `.env` hãy bơm thẳng các biến này qua secret manager của hạ tầng
(Kubernetes Secret, systemd `EnvironmentFile`, Azure/AWS parameter store…).

```bash
cd backend/src/ProductionManagement.Api
dotnet run --urls http://localhost:5080
```

Nếu không đặt `Bootstrap__Password`, hệ thống sinh mật khẩu ngẫu nhiên và **in ra log một lần duy nhất**.
Mật khẩu không bao giờ được hard-code trong migration (Step 3 §15).

Tài khoản mặc định: `manager` (đổi qua `Bootstrap__Username`).

Muốn tạo lại tài khoản từ đầu:

```bash
docker compose down -v && docker compose up -d
```

### 2.4. Nạp dữ liệu mẫu (khuyến nghị)

```bash
PM_PASSWORD='<mật-khẩu-của-bạn>' node mock-data/seed.mjs
```

Tạo 8 đơn hàng bao phủ mọi trạng thái màn hình: đúng tiến độ, chậm chưa xử lý, đã bù Option 1,
đã bù Option 2 + lịch sử hoàn tác, hoàn thành, quá hạn, chưa nhập sản lượng, có ngày nghỉ.

Chi tiết: [`mock-data/README.md`](mock-data/README.md).

### 2.5. Khởi động frontend

```bash
cd frontend
npm install
npm run dev
```

Mở <http://localhost:5173>.

Vite proxy `/api` sang `http://localhost:5080`, nên cookie xác thực là cookie first-party —
không cần cấu hình CORS.

---

## 3. Cấu trúc

```text
backend/
├── src/
│   ├── ProductionManagement.Domain/          entity, invariant, allocation strategy
│   ├── ProductionManagement.Application/     use case, DTO, abstraction
│   ├── ProductionManagement.Infrastructure/  EF Core, migration, hashing, clock
│   └── ProductionManagement.Api/             controller, cookie auth, error model
└── tests/
    ├── ProductionManagement.UnitTests/        business rule
    └── ProductionManagement.IntegrationTests/ API + PostgreSQL thật + concurrency

frontend/src/
├── api/           client.ts, errors.ts
├── app/           router, providers, layouts, config
├── features/      auth, orders, production, adjustments, statistics
└── shared/        components, dialogs, feedback, hooks, lib
```

---

## 4. Chạy test

```bash
cd backend
dotnet test
```

Integration test tự tạo và xoá một database PostgreSQL riêng cho mỗi lần chạy, nên cần
`docker compose up -d` và biến `PGPASSWORD` (nạp từ `.env` như mục 2.3) trước.
Host/port/user có thể ghi đè qua `PM_TEST_POSTGRES`; mật khẩu tài khoản test được sinh
ngẫu nhiên mỗi lần chạy trừ khi đặt `PM_TEST_PASSWORD`.

```bash
cd frontend
npm run typecheck
npm run build
```

---

## 5. API

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

Error contract:

```json
{ "code": "ACTUAL_EXCEEDS_ORDER_QUANTITY", "message": "…", "details": null }
```

| HTTP | Ý nghĩa |
|---|---|
| 400 | Sai định dạng / validation |
| 401 | Chưa đăng nhập |
| 403 | Tài khoản bị vô hiệu hoá |
| 404 | Không tìm thấy |
| 409 | Xung đột nghiệp vụ / concurrency |
| 422 | Vi phạm business rule |

---

## 6. Business rule bất biến

| Rule | Nơi bảo vệ |
|---|---|
| `SUM(actual) <= Order.Quantity` | transaction + row lock trên `orders` |
| Actual là giá trị, không phải increment | chỉ có `POST` (tạo) và `PUT` (thay thế) |
| 1 ProductionRecord / Order / ngày | `UNIQUE(order_id, production_date)` |
| Actual `0` hợp lệ, khác với "chưa nhập" | `actualQuantity: number \| null` |
| Ngày có kế hoạch = 0 không được nhập actual | `PLAN_QUANTITY_IS_ZERO` |
| Shortage là giá trị dẫn xuất | không có bảng shortage |
| `SUM(InitialPlannedQuantity) == Order.Quantity` | `Order.Create` trong 1 transaction |
| `InitialPlannedQuantity` không đổi | không có API sửa |
| Bù sản lượng không giảm kế hoạch ngày khác | chỉ có `AddOn` / `RemoveAddOn` |
| `SUM(item.AddOnQuantity) == shortage` | `PlanAdjustment.Apply` |
| Không trùng ngày trong 1 lần bù | `UNIQUE(plan_adjustment_id, production_plan_id)` |
| Preview không persist | endpoint preview không ghi DB |
| Apply luôn tính lại state hiện tại | `ADJUSTMENT_OUTDATED` (409) |
| Adjustment đã Apply là bất biến | chỉ có Reverse, không có PUT/DELETE |
| Tối đa 1 Applied Adjustment / source plan | `ACTIVE_ADJUSTMENT_EXISTS` (409) |
| Sửa actual làm đổi số thiếu → phần bù được tính lại | `ActiveAdjustmentRecalculator` (reverse + apply lại trong cùng transaction) |
| Trạng thái đơn suy ra từ tổng actual | `Order.RecalculateStatus` |
| HttpOnly Cookie Authentication | không dùng JWT, không dùng localStorage |

---

## 7. Quyết định triển khai khi tài liệu có mâu thuẫn

Áp dụng đúng thứ tự ưu tiên trong `docs/Production_Management_Implementation_Prompt_for_Claude.md` §3.

1. **Tổng kế hoạch sau khi bù.** `order-detail-screen-spec.md` §4.6 nói tổng kế hoạch không được vượt
   tổng đơn, nhưng master summary §4.3 và Option 1/2 spec khẳng định tổng kế hoạch cuối **có thể**
   lớn hơn tổng đơn sau add-on. → Giữ business invariant: add-on được phép đẩy tổng kế hoạch vượt
   tổng đơn; chỉ **tổng thực tế** bị chặn bởi tổng đơn.

2. **Activity / Audit History.** Các screen spec mô tả một Activity History đầy đủ (kèm lý do,
   before/after), nhưng schema đã chốt (Step 3) chỉ có 6 bảng và §44 cấm "global audit log system".
   → Không thêm bảng. Thông tin audit được lấy từ nguồn sẵn có: lịch sử bù sản lượng (người tạo /
   áp dụng / hoàn tác + thời điểm) và cột `created_by` / `updated_by` của `production_records`
   (hiển thị "người nhập sản lượng" trong timeline). Trường **lý do** khi sửa sản lượng không có
   cột trong schema đã chốt nên **không** được triển khai.

3. **"Điều chỉnh kế hoạch" chủ động.** Screen spec có nhắc nghiệp vụ này, nhưng API contract đã chốt
   (Step 4 §20) không có endpoint tương ứng và §21 loại trừ các API ngoài danh sách. → Ngoài phạm vi
   Phase 1. Chỉ có bù sản lượng thiếu (add-on) được triển khai.

4. **Ngày được nhận bù.** Option 2 spec §4.2 nói "tất cả ngày còn lại sau ngày thiếu"; master summary
   §8 Rule 7 và §11 cấm điều chỉnh kế hoạch của ngày đã qua. → Ngày hợp lệ = `date > sourceDate`
   **và** `date >= hôm nay`.

5. **Đơn Hoàn thành → read-only.** `order-detail-screen-spec.md` §4.10 yêu cầu khoá toàn bộ thao tác
   thay đổi; nhưng Step 1 §13 và Step 4 §7 quy định tổng actual giảm xuống dưới tổng đơn thì đơn
   **quay lại** Chưa hoàn thành — điều này chỉ xảy ra khi sửa được actual của đơn đã hoàn thành.
   → Giữ business invariant: UI ẩn nút *nhập mới* trên đơn đã hoàn thành nhưng vẫn cho **sửa** bản
   ghi sẵn có; mọi thay đổi vẫn bị chặn bởi `SUM(actual) <= Order.Quantity`.

6. **Cách tính "Chậm tiến độ".** Tài liệu không nói rõ ngày hôm nay có được tính vào kế hoạch lũy kế
   hay không. Actual được nhập cuối ngày, nên nếu tính cả hôm nay thì mọi đơn đều "chậm" vào mỗi buổi
   sáng và cảnh báo mất ý nghĩa. → Ngày hôm nay chỉ được tính khi **đã nhập actual**. Đây là quyết
   định kỹ thuật, không ảnh hưởng business invariant nào.

7. **Sửa actual của ngày đã bù sản lượng.** Tài liệu mô tả luồng bù như một thao tác một chiều và
   không nói gì về việc sản lượng của ngày thiếu bị sửa lại sau đó. Nhưng số lượng thiếu là **giá trị
   dẫn xuất** từ actual, nên nếu actual đổi mà phần bù giữ nguyên thì kế hoạch các ngày sau đang mang
   một con số không còn đúng với thực tế. → Khi actual thay đổi làm đổi số thiếu, phần bù đang áp dụng
   được **tính lại** ngay trong transaction đó, giữ đúng lựa chọn ban đầu của quản lý: *Chọn ngày để
   bù* thì vẫn đúng (các) ngày đã chọn, *Hệ thống chia đều* thì chia đều lại cho các ngày còn lại.
   Adjustment cũ **không bị sửa** — nó được Reverse và một adjustment mới được Apply, đúng cách sửa
   sai đã quy định ở §6 bảng trên. Ba trường hợp biên:
   - Hết thiếu → chỉ gỡ phần bù, không tạo adjustment mới.
   - Số thiếu không đổi → không làm gì, tránh rác lịch sử.
   - Ngày đã chọn nay thành quá khứ → bị loại; nếu không còn ngày nào hợp lệ thì phần bù cũ vẫn được
     gỡ và số thiếu được báo về là **chưa xử lý** thay vì âm thầm bù vào một ngày quản lý không chọn.
