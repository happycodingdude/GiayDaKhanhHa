# 03 — Feature Specifications — Bản 1

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Trạng thái** | Draft (as-built) — chờ Chốt |
| **Nguồn** | Sinh lại ngày 09/10/2026 từ codebase (commit `74a9fc2`). Mọi luật dưới đây là luật **đang được code thực thi**; tài liệu cũ chỉ dùng để ghi lý do. |
| **Role** | Principal Business Analyst + Domain Specialist |
| **Spec đầu vào** | `01-business-discovery.md`, `02-product-requirements.md` |

Quy ước mã: `BR-xx` business rule · `F-xx` feature · `AC-xx` acceptance criteria. Mã lỗi API viết `IN_HOA`.

---

## 1. Confirmed Requirements

Tất cả 10 feature F-01 … F-10 (theo `02` §5.1) và toàn bộ business rule ở §5 là **Confirmed** vì đã có trong code. Bảng dưới gom các luật xuyên suốt; luật riêng từng feature nằm ở §5.

| Mã | Luật xuyên suốt |
|---|---|
| BR-00 | Mọi thao tác ghi đều yêu cầu đăng nhập; người thực hiện được ghi vào audit từ phiên đăng nhập, không bao giờ từ dữ liệu client gửi. |
| BR-01 | **Đóng băng khi quá ngày kết thúc**: đơn có `dueDate < hôm nay` (kể cả đã Hoàn thành) từ chối mọi thao tác ghi với `422 ORDER_OVERDUE`. Ngoại lệ duy nhất: **sửa** và **xoá** tiến độ **chưa chốt**. Chốt tiến độ của đơn quá hạn cũng bị chặn. |
| BR-02 | **Hôm nay** là ngày lịch theo múi giờ `Asia/Ho_Chi_Minh`, do backend quyết định. |
| BR-03 | Mọi giá trị tổng hợp (tổng thực tế, còn lại, %, chậm, thiếu, trạng thái ngày) là **suy ra**, không lưu, không nhập tay. |
| BR-04 | **Chia đều**: `base = tổng div n`, `dư = tổng mod n`, `dư` phần tử **đầu tiên** nhận `base + 1`. Thứ tự phần tử: dây chuyền theo `sortOrder` rồi `code`; ngày tăng dần. Dùng chung cho tầng 1, tầng 2 và Option 2. Ví dụ: 1000/3 → 334, 333, 333; 10/4 → 3, 3, 2, 2; 23/4 → 6, 6, 6, 5. |
| BR-05 | Trạng thái đơn chỉ có `Pending` (Chưa lập tiến độ) / `Incomplete` (Chưa hoàn thành) / `Completed` (Hoàn thành). Chỉ Lập tiến độ đưa đơn ra khỏi `Pending`; chỉ Xoá tiến độ chưa chốt đưa đơn về `Pending`; chỉ Xuất hàng đánh giá `Incomplete` ↔ `Completed`. |
| BR-06 | Nhãn hiển thị của đơn suy ra theo thứ tự ưu tiên: `Pending` → "Chưa lập tiến độ"; `Completed` → "Hoàn thành"; chưa chốt → "Chờ chốt"; quá hạn → "Quá hạn"; chưa tới ngày bắt đầu → "Chưa sản xuất"; còn lại → "Đang sản xuất". |

---

## 2. Assumptions

| Mã | Giả định |
|---|---|
| FS-A01 | Luật "ô kế hoạch 0 không có dòng kế hoạch, nên **không thể nhận bù**" là chủ đích (ô nghỉ không được dùng để bù). Code hành xử như vậy; tài liệu cũ mơ hồ. |
| FS-A02 | `ORDER_ALREADY_COMPLETED` trả **409** khi ghi nhận/sửa lần ghi nhận và **422** khi xem trước/áp dụng bù là chủ đích (hai ngữ cảnh khác nhau), không phải lỗi. |
| FS-A03 | Kiểu lỗi của `AdjustmentRecalculationDto` (tính lại khoản bù khi sửa sản lượng) còn trong contract nhưng **không được dùng** ở bất kỳ luồng nào; coi là dead code, không phải tính năng. |

---

## 3. Open Questions

| Mã | Câu hỏi |
|---|---|
| FS-Q01 | Có cần cho phép **bù vào ô nghỉ** (kế hoạch 0) không? Hiện không thể vì ô đó không có dòng kế hoạch. |
| FS-Q02 | Đơn **Hoàn thành** vẫn còn ô chưa xuất hàng: hiện vẫn Xuất hàng được (để dọn cảnh báo) nhưng phần thiếu sinh ra không xử lý được. Có đúng ý không? |
| FS-Q03 | Có cần cho sửa **ngày bắt đầu/kết thúc** sau khi chốt (gia hạn) không? Hiện không có. |

---

## 4. Decisions

Các luật có trong code nhưng **không có hoặc khác** tài liệu cũ. Code là quyết định hiện hành.

| Mã | Quyết định trong code | Tài liệu cũ nói gì | Lý do giữ theo code |
|---|---|---|---|
| FS-D01 | Có bước **Chốt tiến độ** (`scheduleConfirmedAt`); tiến độ chưa chốt sửa/xoá được; chưa chốt thì không ghi nhận/xuất hàng được (`SCHEDULE_NOT_CONFIRMED`). | CR-001 BR-N05: "lập tiến độ là thao tác một lần", không có chốt. | Yêu cầu bổ sung ở commit `74a9fc2`; tránh sản xuất theo kế hoạch còn sửa. |
| FS-D02 | **Không ngừng được dây chuyền đang có đơn "Đang sản xuất"** (`409 PRODUCTION_LINE_IN_PRODUCTION`). | CR-001 §6.3: ngừng luôn được phép. | Đơn đang chạy không được mất chỗ sản xuất. |
| FS-D03 | **Xuất hàng chốt cả ngày** (mọi dây chuyền có kế hoạch của đơn trong ngày đó), không có endpoint chốt từng dây chuyền. | CR-001 §4.4: xuất hàng theo từng ô. | Một thao tác cuối ngày; ngày đã xuất hàng là trạng thái của ngày. |
| FS-D04 | Đơn **quá ngày kết thúc bị đóng băng** kể cả đã hoàn thành; trừ sửa/xoá tiến độ chưa chốt. | CR-001 BR-N20 nhắc "luật đóng băng của baseline" nhưng baseline không định nghĩa. | Yếu tố quyết định là lịch, không phải trạng thái. |
| FS-D05 | Hoàn thành khi `tổng thực tế >= số lượng đơn` (thực tế luôn `==` do trần). | Baseline dùng `==`. | Tương đương; `>=` an toàn hơn. |
| FS-D06 | **Chậm tiến độ** tính trên các ô **đã tới hạn**: ngày < hôm nay, hoặc ngày = hôm nay **và ô đó đã xuất hàng**; chỉ khi tiến độ đã chốt. | Không nói rõ hôm nay có tính không. | Nếu tính cả hôm nay, sáng nào mọi đơn cũng "chậm". |
| FS-D07 | `daysRemaining = max(dueDate − hôm nay, 0)`. | Không có công thức. | — |
| FS-D08 | **Không** có luồng tự tính lại khoản bù khi sửa sản lượng (Option B cũ). | S1/S4 có; CR-01 §3.1 gỡ. | Sản lượng ngày đã xuất hàng bất biến nên phần thiếu không đổi. |
| FS-D09 | Cấu hình chỉ có `recordingIntervalMinutes` (5–480) và `remindBeforeDue`; **không có** giờ bắt đầu/kết thúc ca. | CR-01 có `dayStartTime/dayEndTime`. | Migration `CR01SimplifySystemSettings` đã bỏ; không nghiệp vụ nào dùng. |
| FS-D10 | Bộ lọc danh sách đơn có thêm `InProduction` và `Overdue` (ngoài `Pending/Incomplete/Completed/Scheduled/All`). | CR-001 chỉ có `Scheduled`. | Tab "Đang sản xuất" không được gồm đơn quá hạn. |
| FS-D11 | Thao tác trên ma trận đặt **trong từng ô** (Nhập SL / Xem / Xử lý thiếu) và cột Xuất hàng theo ngày; không có "danh sách ô còn việc" dưới ma trận. | CR-001 §7.8 mô tả danh sách riêng. | Chỉnh UI ở các commit sau CR-001. |
| FS-D12 | `lastRecordedBy`, `recordedBy`, `closedBy`, `createdBy` trong DTO là **tên hiển thị** người dùng. | Không rõ username hay display name. | — |
| FS-D13 | `isEdited` của lần ghi nhận = `updatedAt ≠ createdAt`. | Không mô tả. | — |
| FS-D14 | Tổng kế hoạch sau bù **được phép** vượt số lượng đơn. | MH4 cũ nói không; Master/S3 nói có. | Giữ bất biến nghiệp vụ: chỉ tổng thực tế bị chặn. |
| FS-D15 | Bù không tạo được nếu ô nguồn **đã có** khoản bù đang áp dụng, kể cả khi phần thiếu còn nguyên (`409 ACTIVE_ADJUSTMENT_EXISTS`); phải hoàn tác trước. | Khớp S4 §12. | Chống áp dụng trùng. |

---

## 5. Detailed Specification

### F-01 Đăng nhập / phiên làm việc

**User flow**: `/login` → nhập username, mật khẩu → cookie `pm.auth` → `/dashboard`. Mọi trang khác kiểm tra phiên trước khi render; hết phiên → `/login`. Đăng xuất xoá cookie và cache.

| Luật | Nội dung |
|---|---|
| BR-F01-01 | Username và mật khẩu bắt buộc (`400 VALIDATION_ERROR`, field `username`/`password`, code `REQUIRED`). |
| BR-F01-02 | Sai username hoặc sai mật khẩu đều trả `401 INVALID_CREDENTIALS` với cùng câu thông báo. |
| BR-F01-03 | Tài khoản `Inactive` → `403 USER_INACTIVE` (cả lúc đăng nhập lẫn khi gọi `/auth/me`). |
| BR-F01-04 | Phiên 12 giờ, gia hạn trượt theo mỗi request. |
| BR-F01-05 | Tài khoản đầu tiên do hệ thống tạo lúc khởi động (xem `05` §5.6). Không có màn hình quản lý người dùng. |

### F-02 Danh mục dây chuyền

**User flow**: Cấu hình → Dây chuyền sản xuất → Thêm / Sửa (dialog) / Ngừng / Bật lại (xác nhận).

| Luật | Nội dung |
|---|---|
| BR-F02-01 | Mã bắt buộc, ≤ 30 ký tự, **duy nhất** (`409 PRODUCTION_LINE_CODE_ALREADY_EXISTS`); tên bắt buộc ≤ 100; thứ tự ≥ 0; ghi chú ≤ 500. Mã/tên/ghi chú được trim. |
| BR-F02-02 | Dây chuyền mới luôn `Active`. |
| BR-F02-03 | **Không có thao tác xoá.** Chỉ `Active` ↔ `Inactive`. |
| BR-F02-04 | Chuyển `Inactive` bị chặn `409 PRODUCTION_LINE_IN_PRODUCTION` khi dây chuyền thuộc ít nhất một đơn **Đang sản xuất** (`Incomplete` + đã chốt + `startDate ≤ hôm nay ≤ dueDate`). Thông báo liệt kê mã giày các đơn đó. |
| BR-F02-05 | `Inactive` không xuất hiện trong lựa chọn lập tiến độ; dữ liệu lịch sử vẫn hiển thị. Sửa tiến độ chưa chốt có dây chuyền đã ngừng → dây chuyền đó bị bỏ, phải phân bổ lại. |
| BR-F02-06 | `sortOrder` quyết định thứ tự hiển thị **và** thứ tự nhận phần dư khi chia đều tầng 1. |
| BR-F02-07 | DTO trả `inUse` (đã gán cho ít nhất một đơn) và `inProductionOrderCount`. |

**Edge case**: hai request tạo cùng mã → unique index, request sau nhận 409.

### F-03 Nhập hàng

**User flow**: Nhập hàng → + Nhập hàng → mã giày, số lượng, ảnh → tạo → sang màn sửa. Sửa: đổi mã/số lượng/ảnh, lưu một lần. Xoá: chỉ đơn Pending.

| Luật | Nội dung |
|---|---|
| BR-F03-01 | Mã giày bắt buộc, ≤ 50, trim, **duy nhất** (`409 SHOE_CODE_ALREADY_EXISTS`, kiểm trước và unique index). |
| BR-F03-02 | Số lượng số nguyên > 0 (`400 VALIDATION_ERROR` `MUST_BE_GREATER_THAN_ZERO`). |
| BR-F03-03 | Đơn mới: `Pending`, không ngày, không dây chuyền, không kế hoạch. |
| BR-F03-04 | Ảnh: tối đa **một**; JPG/PNG/WEBP nhận diện bằng **chữ ký byte** (`400 IMAGE_TYPE_NOT_SUPPORTED`); ≤ 5 MB (`400 IMAGE_TOO_LARGE`); file rỗng → `REQUIRED`. Ảnh mới thay ảnh cũ. |
| BR-F03-05 | Sửa: mã giày và ảnh đổi được ở mọi trạng thái (trừ quá hạn — BR-01); **số lượng chỉ đổi được khi `Pending`** (`422 ORDER_SCHEDULE_ALREADY_EXISTS`). |
| BR-F03-06 | Sửa gửi `image` và `removeImage=true` cùng lúc → `400` field `removeImage` code `CONFLICTS_WITH_IMAGE`. Gỡ ảnh khi đơn không có ảnh là no-op (idempotent). |
| BR-F03-07 | Xoá: chỉ `Pending` (`422 ORDER_NOT_DELETABLE`); xoá cứng kèm file ảnh; khoá dòng đơn trước khi kiểm để không đua với lập tiến độ. |
| BR-F03-08 | Thứ tự ghi ảnh: ghi file mới → commit DB → xoá file cũ. Commit hỏng → xoá file mới. |
| BR-F03-09 | Ảnh phục vụ qua endpoint có xác thực; `imageUrl` = `/api/v1/orders/{id}/image/content`; file mất trên disk → `404 IMAGE_NOT_FOUND`. |

**Danh sách** (`GET /orders`): lọc `status` ∈ {All, Pending, Incomplete, InProduction, Overdue, Completed, Scheduled} (giá trị khác → 400); tìm mã giày không phân biệt hoa thường (contains); lọc `productionLineId`; `page ≥ 1`, `pageSize` 1–200 (ngoài khoảng → 20); sắp theo `createdAt` giảm dần. Mỗi dòng kèm giá trị suy ra (§5.8).

### F-04 Lập / sửa / xoá / chốt tiến độ

**User flow**: Tiến độ → + Lập tiến độ (hoặc nút Lập tiến độ ở Nhập hàng) → wizard 5 bước → Tạo. Chi tiết tiến độ (chưa chốt): Sửa tiến độ (wizard điền sẵn) / Xoá tiến độ / Chốt tiến độ.

Request: `startDate`, `dueDate`, `allocationMode` (`Even`|`Manual`, chỉ khai báo), `lines[] { productionLineId, allocatedQuantity, plans[] { productionDate, plannedQuantity } }`.

| Luật | Nội dung |
|---|---|
| BR-F04-01 | Chỉ lập cho đơn `Pending`; đã có tiến độ → `409 ORDER_SCHEDULE_ALREADY_EXISTS`. |
| BR-F04-02 | Validation 400 (gom nhiều lỗi): `lines` ≥ 1 (`REQUIRED`); `dueDate ≥ startDate` (`DUE_DATE_BEFORE_START_DATE`); không trùng dây chuyền (`DUPLICATE_PRODUCTION_LINE`); `plannedQuantity ≥ 0`; mọi `productionDate` trong `[startDate, dueDate]` (`OUT_OF_PRODUCTION_PERIOD`); không trùng ngày trong một dây chuyền (`DUPLICATE_PRODUCTION_DATE`); `allocationMode` nếu có phải hợp lệ (`INVALID_VALUE`). |
| BR-F04-03 | Dây chuyền phải tồn tại (`404 PRODUCTION_LINE_NOT_FOUND`) và `Active` (`422 PRODUCTION_LINE_INACTIVE`, details liệt kê mã). |
| BR-F04-04 | Mỗi dây chuyền được chọn phải có `allocatedQuantity > 0` (`422 PRODUCTION_LINE_EMPTY_ALLOCATION`). |
| BR-F04-05 | **Tầng 1**: Σ `allocatedQuantity` = số lượng đơn (`422 LINE_ALLOCATION_MISMATCH`). |
| BR-F04-06 | **Tầng 2**: với mỗi dây chuyền, Σ `plannedQuantity` = `allocatedQuantity` của nó (`422 LINE_PLAN_TOTAL_MISMATCH`, details: `lineId → "tổng/mốc"`). Hệ quả: Σ kế hoạch ban đầu toàn đơn = số lượng đơn. |
| BR-F04-07 | Ô `plannedQuantity = 0` **không tạo dòng kế hoạch** (dây chuyền nghỉ ngày đó); ô không gửi = 0. |
| BR-F04-08 | Backend **luôn validate con số thật**, không tự tính lại theo `allocationMode`; mode không lưu DB. |
| BR-F04-09 | Thành công: `startDate`, `dueDate` được gán; trạng thái `Incomplete`; `scheduleConfirmedAt = null` (**chờ chốt**); mỗi dây chuyền một dòng `order_production_lines` với `allocatedQuantity` bất biến sau chốt. |
| BR-F04-10 | **Sửa tiến độ** (`PUT`): chỉ khi đã lập (`422 ORDER_NOT_SCHEDULED`) và **chưa chốt** (`409 SCHEDULE_ALREADY_CONFIRMED`); validate y hệt lúc lập; bộ mới thay toàn bộ bộ cũ (dòng còn thì cập nhật tại chỗ, dòng mất thì xoá); kế hoạch ban đầu được đặt lại. **Không** bị chặn bởi BR-01. |
| BR-F04-11 | **Xoá tiến độ** (`DELETE`): điều kiện như sửa; xoá mọi dây chuyền và kế hoạch; đơn về `Pending`, ngày về null; giữ mã, số lượng, ảnh. **Không** bị chặn bởi BR-01. |
| BR-F04-12 | **Chốt tiến độ** (`POST …/confirm`): điều kiện như sửa; **bị chặn** bởi BR-01 (`422 ORDER_OVERDUE`); đặt `scheduleConfirmedAt = now`; một chiều, không có bỏ chốt. |
| BR-F04-13 | Tiến độ chưa chốt: không có sản lượng, không có khoản bù; không được tính chậm; không xuất hiện trong timeline dashboard; nằm ở khối "Chờ chốt tiến độ". |

**Wizard (client)**: bước 3 điền sẵn chia đều (BR-04); bước 4 khởi tạo bằng chia đều phân bổ của từng dây chuyền cho các ngày; chỉ đi tiếp khi mọi cột khớp. Nháp lưu `sessionStorage`.

**Edge case**: lập tiến độ với `startDate` trong quá khứ hợp lệ (cho phép lập muộn); đơn chưa chốt mà `dueDate` đã qua: không chốt được, phải sửa dời ngày hoặc xoá.

### F-05 Ghi nhận sản lượng trong ngày

**User flow**: Chi tiết tiến độ → ô đang sản xuất → Nhập SL → modal → nhập số (+ ghi chú) → Enter/Ghi nhận; sửa/xoá lần ghi nhận trong bảng lịch sử.

Khoá ô: `(orderId, productionDate, productionLineId)`. Dòng `production_days` tạo **lazily** ở lần ghi nhận đầu hoặc lúc xuất hàng.

| Luật | Nội dung |
|---|---|
| BR-F05-01 | Thứ tự kiểm tra chung cho mọi thao tác ghi vào ô: đơn tồn tại (404) → đã lập tiến độ (`422 ORDER_NOT_SCHEDULED`) → đã chốt (`422 SCHEDULE_NOT_CONFIRMED`) → không quá hạn (BR-01) → ngày không ở tương lai (`422 FUTURE_DATE_NOT_ALLOWED`) → ô có kế hoạch > 0 (`422 DAY_HAS_NO_PLAN`) → ô còn mở (`409 DAY_ALREADY_CLOSED`). |
| BR-F05-02 | Ghi nhận/sửa: đơn chưa Hoàn thành (`409 ORDER_ALREADY_COMPLETED`), kiểm **trước** các trần số lượng. |
| BR-F05-03 | `quantity` số nguyên **> 0** (`400 MUST_BE_GREATER_THAN_ZERO`); ghi nhận 0 là vô nghĩa. `note` trim, ≤ 255, rỗng → null. |
| BR-F05-04 | **Trần ô**: Σ lần ghi nhận chưa xoá của ô ≤ kế hoạch hiện tại của ô (`422 ENTRY_EXCEEDS_DAILY_PLAN`, details `quantity: MAX_ALLOWED = trần`). |
| BR-F05-05 | **Trần đơn**: Σ lần ghi nhận chưa xoá **toàn đơn, kể cả ô còn mở** ≤ số lượng đơn (`422 ACTUAL_EXCEEDS_ORDER_QUANTITY`, details `MAX_ALLOWED`). Trần nào chặt hơn thì báo lỗi đó. |
| BR-F05-06 | `remainingAllowance = max(min(kế hoạch ô − đã ghi nhận ô, số lượng đơn − tổng thực tế), 0)`; ô đã đóng → 0. Kèm `remainingAllowanceReason` = `DailyPlan` / `OrderQuantity`. |
| BR-F05-07 | Sửa: validate với `ô − cũ + mới` và `đơn − cũ + mới` (sửa xuống không bao giờ bị chặn nhầm). |
| BR-F05-08 | Xoá là **xoá mềm** (`deletedAt`); mọi phép tổng bỏ dòng đã xoá; xoá hai lần là no-op. |
| BR-F05-09 | Mọi tạo/sửa/xoá ghi một dòng `production_entry_logs` (cũ/mới số lượng và ghi chú, người, thời điểm). **Không bắt buộc lý do.** |
| BR-F05-10 | Ngày **đã qua** mà chưa xuất hàng: vẫn ghi nhận được (có cảnh báo trên UI), không tự đóng. |
| BR-F05-11 | Thao tác trên ô được **tuần tự hoá** bằng khoá dòng đơn rồi dòng ô; chống bấm trùng ở client bằng khoá nút khi đang gửi (bảng lần ghi nhận không có unique). |
| BR-F05-12 | Nhắc chu kỳ: thuần client, tính từ `lastRecordedAt`; quá chu kỳ → nhắc; bật `remindBeforeDue` → nhắc trước 15 phút. **Không bao giờ chặn.** |
| BR-F05-13 | Response của mọi thao tác là state đầy đủ của ô: kế hoạch (ban đầu/hiện tại/bù), đã nhập, tạm tính, trần, danh sách lần ghi nhận **mới nhất trên cùng** kèm `runningTotal` tính theo thời gian, `isEdited`, người ghi. |

### F-06 Xuất hàng (chốt sổ cả ngày)

**User flow**: Chi tiết tiến độ → cột Xuất hàng của ngày → dialog liệt kê mọi dây chuyền còn mở và mọi lần ghi nhận → Xác nhận.

| Luật | Nội dung |
|---|---|
| BR-F06-01 | Áp dụng BR-F05-01 ở mức đơn/ngày (không cần ô). Ngày không có ô nào kế hoạch > 0 → `422 DAY_HAS_NO_PLAN`. |
| BR-F06-02 | Chốt **mọi ô có kế hoạch > 0 còn mở** của đơn trong ngày đó, trong **một transaction**: hoặc tất cả, hoặc không gì. Ô đã đóng từ trước bị bỏ qua. Không còn ô mở nào → `409 DAY_ALREADY_CLOSED`. |
| BR-F06-03 | Sản lượng chính thức của ô = Σ lần ghi nhận chưa xoá, **do server tính**; client không gửi số. Ô chưa ghi nhận gì → tạo dòng ô và đóng với 0 (thiếu = kế hoạch). |
| BR-F06-04 | Ô đóng ghi `status = Closed`, `actualQuantity`, `closedAt = now` (không backdate), `closedBy`. Ô đã đóng **bất biến**: không sửa/xoá lần ghi nhận, không mở lại. |
| BR-F06-05 | Đây là **thời điểm duy nhất** đánh giá trạng thái đơn: Σ thực tế toàn đơn ≥ số lượng → `Completed`, ngược lại `Incomplete` (cùng transaction). |
| BR-F06-06 | Đơn **đã Hoàn thành** vẫn xuất hàng được các ngày còn treo (để dọn cảnh báo); khi đó `hasShortage = false` dù có ô thiếu. |
| BR-F06-07 | Response: danh sách ô vừa đóng (dây chuyền, kế hoạch, thực tế, thiếu, chênh lệch) theo thứ tự cột, trạng thái đơn, `orderCompleted`, `hasShortage`. |

### F-07 Xử lý sản lượng thiếu

**User flow**: ô đã xuất hàng có thiếu → Xử lý thiếu → chọn phương thức → (Option 1: chọn ngày) → Xem trước → Xác nhận. Lịch sử bù → Hoàn tác.

Thuật ngữ: **ô nguồn** = ô đã đóng có thiếu; **ô đích** = kế hoạch nhận bù.

| Luật | Nội dung |
|---|---|
| BR-F07-01 | Phần thiếu của ô nguồn = `max(kế hoạch hiện tại − thực tế đã chốt, 0)`; ô chưa đóng → `422 SOURCE_DAY_NOT_CLOSED`; thiếu = 0 → `422 NO_SHORTAGE`. |
| BR-F07-02 | Đơn quá hạn → `422 ORDER_OVERDUE` (cả xem trước); đơn Hoàn thành → `422 ORDER_ALREADY_COMPLETED`. |
| BR-F07-03 | Ô nguồn đã có khoản bù `Applied` → `409 ACTIVE_ADJUSTMENT_EXISTS`. |
| BR-F07-04 | **Ô đích hợp lệ**: cùng đơn, **cùng dây chuyền** với ô nguồn, khác ô nguồn, `ngày > ngày nguồn`, `ngày ≥ hôm nay`, **chưa xuất hàng**. Không còn ô nào → `422 NO_ELIGIBLE_TARGET_DAY`. |
| BR-F07-05 | **Option 1 (`Manual`)**: client gửi đúng **một** ô đích với `addOnQuantity = toàn bộ phần thiếu` (UI không cho nhập số). Server kiểm từng đích; lỗi được nói đúng nguyên nhân theo thứ tự: khác dây chuyền (`422 ADJUSTMENT_TARGET_LINE_MISMATCH`) → không sau ngày thiếu (`422 INVALID_ADJUSTMENT_TARGET`) → đã đóng (`422 TARGET_DAY_CLOSED`) → quá khứ (`422 TARGET_DATE_IN_PAST`); không thuộc đơn → `INVALID_ADJUSTMENT_TARGET`; trùng đích → `422 DUPLICATE_ADJUSTMENT_TARGET`; `addOn ≤ 0` → `INVALID_ADJUSTMENT_TARGET`; Σ addOn ≠ thiếu → `422 ADJUSTMENT_TOTAL_MISMATCH`. |
| BR-F07-06 | **Option 2 (`Automatic`)**: server chia đều phần thiếu cho **mọi** ô đích hợp lệ theo BR-04 (ngày tăng dần); ô nhận 0 không nằm trong đề xuất. Không cho chỉnh. |
| BR-F07-07 | **Xem trước** không lưu gì; trả `items` (ô, ngày, dây chuyền, hiện tại, bù thêm, sau khi bù), `totalAddOnQuantity`, `valid` + `validationCode/Message` (Option 1 không hợp lệ vẫn trả 200 với `valid=false`). |
| BR-F07-08 | **Áp dụng**: client gửi lại `adjustmentType`, `shortageQuantity` đã xem, `targets`. Server khoá đơn rồi khoá các kế hoạch (theo id), **tính lại** phần thiếu từ DB; lệch hoặc = 0 → `409 ADJUSTMENT_OUTDATED`. Với Automatic, tính lại đề xuất và so khớp **chính xác** với client gửi; lệch → `409 ADJUSTMENT_OUTDATED`. Server không bao giờ âm thầm thay đề xuất. |
| BR-F07-09 | Áp dụng tạo `plan_adjustments` (`Applied`, `createdBy = appliedBy`) + items, và **cộng** `addOn` vào `plannedQuantity` của từng ô đích. Không giảm ô nào. Kế hoạch ban đầu không đổi. Trần "còn được nhập" của ô đích tăng theo. |
| BR-F07-10 | **Hoàn tác**: chỉ `Applied` → `Reversed` (`409 ADJUSTMENT_NOT_APPLIED` nếu không); bị BR-01 chặn; trừ `addOn` khỏi từng ô đích (không được làm kế hoạch âm → `409 ADJUSTMENT_OUTDATED`); ghi `reversedBy/At`. Không hoàn tác hai lần, không sửa bản ghi. |
| BR-F07-11 | Lịch sử bù của đơn: mọi khoản bù (Applied và Reversed), mới nhất trên cùng, kèm dây chuyền, ngày nguồn, danh sách ngày đích, người và thời điểm tạo/áp dụng/hoàn tác. |

**Edge case**: ô thiếu là ngày cuối của dây chuyền → không có ô đích → báo `NO_ELIGIBLE_TARGET_DAY`, nút Xử lý thiếu vẫn hiện nhưng dialog cảnh báo. Ô nghỉ (kế hoạch 0) không nhận bù (FS-A01).

### F-08 Theo dõi tiến độ

**Danh sách Tiến độ**: lọc mặc định `Scheduled` (Incomplete + Completed); lọc thêm dây chuyền; cột gồm số ngày đã qua chưa xuất hàng (đếm theo **ngày**, chỉ đơn `Incomplete` đã chốt).

**Chi tiết**: tổng quan (số lượng, đã hoàn thành, còn lại, ngày bắt đầu/kết thúc, còn n ngày / quá hạn, %, trạng thái, tình trạng) + ma trận + lịch sử bù + thống kê.

**Ma trận** (`GET /orders/{id}/production-plans`): trả phẳng theo ô; mỗi ô: `initialPlannedQuantity`, `addOnQuantity = planned − initial`, `plannedQuantity`, `dayStatus`, `actualQuantity` (null = chưa ghi nhận), `isProvisional`, `shortageQuantity`/`difference` (chỉ khi đã đóng, else null), `closedAt`, `hasActiveAdjustment`, `activeAdjustmentId`, `lastRecordedBy/At`; mỗi dây chuyền: `allocatedQuantity` (mốc), `currentPlanQuantity` (có thể > mốc sau bù), `actualQuantity`.

| Luật | Nội dung |
|---|---|
| BR-F08-01 | **Trạng thái ngày** (server tính, thứ tự xét): kế hoạch = 0 → `NoPlan`; ngày > hôm nay → `NotStarted`; đã đóng → `Closed`; còn lại → `InProduction`. |
| BR-F08-02 | **Thiếu / chênh lệch** chỉ tồn tại ở ô đã đóng: `thiếu = max(KH − TT, 0)`, `chênh lệch = TT − KH` (≤ 0). Ô mở → `null`, **không phải 0**. |
| BR-F08-03 | **Tổng thực tế** = Σ mọi lần ghi nhận chưa xoá, **gồm ô mở** (tạm tính). `còn lại = max(số lượng − tổng thực tế, 0)`; `% = round1(tổng thực tế × 100 / số lượng)`. |
| BR-F08-04 | **Chậm** (`behindQuantity`) = `max(Σ KH các ô đã tới hạn − Σ TT các ô đó, 0)` với "đã tới hạn" theo FS-D06; `scheduleStatus` = `Completed` nếu đơn hoàn thành, `Behind` nếu chậm > 0, else `OnSchedule`. |
| BR-F08-05 | `isOverdue` = chưa hoàn thành ∧ `dueDate < hôm nay`; `isPastDueDate` = `dueDate < hôm nay` (không xét trạng thái); `isBeforeStartDate` = `hôm nay < startDate`; `daysRemaining` theo FS-D07. Đơn `Pending` không có ngày → không bao giờ quá hạn/chậm. |
| BR-F08-06 | **Thống kê đơn**: theo ngày (gộp dây chuyền): KH ban đầu, bù, KH, TT (null nếu chưa ghi nhận), `dayStatus` của ngày (đóng khi **mọi** ô có kế hoạch đã đóng), `isProvisional`, chênh lệch/thiếu chỉ khi mọi ô đã đóng, KH/TT lũy kế; theo dây chuyền: phân bổ, tổng KH, tổng TT, thiếu (Σ thiếu các ô đã đóng). |

### F-09 Dashboard

`GET /statistics/dashboard` tính cho **hôm nay**:

| Trường | Cách tính |
|---|---|
| `totalOrders`, `pendingOrderCount`, `incompleteOrders` (Incomplete **đã chốt**), `completedOrders` | đếm |
| `behindOrders`, `alerts[]` | đơn có `scheduleStatus = Behind` (đã chốt), sắp theo chậm giảm dần rồi ngày còn lại tăng dần |
| `totalOrderQuantity`, `totalActualQuantity` (gồm tạm tính), `totalRemainingQuantity` | tổng mọi đơn kể cả Pending/chờ chốt |
| `today` | KH/TT hôm nay của các đơn đã chốt, `hasAnyActualEntered`, chênh lệch, % |
| `trackedOrders[]` | đơn `Incomplete` đã chốt; mỗi đơn: %, hôm nay (KH, TT, trạng thái, chênh lệch chỉ khi mọi ô hôm nay đã đóng), còn lại, tình trạng, chuỗi ngày có kế hoạch (KH, TT null nếu chưa ghi nhận, trạng thái ngày); sắp theo **thời điểm lập tiến độ** mới nhất lên đầu |
| `todayProduction[]` | ô hôm nay có kế hoạch, chưa đóng |
| `unclosedPastCells[]` | ô quá khứ có kế hoạch > 0 chưa đóng (kể cả chưa có dòng ô), cũ nhất lên đầu |
| `openShortages[]` | ô đã đóng có thiếu > 0 và chưa có khoản bù Applied, thiếu nhiều lên đầu |
| `awaitingConfirmation[]` | đơn đã lập chưa chốt (mã, số lượng, ngày, ảnh), mới lập lên đầu |

Giao diện Bản 1 dùng: 5 số tổng, `trackedOrders`, `pendingOrderCount`, `awaitingConfirmation`. Các trường còn lại **chưa hiển thị** (xem `02` PR-Q01).

### F-10 Cấu hình nhắc ghi nhận

| Luật | Nội dung |
|---|---|
| BR-F10-01 | Đúng một dòng cấu hình, tạo lúc khởi động với mặc định 60 phút, nhắc trước = bật. |
| BR-F10-02 | `recordingIntervalMinutes` 5–480 (`400 OUT_OF_RANGE`, cũng là CHECK DB). |
| BR-F10-03 | Cấu hình chỉ dùng để nhắc trên modal ghi nhận; server không dùng để từ chối request; không hồi tố dữ liệu. |

### 5.8 Giá trị suy ra trong `OrderListItemDto` / `OrderDetailDto`

`totalActual`, `remaining`, `totalPlan` (Σ KH hiện tại), `totalInitialPlan` (chi tiết), `progressPercentage`, `scheduleStatus`, `behindQuantity`, `daysRemaining`, `isOverdue`, `isPastDueDate` (chi tiết), `isScheduleConfirmed`, `isBeforeStartDate`, `todayPlannedQuantity`/`todayActualQuantity` (danh sách; null nếu hôm nay không có kế hoạch), `hasUnclosedPastCell`, `unclosedPastDayCount` (danh sách).

---

## 6. Mã lỗi nghiệp vụ và câu chữ hiển thị

| Code | HTTP | Câu hiển thị (frontend) |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra lại. |
| `INVALID_CREDENTIALS` | 401 | Tên đăng nhập hoặc mật khẩu không đúng. |
| `NOT_AUTHENTICATED` | 401 | Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại. |
| `USER_INACTIVE` | 403 | Tài khoản đã bị vô hiệu hoá. |
| `ORDER_NOT_FOUND` | 404 | Không tìm thấy đơn hàng. |
| `SHOE_CODE_ALREADY_EXISTS` | 409 | Mã giày đã tồn tại. Vui lòng chọn mã khác. |
| `ORDER_OVERDUE` | 422 | Đơn hàng đã qua ngày kết thúc nên chỉ được xem, không thể thay đổi dữ liệu. |
| `ORDER_NOT_DELETABLE` | 422 | Đơn hàng đã lập tiến độ nên không xoá được nữa. |
| `IMAGE_NOT_FOUND` | 404 | Đơn hàng này chưa có ảnh mẫu. |
| `IMAGE_TYPE_NOT_SUPPORTED` | 400 | Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP. |
| `IMAGE_TOO_LARGE` | 400 | Ảnh vượt quá 5MB. Vui lòng chọn ảnh nhỏ hơn. |
| `ORDER_SCHEDULE_ALREADY_EXISTS` | 409 (lập lần 2) / 422 (đổi số lượng) | Đơn hàng này đã có tiến độ. Mỗi đơn chỉ lập tiến độ được một lần. |
| `ORDER_NOT_SCHEDULED` | 422 | Đơn hàng này chưa được lập tiến độ nên chưa có kế hoạch sản xuất. |
| `SCHEDULE_ALREADY_CONFIRMED` | 409 | Tiến độ của đơn hàng này đã được chốt nên không thể sửa hay xoá nữa. |
| `SCHEDULE_NOT_CONFIRMED` | 422 | Tiến độ của đơn hàng này chưa được chốt nên chưa thể nhập sản lượng hay xuất hàng. |
| `LINE_ALLOCATION_MISMATCH` | 422 | Tổng phân bổ cho các dây chuyền phải bằng đúng số lượng của đơn hàng. |
| `LINE_PLAN_TOTAL_MISMATCH` | 422 | Tổng kế hoạch theo ngày của mỗi dây chuyền phải bằng đúng số đã phân bổ cho dây chuyền đó. |
| `PRODUCTION_LINE_NOT_FOUND` | 404 | Không tìm thấy dây chuyền sản xuất. |
| `PRODUCTION_LINE_INACTIVE` | 422 | Dây chuyền đang ngừng hoạt động nên không thể chọn để lập tiến độ. |
| `PRODUCTION_LINE_CODE_ALREADY_EXISTS` | 409 | Mã dây chuyền đã tồn tại. Vui lòng chọn mã khác. |
| `PRODUCTION_LINE_EMPTY_ALLOCATION` | 422 | Dây chuyền đã chọn phải được phân bổ số lượng lớn hơn 0. |
| `PRODUCTION_LINE_IN_PRODUCTION` | 409 | Dây chuyền đang có đơn hàng sản xuất nên chưa thể ngừng hoạt động. |
| `PRODUCTION_PLAN_NOT_FOUND` | 404 | Không tìm thấy kế hoạch sản xuất. |
| `PRODUCTION_ENTRY_NOT_FOUND` | 404 | Không tìm thấy lần ghi nhận sản lượng này. |
| `DAY_HAS_NO_PLAN` | 422 | Không có kế hoạch sản xuất trong ngày này nên không thể ghi nhận sản lượng hay xuất hàng. |
| `DAY_ALREADY_CLOSED` | 409 | Ngày này đã xuất hàng nên số liệu đã được chốt sổ và không thể thay đổi. |
| `FUTURE_DATE_NOT_ALLOWED` | 422 | Ngày này chưa tới nên chưa thể ghi nhận sản lượng. |
| `ENTRY_EXCEEDS_DAILY_PLAN` | 422 | Vượt quá phần còn được nhập của ô. Tối đa còn **n** đôi. |
| `ACTUAL_EXCEEDS_ORDER_QUANTITY` | 422 | Vượt quá số lượng còn lại của đơn hàng. Tối đa còn **n** đôi. |
| `ORDER_ALREADY_COMPLETED` | 409 (ghi nhận) / 422 (bù) | Đơn hàng đã hoàn thành nên không thể ghi nhận thêm sản lượng. |
| `ADJUSTMENT_NOT_FOUND` | 404 | Không tìm thấy lần bù sản lượng. |
| `ADJUSTMENT_OUTDATED` | 409 | Dữ liệu sản xuất đã thay đổi. Vui lòng xem lại đề xuất bù sản lượng mới. |
| `ACTIVE_ADJUSTMENT_EXISTS` | 409 | Ngày này đã có một lần bù đang áp dụng. Hãy hoàn tác lần bù đó trước. |
| `ADJUSTMENT_NOT_APPLIED` | 409 | Lần bù này không còn ở trạng thái đang áp dụng. |
| `NO_SHORTAGE` | 422 | Ngày này không có sản lượng thiếu cần xử lý. |
| `INVALID_ADJUSTMENT_TARGET` | 422 | Ngày nhận bù không hợp lệ. |
| `DUPLICATE_ADJUSTMENT_TARGET` | 422 | Một ngày chỉ được chọn một lần trong cùng một lần bù. |
| `ADJUSTMENT_TOTAL_MISMATCH` | 422 | Tổng số lượng bù phải bằng đúng số lượng thiếu. |
| `NO_ELIGIBLE_TARGET_DAY` | 422 | Không còn ngày sản xuất nào có thể nhận phần bù. |
| `SOURCE_DAY_NOT_CLOSED` | 422 | Ngày này chưa xuất hàng nên chưa có số lượng thiếu chính thức để xử lý. |
| `TARGET_DAY_CLOSED` | 422 | Ngày nhận bù đã xuất hàng nên không thể nhận thêm kế hoạch. |
| `TARGET_DATE_IN_PAST` | 422 | Không thể bù vào một ngày đã qua. |
| `ADJUSTMENT_TARGET_LINE_MISMATCH` | 422 | Chỉ được bù sang ngày khác của chính dây chuyền phát sinh thiếu. |
| `INTERNAL_ERROR` | 500 | Đã xảy ra lỗi không mong muốn. Vui lòng thử lại. |
| `INITIAL_PLAN_TOTAL_MISMATCH` | — | Còn trong danh sách mã nhưng **không còn được ném** (kế thừa bản trước CR-001). |

---

## 7. Acceptance Criteria

| Mã | Tiêu chí | Feature |
|---|---|---|
| AC-01 | Tạo đơn trùng mã giày (khác hoa thường không tính) → 409; đơn tạo xong ở `Pending`, không ngày. | F-03 |
| AC-02 | Upload PDF đổi tên `.jpg` → 400 `IMAGE_TYPE_NOT_SUPPORTED`; ảnh 6 MB → 400 `IMAGE_TOO_LARGE`. | F-03 |
| AC-03 | Sửa số lượng của đơn đã lập tiến độ → 422; xoá đơn đã lập tiến độ → 422; xoá đơn Pending → 204 và file ảnh biến mất. | F-03 |
| AC-04 | Lập tiến độ với Σ phân bổ ≠ số lượng → 422 `LINE_ALLOCATION_MISMATCH`; một cột lệch → 422 `LINE_PLAN_TOTAL_MISMATCH` với details đúng dây chuyền. | F-04 |
| AC-05 | Ô kế hoạch 0 không xuất hiện trong ma trận và không ghi nhận được (`DAY_HAS_NO_PLAN`). | F-04/F-05 |
| AC-06 | Lập xong chưa chốt: ghi nhận → 422 `SCHEDULE_NOT_CONFIRMED`; sửa tiến độ được; xoá tiến độ → đơn về `Pending` giữ mã/ảnh; chốt → từ đó sửa/xoá trả 409. | F-04 |
| AC-07 | Đơn chưa chốt đã quá hạn: chốt → 422 `ORDER_OVERDUE`; sửa dời ngày rồi chốt → thành công. | F-04 |
| AC-08 | Ghi nhận 0 → 400; ghi nhận vượt kế hoạch ô → 422 kèm `MAX_ALLOWED`; ghi nhận ở ô cuối vượt số lượng đơn → 422 `ACTUAL_EXCEEDS_ORDER_QUANTITY`; ngày tương lai → 422. | F-05 |
| AC-09 | Sửa lần ghi nhận xuống thấp hơn luôn thành công; xoá rồi tổng ô giảm; log có 3 action. | F-05 |
| AC-10 | Xuất hàng ngày có 2 dây chuyền, một dây chuyền chưa ghi nhận → cả hai đóng, ô trống có thực tế 0 và thiếu = KH; bấm lần 2 → 409. | F-06 |
| AC-11 | Xuất hàng ngày cuối với tổng đủ → đơn `Completed`, `hasShortage=false`; nhập thêm → 409 `ORDER_ALREADY_COMPLETED`. | F-06 |
| AC-12 | Ô đang mở có TT < KH: `shortageQuantity = null`, dashboard không liệt kê trong `openShortages`. | F-08/F-09 |
| AC-13 | Option 2 với thiếu 23 và 4 ngày đích → 6/6/6/5 theo ngày tăng dần; Option 1 chọn ngày khác dây chuyền → 422 `ADJUSTMENT_TARGET_LINE_MISMATCH`. | F-07 |
| AC-14 | Áp dụng với `shortageQuantity` khác thực tế → 409 `ADJUSTMENT_OUTDATED`; áp dụng lần 2 cho cùng ô → 409 `ACTIVE_ADJUSTMENT_EXISTS`; hoàn tác → KH ngày đích giảm đúng, bản ghi `Reversed` vẫn trong lịch sử. | F-07 |
| AC-15 | Đơn quá hạn: ghi nhận, xuất hàng, xem trước bù, áp dụng, hoàn tác, sửa nhập hàng đều → 422 `ORDER_OVERDUE`; xem chi tiết vẫn được. | BR-01 |
| AC-16 | Dây chuyền có đơn đang sản xuất: PATCH `Inactive` → 409; sau khi đơn hoàn thành → thành công; dây chuyền Inactive không nằm trong `GET /production-lines?status=Active`. | F-02 |
| AC-17 | Đơn có ô hôm nay đang mở với TT < KH **không** bị tính chậm; sau khi xuất hàng hôm nay mới tính. | F-08 |
| AC-18 | Cấu hình 4 phút hoặc 481 phút → 400; 60 phút + nhắc trước → modal nhắc "Còn 15 phút…" ở phút 45 kể từ lần ghi nhận gần nhất. | F-10 |
