# 06 — Implementation Plan — Bản 1 (as-built)

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Trạng thái** | Draft (as-built) — chờ Chốt |
| **Nguồn** | Sinh lại ngày 09/10/2026 từ codebase (commit `74a9fc2`) và lịch sử git. |
| **Role** | Principal Software Engineer + Technical Lead |
| **Spec đầu vào** | `03-feature-specifications.md`, `04-ux-ui-specifications.md`, `05-technical-architecture.md` |

> Bản 1 đã triển khai xong. Spec này ghi lại **các vertical slice đã làm, thứ tự, phụ thuộc, trạng thái và những gì còn nợ**, để Bản 2 kế thừa đúng cách làm thay vì lập kế hoạch từ con số không.

---

## 1. Confirmed Requirements

| Mã | Nội dung |
|---|---|
| IP-C01 | Mọi feature được giao theo **vertical slice**: DB → Domain → Application → API → Frontend (hook, trang, trạng thái loading/error/empty/success) → kiểm tra tay trên UI. |
| IP-C02 | Thứ tự triển khai thực tế (theo git log): nền tảng + Auth → Đơn hàng & kế hoạch (baseline) → Ghi nhận trong ngày & Xuất hàng (CR-01) → Dây chuyền + Nhập hàng + Lập tiến độ hai tầng (CR-001) → chỉnh UI → Chốt tiến độ + chặn ngừng dây chuyền. |
| IP-C03 | Repo **không chứa test tự động**; kiểm chứng bằng build, typecheck, dữ liệu mẫu 11 kịch bản và thao tác tay trên trình duyệt. |
| IP-C04 | Dữ liệu dev được reset và nạp lại bằng `node mock-data/seed.mjs`, không dựng qua API. |
| IP-C05 | Commit do chủ dự án thực hiện; AI không tự commit/tạo nhánh. |

---

## 2. Assumptions

| Mã | Giả định |
|---|---|
| IP-A01 | Môi trường dev chuẩn là Docker (PostgreSQL) + backend/frontend chạy trong container theo `docker-compose.local.yml` (file chưa commit, ngoài phạm vi spec). |
| IP-A02 | Trước khi bắt đầu Bản 2, bộ spec Bản 1 này sẽ được Chốt và trở thành baseline. |

---

## 3. Open Questions

| Mã | Câu hỏi |
|---|---|
| IP-Q01 | Có đưa test tự động (xUnit cho domain/application, Vitest cho hàm chia đều/ngày) vào repo ở Bản 2 không, hay tiếp tục giữ test ngoài repo? |
| IP-Q02 | Có cần CI (build + typecheck) trên GitHub không? Hiện không có workflow nào. |

---

## 4. Decisions

| Mã | Quyết định | Vì sao |
|---|---|---|
| IP-D01 | Mỗi CR được triển khai bằng **migration riêng**, dữ liệu cũ được chuyển đổi (ví dụ `ConfirmProductionSchedule` coi tiến độ cũ là đã chốt) thay vì xoá DB. | Giữ dữ liệu dev liên tục; tập cho việc nâng cấp production sau này. |
| IP-D02 | Bất biến về số được đặt trong Domain để **không thể lách qua endpoint khác**; service chỉ lo khoá và dữ liệu ngoài aggregate. | Một nơi đúng, mọi endpoint đúng. |
| IP-D03 | Endpoint ghi trả về **state đầy đủ** thay vì 204, frontend `setQueryData` + invalidate. | Giảm round-trip và tránh màn hình "lệch nhịp" sau mutation. |
| IP-D04 | Hàm chia đều, định dạng ngày/số, ánh xạ mã lỗi tập trung ở `shared/lib` và `api/errors.ts`. | Một nguồn cho cả app; lệch một đơn vị là người dùng thấy ngay. |
| IP-D05 | Tài liệu cũ (`docs/*.md`) bị **xoá** sau khi sinh bộ spec này; lịch sử còn trong git. | Tránh hai nguồn sự thật. |

---

## 5. Detailed Specification

### 5.1 Vertical slices đã triển khai

| Slice | Phạm vi | DB | Backend | Frontend | Trạng thái | Phụ thuộc |
|---|---|---|---|---|---|---|
| S0 Nền tảng | Solution 4 project, EF Core + Npgsql, migration tự chạy, cookie auth, middleware lỗi, bootstrap tài khoản, Vite + Router + Query, layout, client API, mã lỗi → tiếng Việt | `InitialSchema` | `Program`, `DependencyInjection`, `ExceptionHandlingMiddleware`, `DatabaseInitializer`, `AuthService` | `AppProviders`, `router`, `AppLayout`, `LoginPage`, `client.ts`, `errors.ts` | Done | — |
| S1 Đơn hàng & kế hoạch (baseline) | Tạo đơn kèm kế hoạch, danh sách, chi tiết, giá trị suy ra | `orders`, `production_plans` | `OrderService`, `OrderDerivedCalculator` | danh sách/chi tiết đơn (sau này thành Tiến độ) | Done, **được thay bởi S4** | S0 |
| S2 Ghi nhận trong ngày & Xuất hàng (CR-01) | Nhiều lần ghi nhận/ngày, sửa/xoá mềm, log, xuất hàng, trạng thái ngày, trần còn được nhập, nhắc chu kỳ, cấu hình | `CR01IntradayProductionEntries`, `CR01SimplifySystemSettings` (`production_days`, `production_entries`, `production_entry_logs`, `system_settings`) | `ProductionDayService`, `SettingsService`, `ProductionDayQueries`, `ProductionCalculations` | `ProductionCellDialog`, `EntryQuickForm`, `EntryHistoryTable`, `CloseDayDialog`, `RemainingAllowance`, `DayStatusBadge`, `useRecordingReminder`, `SettingsPage` | Done | S1 |
| S3 Xử lý thiếu | Option 1/2, preview/apply/reverse, lịch sử, chiến lược chia đều | `plan_adjustments`, `plan_adjustment_items` | `AdjustmentService`, `AdjustmentRules`, `EvenDistributionAllocationStrategy` | `ShortageDialog`, `AdjustmentHistory` | Done | S2 |
| S4 Dây chuyền + Nhập hàng + Lập tiến độ hai tầng (CR-001) | Danh mục dây chuyền, mã giày + ảnh, tách Nhập hàng/Lập tiến độ, ma trận ngày × dây chuyền, xuất hàng cả ngày, bù cùng dây chuyền, redirect route cũ | `CR001GoodsReceiptScheduleProductionLines` (`production_lines`, `order_production_lines`, cột ảnh, ngày nullable, `Pending`) | `ProductionLineService`, `ProductionScheduleService`, `OrderImageService`, `OrderImageRules`, `LocalOrderImageStorage`, `EvenDistribution` | `ProductionLinesPage`, `ProductionLineDialog`, `GoodsReceiptListPage`, `GoodsReceiptFormPage`, `ImageUploader`, `ImageLightbox`, `CreateSchedulePage`, `LineAllocationStep`, `PlanMatrixStep`, `ProgressListPage`, `ProgressDetailPage`, `ProductionMatrix`, `Stepper` | Done | S1–S3 |
| S5 Dashboard & thống kê | Số tổng, timeline tháng, danh sách, thống kê đơn theo ngày và theo dây chuyền | — | `StatisticsService` | `DashboardPage`, `TrackedOrders`, `OrderStatisticsPanel` | Done (một phần dữ liệu API chưa hiển thị) | S2–S4 |
| S6 Chỉnh UI | Modal vừa viewport, Select/DateInput tự vẽ, ghim cột ngày, link quay lại, in đậm số liệu, trạng thái lưu trễ 300 ms | — | — | `Modal`, `Select`, `ui.tsx`, CSS | Done | S4 |
| S7 Chốt tiến độ + chặn ngừng dây chuyền | Sửa/xoá tiến độ chưa chốt, chốt một chiều, nhãn Chờ chốt/Chưa sản xuất, khối Chờ chốt trên dashboard, `PRODUCTION_LINE_IN_PRODUCTION` | `ConfirmProductionSchedule` | `Order.Reschedule/Unschedule/ConfirmSchedule`, `ProductionScheduleService.Update/Delete/Confirm`, `OrderQueries.InProductionOn` | `EditSchedulePage`, nút Sửa/Xoá/Chốt, `AwaitingConfirmation`, `OrderStatusBadge` | Done | S4, S5 |

### 5.2 Thứ tự khuyến nghị khi làm lại từ đầu (cho Bản 2 tham khảo)

1. S0 nền tảng → 2. Dây chuyền (danh mục độc lập) → 3. Nhập hàng (đơn `Pending` + ảnh) → 4. Lập/sửa/xoá/chốt tiến độ → 5. Ghi nhận + Xuất hàng → 6. Xử lý thiếu → 7. Danh sách/chi tiết tiến độ + thống kê → 8. Dashboard → 9. Cấu hình.

Lý do: mỗi bước chỉ phụ thuộc bước trước và tự test được từ UI ngay khi xong.

### 5.3 Technical tasks mẫu cho một slice (checklist đã dùng)

1. Migration + `IEntityTypeConfiguration` (tên snake_case, CHECK, index, FK Restrict).
2. Entity/aggregate: bất biến số + mã lỗi mới trong `ErrorCodes`.
3. Service application: khoá dòng đúng thứ tự, guard chung (`OrderMutationGuard`, scheduled/confirmed), DTO đầy đủ.
4. Controller: route theo `05` §5.5, trả state đầy đủ.
5. Frontend: `types.ts` khớp DTO → `api/*Api.ts` → hook (`useQuery`/`useMutation` + invalidate đúng prefix) → component/page với 4 trạng thái → thêm câu chữ mã lỗi mới vào `errors.ts`.
6. Cập nhật `mock-data/scenarios.json` nếu cần trạng thái UI mới.
7. Build + typecheck + kiểm tra tay trên `localhost:5173`.

### 5.4 Testing strategy (as-built)

| Lớp | Cách kiểm | Trạng thái |
|---|---|---|
| Build | `dotnet build`; `npm run typecheck && npm run build` | Chạy mỗi lần thay đổi |
| Dữ liệu mẫu | 11 kịch bản trong `scenarios.json` phủ: đúng tiến độ, chậm, đã bù Option 1, bù tự động + hoàn tác, hoàn thành, quá hạn, chưa bắt đầu, có ngày nghỉ, chưa lập tiến độ, chờ chốt, chờ chốt quá hạn | Có |
| UI | Tự mở trình duyệt kiểm tra luồng sau mỗi thay đổi UI | Thực hiện tay |
| Unit/integration | Không có trong repo; test tạm (nếu có) tạo ngoài repo và xoá sau | Theo quyết định chủ dự án |

Các điểm **bắt buộc kiểm tay** trước khi nói "xong" (rút từ các bug đã gặp): ô mở không hiện thiếu; chia đều hai phía cho cùng dãy số; Enter trong form ghi nhận không chốt sổ; modal vừa viewport; hôm nay không bị tính chậm; đơn quá hạn ẩn hết nút ghi; sửa tiến độ giữ nguyên ma trận khi không đổi gì.

### 5.5 Definition of Done (đã áp dụng)

- [ ] Migration áp được trên DB có dữ liệu cũ.
- [ ] Bất biến nằm trong Domain, có mã lỗi và câu tiếng Việt.
- [ ] Endpoint trả state đầy đủ; lỗi theo khuôn `{code, message, details}`.
- [ ] Frontend có loading / error / empty / success; số liệu in đậm trong câu chú thích.
- [ ] Mutation invalidate đúng key; không optimistic update cho sản xuất.
- [ ] Build + typecheck xanh.
- [ ] Kiểm tra tay luồng trên trình duyệt với dữ liệu mẫu.
- [ ] Không thay đổi ngoài yêu cầu; không doc mới ngoài bộ spec.

### 5.6 Rủi ro kỹ thuật đã nhận diện

| Rủi ro | Mức | Biện pháp hiện có |
|---|---|---|
| Lệch "hôm nay" giữa trình duyệt và backend | Trung bình | Dashboard dùng ngày backend; các màn khác chưa (xem `05` TA-Q01) |
| Dashboard nạp toàn bộ dữ liệu | Thấp (quy mô nhỏ) | Chưa có; theo dõi khi số đơn tăng |
| Mất phiên đăng nhập khi tạo lại container | Thấp | `DataProtection__KeysPath` |
| File ảnh mồ côi | Thấp | Log Warning; chưa có job dọn |
| Double-submit ghi nhận | Thấp | Khoá nút khi pending; ô còn mở nên xoá được nếu lọt |

### 5.7 Technical debt còn lại

| # | Mục | Vị trí | Đề xuất |
|---|---|---|---|
| TD-01 | `AdjustmentRecalculationDto` / `AdjustmentRecalculationOutcome` không còn được dùng | `Application/Contracts/AdjustmentContracts.cs` | Xoá |
| TD-02 | Mã lỗi `INITIAL_PLAN_TOTAL_MISMATCH` không còn được ném | `ErrorCodes.cs`, `errors.ts` | Xoá |
| TD-03 | `shared/components/StatusBadges.tsx` có `OrderStatusBadge` hai trạng thái **không dùng** (trùng tên với bản 6 trạng thái ở `features/orders`) | frontend | Xoá bản cũ |
| TD-04 | API dashboard trả `alerts`, `today`, `todayProduction`, `unclosedPastCells`, `openShortages` nhưng UI không render; `OrderListItemDto.todayPlanned/Actual` không hiển thị | `DashboardPage`, `ProgressListPage` | Quyết định ở Bản 2: hiển thị hoặc bỏ khỏi API |
| TD-05 | `react-hook-form` + `zod` chỉ dùng ở màn đăng nhập; các form khác tự quản lý state | frontend | Thống nhất một cách |
| TD-06 | `README.md` nằm trong `.gitignore` và mô tả API cũ (`production-records`) | repo | Viết lại hoặc bỏ hẳn |
| TD-07 | `docker-compose.yml` nhắc `.env.example` không tồn tại | repo | Tạo file mẫu không chứa secret |
| TD-08 | `today()` frontend theo giờ trình duyệt | `shared/lib/date.ts` | Lấy từ backend (xem TA-Q01) |

---

## 6. Acceptance Criteria

| Mã | Tiêu chí | Trạng thái |
|---|---|---|
| IP-AC01 | 7 slice ở §5.1 đều có đủ DB/Backend/Frontend và dùng được từ UI. | Implemented |
| IP-AC02 | `dotnet build` và `npm run typecheck && npm run build` thành công tại commit `74a9fc2`. | Verified theo README; cần chạy lại khi Chốt |
| IP-AC03 | `node mock-data/seed.mjs` nạp đủ 11 kịch bản và mọi màn hình hiển thị đúng trạng thái mô tả trong `scenarios.json`. | Verified bởi chủ dự án trong quá trình phát triển |
| IP-AC04 | Bộ spec `docs/specs/v1/` (`01` → `06`) phản ánh đúng code; mọi điểm lệch giữa code và tài liệu cũ được ghi trong mục Decisions của `03`. | Implemented |
