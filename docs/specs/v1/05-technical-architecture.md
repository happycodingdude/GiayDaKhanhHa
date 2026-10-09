# 05 — Technical Architecture — Bản 1

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Trạng thái** | Draft (as-built) — chờ Chốt |
| **Nguồn** | Sinh lại từ codebase tại commit `74a9fc2` (09/10/2026). Code là nguồn sự thật; tài liệu cũ trong `docs/` chỉ dùng để lấy bối cảnh. |
| **Role** | Principal Software Architect + Solution Architect |
| **Spec đầu vào** | `02-product-requirements.md`, `03-feature-specifications.md`, `04-ux-ui-specifications.md` |

> Spec này mô tả kiến trúc **đang chạy**. Mọi mục "Confirmed" là hành vi đã có trong code. Chỗ nào code không nói lên được lý do, mục đó được đánh dấu `Assumption` hoặc `Open Question`.

---

## 1. Confirmed Requirements

| Mã | Nội dung | Bằng chứng trong code |
|---|---|---|
| TA-C01 | Hệ thống là **web app một người dùng** (quản lý sản xuất), gồm SPA React + REST API .NET + PostgreSQL. | `frontend/`, `backend/src/ProductionManagement.*`, `docker-compose.yml` |
| TA-C02 | Backend là **modular monolith 4 project**: Domain → Application → Infrastructure → Api. Phụ thuộc một chiều, Domain không tham chiếu framework. | `*.csproj`, `Domain.csproj` không có PackageReference |
| TA-C03 | Xác thực bằng **cookie HttpOnly** (`pm.auth`), không JWT, không token trong JavaScript. Mọi endpoint nghiệp vụ bắt buộc đăng nhập (FallbackPolicy), chỉ `login`/`logout` miễn. | `Program.cs`, `AuthController.cs` |
| TA-C04 | **Không có phân quyền**: một loại người dùng, không bảng Role/Permission. | `User.cs`, không có claim role |
| TA-C05 | **Không có file cấu hình trong repo**: `appsettings*.json` bị gitignore; toàn bộ cấu hình đến từ biến môi trường. | `.gitignore`, `README.md` |
| TA-C06 | Mật khẩu băm **PBKDF2-HMAC-SHA256, 210.000 vòng**, salt 16 byte, so sánh thời gian cố định. | `Pbkdf2PasswordHasher.cs` |
| TA-C07 | Tài khoản đầu tiên do bootstrap tạo khi DB chưa có user; mật khẩu lấy từ `Bootstrap__Password`, không có thì sinh ngẫu nhiên và ghi log đúng một lần. **Không hard-code mật khẩu trong migration.** | `DatabaseInitializer.cs` |
| TA-C08 | Migration tự chạy lúc khởi động (`Database__AutoMigrate`, mặc định `true`). | `DatabaseInitializer.cs` |
| TA-C09 | Ngày nghiệp vụ (`production_date`, `start_date`, `due_date`) là kiểu `date`, không múi giờ. "Hôm nay" tính theo múi giờ nghiệp vụ `Asia/Ho_Chi_Minh` (override bằng `Business__TimeZone`). Dấu thời gian audit lưu UTC. | `SystemClock.cs`, EF configurations |
| TA-C10 | **Mọi giá trị suy ra không lưu xuống DB**: tổng thực tế, còn lại, % tiến độ, chậm tiến độ, phần thiếu, trạng thái hiển thị của ngày. | `OrderDerivedValues.cs`, `ProductionCalculations.cs`, `ProductionDayQueries.cs` |
| TA-C11 | Bất biến liên dòng được bảo vệ bằng **transaction + row lock `SELECT … FOR UPDATE`**, không dùng trigger hay cột version. Thứ tự khoá thống nhất: **Order → ProductionDay → ProductionPlan** (plans khoá theo `ORDER BY id`). | `AppDbContext.cs`, `IAppDbContext.cs` |
| TA-C12 | Unique index là chốt chặn cuối cho race; vi phạm `23505` được dịch thành `409`. | `OrderService.cs`, `ProductionLineService.cs` |
| TA-C13 | Enum lưu dạng `varchar` + `CHECK`, không dùng enum gốc PostgreSQL. | mọi `*Configuration.cs` |
| TA-C14 | Id là **UUID v7** sinh ở application (`Guid.CreateVersion7()`), DB không tự sinh. | mọi entity |
| TA-C15 | Ảnh mẫu lưu **local disk** của server; DB chỉ giữ đường dẫn tương đối; phục vụ qua endpoint có xác thực; tên file do server sinh. | `LocalOrderImageStorage.cs`, `OrderImageService.cs` |
| TA-C16 | Định dạng ảnh xác định bằng **chữ ký byte** (JPEG/PNG/WEBP), không tin phần mở rộng hay Content-Type; giới hạn 5 MB; request size limit 5 MB + 512 KB. | `OrderImageRules.cs`, `OrdersController.cs` |
| TA-C17 | Hợp đồng lỗi duy nhất `{ code, message, details }`; ánh xạ exception → HTTP: 400/401/403/404/409/422/500. Chi tiết kỹ thuật không lộ ra client. | `ExceptionHandlingMiddleware.cs`, `ApiError.cs` |
| TA-C18 | Frontend: React 19 + TypeScript + Vite 8, TanStack Query 5 (server state), TanStack Router 1 (routing + guard), CSS thuần với design token trong `index.css`. **Không dùng Tailwind.** | `package.json`, `index.css` |
| TA-C19 | Frontend gọi API qua **một client duy nhất** (`api/client.ts`), proxy `/api` cùng origin (Vite) để cookie là first-party, không cần CORS. | `client.ts`, `vite.config.ts` |
| TA-C20 | **Không optimistic update** cho thao tác sản xuất; sau mutation invalidate query theo bộ key tập trung. | `useInvalidateOrder.ts`, `queryKeys.ts` |
| TA-C21 | Dữ liệu dev nạp bằng script ghi thẳng PostgreSQL (`mock-data/seed.mjs` + `scenarios.json`), không qua API. | `mock-data/` |
| TA-C22 | Repo **không chứa project test**. | `README.md` §4, `backend/ProductionManagement.sln` |

---

## 2. Assumptions

| Mã | Giả định | Lý do |
|---|---|---|
| TA-A01 | Trình duyệt của quản lý chạy cùng múi giờ với `Business__TimeZone`. | Frontend tính `today()` theo giờ trình duyệt (`shared/lib/date.ts`), backend tính theo múi giờ nghiệp vụ. Lệch múi giờ sẽ làm dòng "Hôm nay" trên ma trận và nút thao tác lệch nhau một ngày. |
| TA-A02 | Quy mô dữ liệu nhỏ (vài chục đơn, vài dây chuyền). | Dashboard nạp **toàn bộ** `orders`, `production_plans`, `production_days` vào bộ nhớ rồi tính (`StatisticsService.GetDashboardAsync`). Không phân trang, không cache. |
| TA-A03 | Một instance backend duy nhất. | Khoá DataProtection lưu file hệ thống; ảnh lưu local disk; không có shared storage. |
| TA-A04 | Triển khai thực tế nằm ngoài phạm vi bản này. | Trong repo chỉ có `docker-compose.yml` cho PostgreSQL. Các file Docker cho backend/frontend hiện chưa commit và theo quyết định của chủ dự án **không đưa vào spec**. |

---

## 3. Open Questions

| Mã | Câu hỏi | Ảnh hưởng |
|---|---|---|
| TA-Q01 | Có cần đồng bộ "hôm nay" giữa frontend và backend (ví dụ backend trả `today` trong mọi response) để loại bỏ rủi ro TA-A01 không? | Nếu người dùng mở app từ máy khác múi giờ, badge/ nút thao tác hiển thị sai ngày. Dashboard đã dùng `data.date` từ backend; các màn khác chưa. |
| TA-Q02 | `docker-compose.yml` nhắc tới `.env.example` nhưng file này không tồn tại trong repo. Có cần tạo không? | Người mới clone repo không biết cần biến nào. |
| TA-Q03 | Có cần job dọn file ảnh mồ côi không? | `LocalOrderImageStorage.Delete` ghi log khi xoá thất bại và "để job dọn dẹp xử lý sau" nhưng job đó chưa tồn tại. |

---

## 4. Decisions

Các quyết định kiến trúc đã thể hiện trong code, kèm lý do (lấy từ comment trong code và tài liệu cũ).

| Mã | Quyết định | Lý do |
|---|---|---|
| TA-D01 | Cookie auth thay cho JWT. | Một người dùng, SPA cùng origin; cookie HttpOnly loại bỏ rủi ro token nằm trong JS. |
| TA-D02 | Row lock thay cho optimistic concurrency. | Các bất biến chéo bảng (tổng ghi nhận ≤ kế hoạch ô, tổng thực tế ≤ số lượng đơn) cần tuần tự hoá request trên cùng đơn hàng. Lock theo thứ tự cố định để tránh deadlock. |
| TA-D03 | Không có bảng/cột lưu giá trị suy ra, không có bảng dashboard, không có bảng shortage. | Tránh lệch dữ liệu; một khoản bù làm kế hoạch ngày đổi thì trạng thái lưu cứng sẽ sai ngay. |
| TA-D04 | Trạng thái hiển thị của ngày (`NoPlan/NotStarted/InProduction/Closed`) chỉ tồn tại ở DTO, do server tính; frontend không tự tính. | Tránh hai phía hiểu khác nhau; `NoPlan` xét trước `NotStarted`. |
| TA-D05 | Xoá mềm `production_entries` (`deleted_at`) + global query filter; vết thay đổi riêng ở `production_entry_logs`. | Lịch sử "đã nhập những gì" phải dựng lại được; mọi phép SUM tự bỏ dòng đã xoá. |
| TA-D06 | Không hard-delete dây chuyền (chỉ bật/tắt); mọi FK sang `production_lines` là `Restrict`. | Kế hoạch và sản lượng tham chiếu dây chuyền; lịch sử không được biến mất. |
| TA-D07 | `plan_adjustments` chủ đích **không có `order_id`**; đơn được truy qua kế hoạch nguồn. | Giữ một nguồn sự thật, tránh hai cột có thể lệch nhau. |
| TA-D08 | Ghi file ảnh **trước** commit DB; xoá file cũ **sau** commit. | Commit hỏng thì dọn file mới được; xoá file trước commit mà rollback thì DB trỏ tới file đã mất. |
| TA-D09 | Thuật toán phân bổ tự động (Option 2) đứng sau interface `IAutomaticAllocationStrategy`. | Có thể đổi luật chia mà không đụng luồng điều chỉnh. Hiện chỉ có một implementation chia đều. |
| TA-D10 | Frontend và backend dùng **cùng một hàm chia đều** (phần dư dồn vào các phần tử đầu). | Frontend điền sẵn, backend validate; lệch một đơn vị là người dùng thấy tổng không khớp mà không hiểu vì sao. |
| TA-D11 | Preview điều chỉnh là **mutation**, không phải query; kết quả chỉ là state UI, không cache. | Preview không lưu gì, không được coi là dữ liệu server. |
| TA-D12 | Multipart cho tạo/sửa đơn (ảnh gửi kèm cùng request). | Thông tin và ảnh được lưu trong **một** lần; không có khoảnh khắc đơn tồn tại mà ảnh chưa lên. |
| TA-D13 | Giữ redirect từ route cũ `/orders*` sang `/goods-receipt` và `/progress`. | Bookmark của bản trước không vỡ. |
| TA-D14 | Không viết test trong repo. | Quyết định của chủ dự án: test tạo tạm ngoài repo, không push. |

---

## 5. Detailed Specification

### 5.1 Tổng quan kiến trúc

```text
┌──────────────────────────────┐        /api/v1 (same-origin proxy)        ┌──────────────────────────────┐
│  Frontend SPA                │  ───────────────────────────────────────▶ │  Backend REST API            │
│  React 19 · TS · Vite 8      │  cookie pm.auth (HttpOnly, SameSite=Lax)  │  ASP.NET Core (.NET 10)      │
│  TanStack Query / Router     │ ◀─────────────────────────────────────── │  4 project (modular monolith)│
└──────────────────────────────┘        JSON · multipart (ảnh)             └──────────────┬───────────────┘
                                                                                          │ EF Core 10 + Npgsql
                                                                                          ▼
                                                                           ┌──────────────────────────────┐
                                                                           │  PostgreSQL 17 (docker)      │
                                                                           │  11 bảng · 5 migration       │
                                                                           └──────────────────────────────┘
                                                                           ┌──────────────────────────────┐
                                                                           │  Local disk: ảnh mẫu         │
                                                                           │  Storage__OrderImagesPath    │
                                                                           └──────────────────────────────┘
```

### 5.2 Technology stack (as-built)

| Thành phần | Công nghệ | Phiên bản |
|---|---|---|
| Runtime backend | .NET | 10 |
| Web framework | ASP.NET Core MVC (controllers) | 10 |
| ORM | Entity Framework Core + Npgsql | 10.0.1 / 10.0.0 |
| Database | PostgreSQL | 17-alpine (docker) |
| Frontend | React / TypeScript | 19.2 / 5.9 |
| Build | Vite | 8 |
| Server state | @tanstack/react-query | 5 |
| Routing | @tanstack/react-router | 1 |
| Form (chỉ màn đăng nhập) | react-hook-form + zod | 7 / 4 |
| Styling | CSS thuần, design token trong `index.css` | — |
| Dev data | Node script `mock-data/seed.mjs` (psql qua docker) | — |

### 5.3 Backend architecture

#### 5.3.1 Phân tầng

| Project | Trách nhiệm | Thành phần chính |
|---|---|---|
| `ProductionManagement.Domain` | Entity, enum, mã lỗi, exception, bất biến về **con số**, domain service thuần. Không phụ thuộc framework. | `Order` (aggregate root), `ProductionPlan`, `ProductionDay`, `ProductionEntry`, `ProductionEntryLog`, `OrderProductionLine`, `ProductionLine`, `PlanAdjustment`, `PlanAdjustmentItem`, `SystemSettings`, `User`; `EvenDistribution`, `EvenDistributionAllocationStrategy`, `ProductionCalculations`; `ErrorCodes`; 5 exception |
| `ProductionManagement.Application` | Use case, DTO, abstraction hạ tầng, luật cần chạm DB. | Service theo feature: `AuthService`, `OrderService`, `OrderImageService`, `ProductionScheduleService`, `ProductionDayService`, `AdjustmentService` (+ `AdjustmentRules`), `ProductionLineService`, `SettingsService`, `StatisticsService`; Common: `OrderDerivedCalculator`, `OrderMutationGuard`, `ProductionDayQueries`, `OrderQueries`; Abstractions: `IAppDbContext`, `IClock`, `ICurrentUser`, `IPasswordHasher`, `IOrderImageStorage` |
| `ProductionManagement.Infrastructure` | EF Core, migration, row lock, hashing, storage, clock, DI. | `AppDbContext`, 11 `IEntityTypeConfiguration`, 5 migration, `Pbkdf2PasswordHasher`, `LocalOrderImageStorage`, `SystemClock`, `DependencyInjection` |
| `ProductionManagement.Api` | HTTP: controller, cookie auth, middleware lỗi, bootstrap. | 7 controller, `ExceptionHandlingMiddleware`, `HttpContextCurrentUser`, `DatabaseInitializer`, `Program` |

Nguyên tắc phân chia trách nhiệm (thể hiện nhất quán trong code):

- Mọi bất biến về **con số** nằm trong aggregate `Order` hoặc entity (ví dụ tổng phân bổ = số lượng đơn, tổng kế hoạch theo ngày = phân bổ dây chuyền, add-on > 0).
- Service application lo phần cần **chạm DB**: dây chuyền có tồn tại/Active không, khoá dòng, mã giày trùng, tính giá trị suy ra.
- Controller chỉ chuyển đổi HTTP ↔ DTO; nơi duy nhất chạm `IFormFile` là `OrdersController`.

#### 5.3.2 Aggregate và vòng đời

- `Order` là aggregate root, sở hữu `OrderProductionLine[]` và `ProductionPlan[]` (navigation field-backed). `ProductionDay` và `ProductionEntry` được service thao tác trực tiếp qua `DbSet` nhưng luôn **sau khi khoá dòng `orders`**.
- Vòng đời trạng thái đơn: `Pending` → (Lập tiến độ) → `Incomplete` → (Xuất hàng đủ) → `Completed`. Chỉ `Unschedule` đưa đơn về `Pending`. `RecalculateStatus` **chỉ** chạy trong transaction Xuất hàng.
- Cờ `ScheduleConfirmedAt` tách "đã lập tiến độ" với "được sản xuất": chưa chốt thì sửa/xoá tiến độ được, chưa ghi nhận được.

#### 5.3.3 Lifecycle request ghi (mẫu chung)

```text
BeginTransaction
  → LockOrderAsync(orderId)            (SELECT … FOR UPDATE; 404 nếu không có)
  → nạp Order, kiểm guard: IsScheduled / IsScheduleConfirmed / OrderMutationGuard (quá hạn) / FutureDate
  → LockProductionDayAsync / LockProductionPlansAsync (theo thứ tự cố định)
  → kiểm bất biến số lượng bằng dữ liệu SỐNG (không tin client)
  → ghi entity + log
  → SaveChanges → Commit
→ đọc lại DTO đầy đủ để trả về (frontend không phải refetch)
```

#### 5.3.4 Thời gian

- `IClock.UtcNow` cho audit; `IClock.Today` = ngày theo `Business__TimeZone` (mặc định `Asia/Ho_Chi_Minh`, fallback UTC nếu id múi giờ không hợp lệ).
- JSON: `DateOnly` serialize `YYYY-MM-DD`; enum serialize theo **tên**.

### 5.4 Database design

Tên bảng/cột snake_case. `id` UUID v7 do app sinh. Mọi FK `ON DELETE RESTRICT` trừ ghi chú.

| Bảng | Cột chính | Ràng buộc / index |
|---|---|---|
| `users` | id, username(100), password_hash(255), display_name(100), status, created_at, updated_at | `uq_users_username`; CHECK status ∈ {Active, Inactive} |
| `production_lines` | id, code(30), name(100), status, sort_order (default 0), note(500), created_at, updated_at | `uq_production_lines_code`; CHECK status; CHECK sort_order ≥ 0 |
| `orders` | id, shoe_code(50), quantity, image_path(500), image_file_name(255), image_content_type(100), image_size_bytes, start_date, due_date, schedule_confirmed_at, status, created_at, updated_at | `uq_orders_shoe_code`; `ix_orders_status`; CHECK quantity > 0; CHECK status ∈ {Pending, Incomplete, Completed}; **CHECK ngày thuộc tiến độ**: Pending ⇒ cả hai ngày NULL; khác Pending ⇒ cả hai NOT NULL và start ≤ due; CHECK Pending ⇒ schedule_confirmed_at NULL; CHECK 4 cột ảnh cùng NULL hoặc cùng NOT NULL (size > 0) |
| `order_production_lines` | PK (order_id, production_line_id), allocated_quantity, created_at | CHECK allocated_quantity > 0; `ix_order_production_lines_line` |
| `production_plans` | id, order_id, production_line_id, production_date, initial_planned_quantity, planned_quantity, created_at, updated_at | `uq_production_plans_order_date_line`; `ix_production_plans_line`; CHECK cả hai số lượng ≥ 0 |
| `production_days` | id, order_id, production_line_id, production_date, status, actual_quantity, closed_at, closed_by, created_by, updated_by, created_at, updated_at | `uq_production_days_order_date_line`; `ix_production_days_line`; `ix_production_days_status_date`; CHECK actual ≥ 0; CHECK status ∈ {Open, Closed}; **CHECK nhất quán đóng**: Closed ⇒ closed_at, closed_by, actual_quantity NOT NULL; Open ⇒ cả ba NULL |
| `production_entries` | id, production_day_id, quantity, recorded_at, note(255), deleted_at, created_by, updated_by, created_at, updated_at | CHECK quantity > 0; `ix_production_entries_day_recorded_at` (recorded_at DESC); `ix_production_entries_day_active` (partial, `deleted_at IS NULL`); **global query filter** `deleted_at IS NULL`; **không** có unique (cho phép nhiều lần ghi nhận) |
| `production_entry_logs` | id, production_entry_id, action, old_quantity, new_quantity, old_note, new_note, changed_by, changed_at | CHECK action ∈ {Create, Update, Delete}; `ix_production_entry_logs_entry`; FK tới entry **không** đi kèm query filter |
| `system_settings` | id, recording_interval_minutes, remind_before_due, updated_by, updated_at | CHECK interval BETWEEN 5 AND 480; đúng một dòng, do bootstrap tạo |
| `plan_adjustments` | id, source_production_plan_id, shortage_quantity, adjustment_type, status, created_by, applied_by, reversed_by, created_at, applied_at, reversed_at | CHECK shortage > 0; CHECK type ∈ {Manual, Automatic}; CHECK status ∈ {Applied, Reversed}; `ix_plan_adjustments_source_plan`; **không có order_id** |
| `plan_adjustment_items` | id, plan_adjustment_id, production_plan_id, add_on_quantity | CHECK add_on > 0; `uq_plan_adjustment_items_adjustment_plan`; 2 index phụ |

Migration (theo thứ tự): `InitialSchema` → `CR01IntradayProductionEntries` → `CR01SimplifySystemSettings` → `CR001GoodsReceiptScheduleProductionLines` → `ConfirmProductionSchedule` (thêm `schedule_confirmed_at`; dữ liệu cũ đã lập tiến độ được coi là đã chốt: `schedule_confirmed_at = updated_at`).

Bất biến do DB giữ (không chỉ code): ngày thuộc tiến độ, nhất quán đóng ngày, 4 cột ảnh, một kế hoạch/một ô, một ô production_days/một ô ma trận, một dây chuyền/một đơn, mã giày và mã dây chuyền duy nhất.

### 5.5 API strategy

- Prefix `api/v1`, JSON, enum theo tên, ngày `YYYY-MM-DD`. Hợp đồng chi tiết (request/response/field) ở `03-feature-specifications.md` §5 và DTO trong `Application/Contracts`.
- Tạo/sửa đơn dùng `multipart/form-data` (field `shoeCode`, `quantity`, `image`, `removeImage`).
- Endpoint ghi trả về **state đầy đủ** sau khi ghi (`OrderDetailDto`, `ProductionCellDetailDto`, …) để frontend không refetch thêm.

| Nhóm | Method & path |
|---|---|
| Auth | `POST /auth/login` (anonymous) · `POST /auth/logout` (anonymous) · `GET /auth/me` |
| Dây chuyền | `GET /production-lines?status=` · `POST /production-lines` · `PUT /production-lines/{id}` · `PATCH /production-lines/{id}/status` |
| Nhập hàng | `POST /orders` (multipart, 201) · `PUT /orders/{id}` (multipart) · `DELETE /orders/{id}` (204) · `GET /orders?status&search&productionLineId&page&pageSize` · `GET /orders/{id}` |
| Ảnh mẫu | `PUT /orders/{id}/image` · `DELETE /orders/{id}/image` · `GET /orders/{id}/image/content` |
| Tiến độ | `POST /orders/{id}/production-schedule` · `PUT …/production-schedule` · `DELETE …/production-schedule` · `POST …/production-schedule/confirm` · `GET /orders/{id}/production-plans` (ma trận) |
| Sản lượng | `GET /orders/{id}/production-days/{date}/lines/{lineId}` · `POST …/entries` · `PUT /production-entries/{entryId}` · `DELETE /production-entries/{entryId}` · `POST /orders/{id}/production-days/{date}/close` (body rỗng) |
| Bù thiếu | `POST /production-plans/{planId}/adjustments/preview` · `POST /production-plans/{planId}/adjustments` · `POST /plan-adjustments/{id}/reverse` · `GET /orders/{id}/plan-adjustments` |
| Thống kê | `GET /orders/{id}/statistics` · `GET /statistics/dashboard` |
| Cấu hình | `GET /settings` · `PUT /settings` |

Hợp đồng lỗi:

```json
{ "code": "ENTRY_EXCEEDS_DAILY_PLAN", "message": "…", "details": [ { "field": "quantity", "code": "MAX_ALLOWED", "message": "40" } ] }
```

| HTTP | Exception | Ý nghĩa |
|---|---|---|
| 400 | `ValidationException` | Sai định dạng/field; `details` theo field |
| 401 | `UnauthenticatedException` | Chưa đăng nhập, sai thông tin đăng nhập, cookie cũ không parse được id |
| 403 | `ForbiddenException` | Tài khoản Inactive |
| 404 | `NotFoundException` | Không tìm thấy |
| 409 | `ConflictException` | Xung đột trạng thái: trùng mã, đã chốt, đã đóng ngày, đề xuất cũ, đã có bù… |
| 422 | `BusinessRuleException` | Vi phạm luật nghiệp vụ; `details` có thể mang con số trần (`MAX_ALLOWED`) |
| 500 | khác | `INTERNAL_ERROR`, log Error |

Danh sách mã lỗi nghiệp vụ: xem `ErrorCodes.cs` và bảng ánh xạ câu chữ tiếng Việt tại `frontend/src/api/errors.ts` (được chép vào `03-feature-specifications.md` §6).

### 5.6 Authentication / Authorization

- Cookie `pm.auth`: HttpOnly, SameSite=Lax, Secure=Always ngoài Development, hết hạn 12 giờ **trượt**.
- Request chưa xác thực nhận JSON `401 NOT_AUTHENTICATED`, không redirect HTML. Access denied → `403 FORBIDDEN`.
- Claim: `NameIdentifier` = user id (Guid), `Name` = username. `ICurrentUser.UserId` ném 401 nếu claim không parse được Guid (cookie phát hành từ bản id kiểu số).
- Đăng nhập: sai username và sai mật khẩu trả **cùng một lỗi** để không lộ username tồn tại. Tài khoản Inactive → 403.
- Khoá DataProtection: mặc định thư mục profile tiến trình; đặt `DataProtection__KeysPath` (volume) để phiên đăng nhập sống qua lần tạo lại container.
- Audit (`created_by`, `updated_by`, `closed_by`, `changed_by`, `applied_by`, …) luôn lấy từ `ICurrentUser`, **client không bao giờ gửi lên**.

### 5.7 Concurrency & data integrity

| Bất biến | Nơi bảo vệ |
|---|---|
| Tổng ghi nhận của ô ≤ kế hoạch ô; tổng thực tế toàn đơn ≤ số lượng đơn | Transaction + lock Order + lock ProductionDay; tính lại từ DB trước khi ghi (`GuardAllowance`) |
| Hai request lập tiến độ / xoá đơn song song trên cùng đơn | Lock dòng `orders` trước khi kiểm trạng thái |
| Hai điều chỉnh bù chồng ngày | Lock Order rồi lock các plan `ORDER BY id` |
| Tối đa 1 adjustment Applied / kế hoạch nguồn | `GuardNoActiveAdjustmentAsync` trong transaction |
| Mã giày / mã dây chuyền duy nhất | Kiểm trước + unique index; `23505` → 409 |
| Ô `production_days` lazily tạo | Tạo trong cùng transaction sau khi lock Order (tuần tự hoá, không race trên unique) |
| Preview không bao giờ tin được | Apply tính lại phần thiếu và (với Automatic) tính lại đề xuất, lệch là `409 ADJUSTMENT_OUTDATED` |

### 5.8 File storage (ảnh mẫu)

- Thư mục gốc: `Storage__OrderImagesPath`, mặc định `<BaseDirectory>/App_Data/order-images`, tạo lúc khởi tạo.
- Đường dẫn tương đối: `<2 ký tự đầu của orderId>/<orderId>-<guid ngẫu nhiên>.<ext>`, luôn dùng `/`.
- `Resolve` chặn path traversal: đường dẫn sau chuẩn hoá phải nằm trong thư mục gốc.
- `Delete` không ném khi file đã mất; lỗi IO chỉ ghi log Warning.
- Nội dung ảnh trả qua `GET /orders/{id}/image/content` với `Content-Disposition` tên file đã làm sạch; file mất trên disk → 404 `IMAGE_NOT_FOUND`.

### 5.9 Configuration

| Biến môi trường | Bắt buộc | Mặc định | Dùng ở |
|---|---|---|---|
| `ConnectionStrings__Default` | **Có** | — | `DependencyInjection` |
| `Database__AutoMigrate` | Không | `true` | `DatabaseInitializer` |
| `Bootstrap__Username` / `Bootstrap__DisplayName` | Không | `manager` / `Quản lý sản xuất` | tạo tài khoản đầu |
| `Bootstrap__Password` | Không | sinh ngẫu nhiên 16 ký tự, log một lần | tạo tài khoản đầu |
| `Bootstrap__SeedProductionLines` | Không | `false` | seed 3 dây chuyền mẫu khi danh mục rỗng |
| `Business__TimeZone` | Không | `Asia/Ho_Chi_Minh` | `SystemClock` |
| `Storage__OrderImagesPath` | Không | `App_Data/order-images` | ảnh mẫu |
| `DataProtection__KeysPath` | Không | thư mục profile | khoá cookie |
| `VITE_API_PROXY_TARGET` (frontend dev) | Không | `http://localhost:5080` | Vite proxy |
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_PORT` | password **có** | `production_management` / `postgres` / — / `5432` | `docker-compose.yml` |

### 5.10 Frontend architecture

```text
src/
├── api/        client.ts (fetch wrapper, upload multipart), errors.ts (ApiError, NetworkError, map mã lỗi → tiếng Việt)
├── app/        router (route tree + auth guard), providers (QueryClient + Toast + Router), layouts (AppLayout), config (queryKeys)
├── features/   auth · orders · production · adjustments · production-lines · settings · statistics
│   └── <feature>/ api/ (hàm gọi API) · hooks/ (useQuery/useMutation) · components/ · pages/ · types.ts (DTO)
└── shared/     components (ui, Select, Stepper, ImageUploader, ImageLightbox, StatusBadges), dialogs (Modal, ConfirmDialog),
                feedback (QueryState, ToastProvider), hooks (useInvalidateOrder, useDelayedFlag), lib (date, format)
```

- **Routing**: route cha `authenticated` chạy `beforeLoad` gọi `GET /auth/me` qua `queryClient.ensureQueryData`; 401/403 → redirect `/login`. Route: `/login`, `/dashboard`, `/goods-receipt`, `/goods-receipt/new`, `/goods-receipt/$orderId`, `/progress`, `/progress/new?orderId=`, `/progress/$orderId`, `/progress/$orderId/edit`, `/settings`, `/settings/production-lines`; redirect `/` → `/dashboard`, `/orders` → `/progress`, `/orders/new` → `/goods-receipt/new`, `/orders/$id` → `/progress/$id`. `defaultPreload: 'intent'`.
- **Server state**: QueryClient mặc định `staleTime` 15 s, không `refetchOnWindowFocus`, retry ≤ 2 lần và **không retry lỗi < 500**; mutation không retry. `currentUser` staleTime 60 s; `settings` 5 phút.
- **Query key** tập trung trong `queryKeys.ts`; mutation invalidate theo prefix: sau ghi nhận → ô + đơn + ma trận + thống kê + danh sách + dashboard; sau xuất hàng → thêm lịch sử bù; sau bù/hoàn tác → đơn + ma trận + thống kê + lịch sử bù + danh sách + dashboard; sau lập/sửa/xoá/chốt tiến độ → setQueryData đơn + invalidate danh sách, ma trận, thống kê, dashboard; dây chuyền → prefix `production-lines`.
- **Không optimistic update** (trần "Còn được nhập" chỉ server tính đúng).
- **Lỗi**: `toUserMessage()` ánh xạ `code` → câu tiếng Việt; hai lỗi vượt trần kèm số tối đa từ `details[MAX_ALLOWED]`; `NetworkError` khi không tới được server.
- **Draft wizard** lập tiến độ lưu `sessionStorage` (`create-schedule-draft`) để reload không mất bước; xoá khi rời trang bằng điều hướng trong app; chế độ sửa không dùng draft.
- **Ngày**: kiểu chuỗi `IsoDate` `YYYY-MM-DD`, không đi qua `Date` để tránh lệch múi giờ; hiển thị `dd/mm/yyyy`; timestamp UTC quy đổi local khi hiển thị `dd/mm/yyyy HH:mm`.
- **Styling**: CSS thuần với token (`--primary #1f6feb`, `--success`, `--warning`, `--danger`, `--info`, `--radius 10px`…), desktop-first, một breakpoint `max-width: 900px` (sidebar thành thanh ngang, lưới 2 cột về 1 cột, modal padding nhỏ).

### 5.11 Deployment approach (trong phạm vi repo)

1. `docker compose up -d` → PostgreSQL 17 (`giayda-postgres`, volume `pgdata`, healthcheck `pg_isready`).
2. Nạp `.env` vào môi trường → `dotnet run --urls http://localhost:5080` trong `backend/src/ProductionManagement.Api` (tự migrate + bootstrap).
3. `npm install && npm run dev` trong `frontend/` → `http://localhost:5173`, proxy `/api`.
4. (Tuỳ chọn) `node mock-data/seed.mjs` nạp 11 kịch bản.

Build check: `dotnet build` (backend), `npm run typecheck && npm run build` (frontend).

### 5.12 Observability, security, scalability

- **Logging**: `ILogger`; request bị từ chối (4xx) log Information kèm code; lỗi 5xx log Error kèm stack. Không có metric/tracing.
- **Security**: không secret trong repo; lỗi kỹ thuật không lộ; ảnh kiểm chữ ký byte và giới hạn kích thước; tên file server sinh; path traversal guard; cookie HttpOnly; mật khẩu PBKDF2.
- **Scalability**: thiết kế cho một xưởng, một quản lý. Điểm cần theo dõi khi dữ liệu lớn: `GetDashboardAsync` nạp toàn bộ bảng; `GET /orders` tính giá trị suy ra cho từng dòng của trang (3 query phụ). Xem `06-implementation-plan.md` §5.7 (technical debt).

---

## 6. Acceptance Criteria

| Mã | Tiêu chí | Trạng thái |
|---|---|---|
| TA-AC01 | `dotnet build` backend và `npm run typecheck && npm run build` frontend thành công. | Verified (theo README §4; chưa chạy lại trong phiên sinh spec) |
| TA-AC02 | Khởi động backend với DB trống tạo đủ 11 bảng, 1 user, 1 dòng `system_settings`; mật khẩu ngẫu nhiên xuất hiện trong log đúng một lần. | Implemented |
| TA-AC03 | Gọi bất kỳ endpoint nghiệp vụ nào không có cookie → `401` JSON, không redirect. | Implemented |
| TA-AC04 | Hai request ghi nhận sản lượng song song vào cùng ô không bao giờ làm tổng ô vượt kế hoạch (row lock). | Implemented, chưa có test tự động |
| TA-AC05 | Upload file PDF đổi tên `.jpg` bị từ chối `400 IMAGE_TYPE_NOT_SUPPORTED` ở cả client và server. | Implemented |
| TA-AC06 | Mọi response lỗi tuân theo khuôn `{code, message, details}`. | Implemented |
| TA-AC07 | Đường dẫn vật lý của ảnh không xuất hiện trong bất kỳ response nào. | Implemented |
| TA-AC08 | Reload trang giữa wizard lập tiến độ giữ nguyên bước và dữ liệu đang nhập. | Implemented |
