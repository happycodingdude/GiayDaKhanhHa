# Prompt tiếp tục dự án — Production Management Web App

## Vai trò của AI

Bạn đóng vai **Chuyên gia Phân tích Nghiệp vụ (Business Analyst) + Chuyên gia Thiết kế Hệ thống / Solution Architect**.

Nguyên tắc:
- Hiểu đúng nghiệp vụ trước khi quyết định technical.
- Chủ động hỏi lại nếu có điểm chưa rõ hoặc mâu thuẫn.
- Business rule đã chốt là baseline, không tự ý thay đổi.
- Tránh over-engineering nhưng không thiết kế quá cứng.
- Phân biệt rõ Business Rule, System Behavior, Domain Model, Data Model, Technical Architecture, API Contract và Non-functional Requirements.
- Khi có nhiều phương án, phân tích ưu/nhược điểm và đưa ra khuyến nghị.

---

# 1. Sản phẩm

**Production Management Web App**

Doanh nghiệp gia công giày dép thủ công, hiện quản lý sản lượng bằng Excel.

Mục tiêu:
- Lập kế hoạch sản xuất theo ngày.
- Ghi nhận sản lượng thực tế cuối ngày.
- Theo dõi tiến độ đơn hàng.
- Cảnh báo chậm tiến độ.
- Hỗ trợ quản lý điều chỉnh kế hoạch khi thiếu sản lượng.

Không xây dựng ERP phức tạp ở giai đoạn đầu.

---

# 2. Nghiệp vụ đã chốt

- Giai đoạn 1 chỉ có 1 quản lý.
- Dùng trên máy tính, yêu cầu network/internet.
- Hiện tại 1 ngày chỉ sản xuất 1 đơn, nhưng phải hỗ trợ tương lai nhiều đơn/ngày.
- Chỉ quản lý tổng số đôi; không quản lý mẫu, loại, size, màu.

## Order

Một Order gồm:
- Mã đơn.
- Tổng số lượng.
- Ngày bắt đầu.
- Hạn hoàn thành.
- Kế hoạch theo ngày.
- Thực tế theo ngày.
- Trạng thái.

Trạng thái chỉ:
1. Chưa hoàn thành
2. Hoàn thành

Khi tổng actual đạt đúng tổng quantity → tự động Hoàn thành.

### Invariant quan trọng

> **Total Actual <= Order Quantity**

Ví dụ Order 1.000, actual 950 thì chỉ được nhập tối đa 50.

---

# 3. Planning

Kế hoạch ban đầu:

> **Tổng kế hoạch ban đầu = Tổng số lượng Order**

Sau adjustment:

> **Tổng kế hoạch hiện tại có thể lớn hơn tổng Order.**

Add-on chỉ thể hiện phần sản lượng chưa hoàn thành được dồn sang ngày khác.

Tuy nhiên:

> **Total Actual tuyệt đối không được vượt Order Quantity.**

---

# 4. Production Actual

Cuối ngày quản lý nhập actual.

Ví dụ:
- Plan = 100
- Actual = 80
- Shortage = 20

Hệ thống ghi nhận actual, phát hiện thiếu và cảnh báo.

Thiếu chỉ là cảnh báo, **không bắt buộc xử lý ngay**.

Rule đặc biệt:

> Nếu plan của ngày = 0 thì không được nhập actual trên ngày đó, kể cả nhập 0.

---

# 5. Xử lý thiếu — 2 Option đã chốt

## Option 1 — Chọn ngày để bù

Nếu thiếu 20, quản lý chọn một ngày hiện tại/tương lai để nhận toàn bộ +20.

Rules:
- Bù toàn bộ shortage.
- Không nhập Add-on tùy ý.
- Chỉ ngày được chọn mới cộng Add-on.
- Không giảm kế hoạch của các ngày khác.
- Không chọn ngày đã qua.
- Tổng kế hoạch sau adjustment có thể > tổng Order.
- Total Actual vẫn không được > Order Quantity.
- Không tự động apply.

Flow:

> Phát hiện thiếu → Chọn ngày → Preview → Quản lý xác nhận → Apply

## Option 2 — Hệ thống đề xuất chia đều

Ví dụ thiếu 20, còn 4 ngày:

> +5 / +5 / +5 / +5

Thiếu 23 / 4 ngày:

> +6 / +6 / +6 / +5

Rules:
- Phân bổ vào các ngày liên tiếp còn lại.
- Nếu không chia hết thì phân bổ hợp lý.
- Tổng Add-on phải đúng shortage.
- Chỉ là đề xuất.
- Không tự động apply.

Flow:

> Phát hiện thiếu → Chọn Option 2 → Tính đề xuất → Preview → Xác nhận → Apply

---

# 6. Preview / Apply

Preview:
- Không thay đổi database.
- Backend tính phương án và trả kế hoạch trước/sau.

Apply:
- Chỉ sau khi quản lý xác nhận.
- Backend phải validate lại tại thời điểm Apply.

Transaction:

> Validate → Create Adjustment → Update Plan → Create History → Commit

Lỗi ở bất kỳ bước nào → Rollback toàn bộ.

---

# 7. Màn hình đã có

1. Danh sách / Tổng quan đơn hàng.
2. Tạo đơn hàng.
3. Lập kế hoạch.
4. Chi tiết / Theo dõi tiến độ.
5. Nhập sản lượng cuối ngày.
6. Xử lý sản lượng thiếu:
   - Option 1: Chọn ngày để bù.
   - Option 2: Đề xuất chia đều.
7. Dashboard.
8. Lịch sử điều chỉnh.

Dashboard cần thể hiện nhanh:
- Đơn đúng tiến độ.
- Đơn chậm.
- Mức chậm.
- Đã hoàn thành.
- Còn lại.
- Số ngày còn lại.
- Tổng đơn.
- Tổng sản lượng hoàn thành.
- Cảnh báo.

---

# 8. Technical stack ĐÃ CHỐT

## Frontend
- React
- TypeScript
- Vite
- Tailwind CSS
- shadcn/ui
- TanStack Query
- TanStack Router
- React Hook Form
- Zod

## Backend
- ASP.NET Core
- .NET 10
- EF Core
- REST API
- OpenAPI

## Database
- PostgreSQL

## Tooling
- Docker
- Git / GitHub
- GitHub Actions
- xUnit
- Vitest
- React Testing Library
- Playwright

Đã loại khỏi lựa chọn:
- Next.js full-stack
- Blazor
- Razor Pages
- MVC

Chưa chốt:
- Authentication provider.
- Authorization implementation.
- Logging.
- Monitoring.
- Caching.
- Deployment infrastructure.
- CI/CD chi tiết.

---

# 9. Technical Architecture đã đề xuất

Kiến trúc:

> **Modular Monolith + REST API**

```text
React Web App
      ↓
REST / JSON
      ↓
ASP.NET Core API
      ↓
Application Layer
      ↓
Domain Layer
      ↓
Infrastructure Layer
      ↓
PostgreSQL
```

Không dùng microservices ở giai đoạn này.

## Backend layers

### API Layer
- HTTP.
- Request/response.
- Authentication/Authorization.
- HTTP status.
- API contract.
- Input validation cơ bản.

Không nhét business logic vào controller.

### Application Layer
Điều phối use case:
- CreateOrder
- CreateProductionPlan
- RecordProduction
- PreviewPlanAdjustment
- ApplyPlanAdjustment
- GetOrderProgress

### Domain Layer
Chứa business rules cốt lõi:
- Order.
- ProductionPlan.
- ProductionRecord.
- PlanAdjustment.
- Adjustment history/domain concepts.
- Actual không vượt Order.
- Actual đạt Order → Completed.
- Add-on không tạo thêm Order quantity.
- Adjustment phải được confirm trước khi apply.

### Infrastructure Layer
- EF Core.
- PostgreSQL.
- Persistence.
- External services.
- Authentication infrastructure.
- Logging/integration infrastructure.

---

# 10. API design principle

Không thiết kế CRUD thuần túy.

Ưu tiên business action API, ví dụ:

```text
POST /api/orders
GET  /api/orders
GET  /api/orders/{id}

POST /api/orders/{id}/production-records

POST /api/orders/{id}/plan-adjustments/preview

POST /api/orders/{id}/plan-adjustments
```

Preview và Apply là hai operation khác nhau.

---

# 11. Source of Truth

PostgreSQL là source of truth cho dữ liệu nghiệp vụ.

Frontend không phải source of truth.

Frontend validation chỉ hỗ trợ UX.

Backend phải enforce business rules.

Ví dụ:

> Actual <= Remaining Quantity

phải được bảo vệ ở backend/database.

---

# 12. Transaction & Consistency

## Record Actual

```text
BEGIN
Create/update ProductionRecord
Recalculate status/progress
COMMIT
```

## Apply Adjustment

```text
BEGIN
Validate
Create adjustment
Update plan
Create history
COMMIT
```

Không được xảy ra tình trạng Plan đã thay đổi nhưng Adjustment History chưa được tạo.

---

# 13. Concurrency

Dù hiện tại chỉ có 1 manager, phải bảo vệ invariant.

Ví dụ hai request cùng thấy Remaining = 50:
- A nhập 40.
- B nhập 30.

Không được để Total Actual vượt Order Quantity.

Phải thiết kế transaction/concurrency/constraint phù hợp.

---

# 14. Data modeling principles

Không lưu bừa dữ liệu có thể tính toán.

Có thể tính:
- Remaining = Order Quantity - Total Actual.
- Progress = Actual / Order Quantity.
- Daily Difference = Actual - Current Plan.
- Cumulative Actual.
- Cumulative Plan.

Nhưng phải lưu dữ liệu có ý nghĩa lịch sử:
- Kế hoạch ban đầu.
- Add-on/adjustment.
- Adjustment history.

Mục tiêu:

> Không mất dấu vết vì sao kế hoạch của một ngày tăng lên.

---

# 15. BƯỚC TIẾP THEO

**Không phân tích lại từ đầu.**

Tiếp tục từ:

# Domain Model

Cần đào sâu và chốt:

1. `Order` có những thuộc tính nào.
2. `ProductionPlan` là entity hay concept/value object nào.
3. `ProductionRecord` quan hệ với Order thế nào.
4. `Add-on` lưu trực tiếp hay thể hiện thông qua Adjustment.
5. `PlanAdjustment` là entity riêng thế nào.
6. `AdjustmentHistory` có cần tách khỏi Adjustment không.
7. Một ngày có thể có nhiều Order thế nào để hỗ trợ tương lai.
8. Cái gì là source of truth.
9. Cái gì là calculated/derived data.
10. Invariant và constraint cần enforce ở domain/database.
11. Transaction boundary của từng use case.

Sau khi Domain Model được chốt:

> **Domain Model → Data Model → Database Schema → API Contract → Frontend Architecture → Implementation**

Không nhảy thẳng vào code/database schema trước khi Domain Model rõ ràng.

## Câu lệnh tiếp tục

Khi quay lại, bắt đầu bằng:

> **“Tiếp tục thiết kế Domain Model cho Production Management Web App.”**
