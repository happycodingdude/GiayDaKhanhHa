# 04 — UX/UI Specifications — Bản 1

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Trạng thái** | Draft (as-built) — chờ Chốt |
| **Nguồn** | Sinh lại từ `frontend/src` tại commit `74a9fc2` (09/10/2026). |
| **Role** | Principal Product Designer + Senior UX/UI Designer |
| **Spec đầu vào** | `03-feature-specifications.md` |

> Spec mô tả giao diện **đang chạy**. Lý do thiết kế ("vì sao") lấy từ comment trong code và tài liệu cũ; chỗ nào không có lý do rõ ràng được đánh `Assumption`.

---

## 1. Confirmed Requirements

| Mã | Nội dung |
|---|---|
| UX-C01 | Ứng dụng **desktop-first**, một người dùng, tiếng Việt toàn bộ. Có một breakpoint `≤ 900px` để không vỡ trên màn hình hẹp, không tối ưu cho mobile. |
| UX-C02 | Khung ứng dụng: **sidebar cố định** bên trái (232 px, nền tối) chứa thương hiệu, điều hướng, tài khoản + đăng xuất + ngày hôm nay; chỉ cột nội dung cuộn. |
| UX-C03 | Điều hướng 4 mục: **Dashboard · Nhập hàng · Tiến độ · Cấu hình** (nhóm, gồm *Dây chuyền sản xuất* và *Cấu hình chung*). |
| UX-C04 | Mọi màn hình lấy dữ liệu từ server đều có **loading / error (kèm Thử lại) / empty** state. |
| UX-C05 | Thao tác ghi thành công báo bằng **toast** (tự ẩn sau 5 giây); lỗi hiển thị **inline** ngay trong form/dialog, câu chữ tiếng Việt từ bảng ánh xạ mã lỗi. |
| UX-C06 | Số liệu định dạng `vi-VN` (`1.000`); ngày `dd/mm/yyyy`; timestamp `dd/mm/yyyy HH:mm`; số chưa nhập hiển thị `—`, **không bao giờ là 0**. |
| UX-C07 | Thao tác không hoàn tác được (xoá đơn, xoá tiến độ, chốt tiến độ, xuất hàng, ngừng dây chuyền, ghi đè phân bổ, chia đều lại, hoàn tác bù) luôn qua **hộp thoại xác nhận**. |
| UX-C08 | Mọi nút hành động mặc định màu xanh (primary); nút phá huỷ màu đỏ (danger). |
| UX-C09 | Trong văn bản chú thích/xác nhận, **số liệu, mã, ngày chính được in đậm**. |
| UX-C10 | Ma trận ngày × dây chuyền là màn hình trung tâm; **ghi nhận sản lượng** mở trong modal, **xuất hàng** là nút riêng theo ngày, **Enter trong form ghi nhận không bao giờ kích hoạt xuất hàng**. |
| UX-C11 | Modal: khoá cuộn nền, `Escape` chỉ đóng modal trên cùng, bấm nền đóng, header/footer luôn hiển thị, chỉ vùng danh sách bên trong cuộn. |

---

## 2. Assumptions

| Mã | Giả định |
|---|---|
| UX-A01 | Màn hình làm việc tối thiểu ~1280 px; ma trận với 5+ dây chuyền cần cuộn ngang và ghim cột ngày. |
| UX-A02 | Icon dùng emoji/SVG inline, không có bộ icon riêng; đây là lựa chọn giữ dependency tối thiểu. |
| UX-A03 | Font hệ thống (`Segoe UI`, Roboto, system-ui); không nhúng font. |

---

## 3. Open Questions

| Mã | Câu hỏi |
|---|---|
| UX-Q01 | API dashboard trả thêm `alerts`, `today`, `todayProduction`, `unclosedPastCells`, `openShortages` nhưng Dashboard hiện **không render** các khối này. Có muốn hiển thị lại hay bỏ khỏi API? (ghi vào Bản 2 nếu muốn thêm UI) |
| UX-Q02 | Có cần trang 404 cho route không tồn tại không? Hiện router không khai báo `notFoundComponent`. |

---

## 4. Decisions

| Mã | Quyết định | Vì sao |
|---|---|---|
| UX-D01 | Nhập hàng và Tiến độ là **hai màn riêng**, hai bước nghiệp vụ khác thời điểm (hàng về ≠ lập kế hoạch). | Đơn mới về chưa biết chạy dây chuyền nào, bắt đầu ngày nào. |
| UX-D02 | Danh sách Nhập hàng: **dòng không bấm được**, mọi điều hướng qua nút ở cột Thao tác. Danh sách Tiến độ: **dòng bấm được** mở chi tiết. | Nhập hàng có nhiều hành động cạnh tranh (Lập tiến độ / Sửa / Xoá); Tiến độ chỉ có một đích. |
| UX-D03 | Lập tiến độ là **wizard 5 bước** (Chọn mã giày → Dây chuyền & ngày → Phân bổ dây chuyền → Kế hoạch theo ngày → Xem lại), phản ánh đúng **hai tầng phân bổ**. | Quản lý nghĩ theo thứ tự: chọn dây chuyền → chia đơn cho dây chuyền → chia dây chuyền theo ngày. |
| UX-D04 | Chia đều là **mặc định** và luôn điền sẵn; nhập tay là lựa chọn phụ. Chuyển về chia đều phải xác nhận vì ghi đè số đã nhập. | Điểm xuất phát cụ thể tốt hơn bảng trống. |
| UX-D05 | Wizard chặn **ngay từ đầu** khi không có dây chuyền Active / không có đơn chờ lập. | Bắt người dùng phát hiện ở bước 3 là quá muộn. |
| UX-D06 | Ma trận: cột ngày ghim, tiêu đề tách khỏi vùng cuộn dọc, mỗi dây chuyền 4 cột (KH · TT · Lệch · thao tác), cột "Tổng TT" và "Xuất hàng" cuối hàng. | Với nhiều dây chuyền, cuộn sang phải mà mất cột ngày là mất mốc đọc. |
| UX-D07 | Ô không có kế hoạch hiển thị `—`, không có thao tác. | Server không tạo dòng kế hoạch cho ô 0, không có gì để nhập. |
| UX-D08 | Form ghi nhận: ô số lượng auto-focus, Enter = Ghi nhận, nút **Tối đa** điền trọn phần còn lại rồi trả focus về ô (không tự gửi). | Màn này dùng 8–10 lần/ngày; thao tác lặp nhiều nhất là "nhập đủ phần còn lại". |
| UX-D09 | Dialog Xuất hàng: focus mặc định vào **Quay lại**, hiển thị **đầy đủ** mọi lần ghi nhận của mọi dây chuyền, cảnh báo đỏ "không sửa, không xoá, không mở lại", tự tải state mới nhất từng ô thay vì dùng số trên ma trận. | Chốt sổ là một chiều; Enter theo phản xạ không được phép chốt. |
| UX-D10 | Sửa/xoá lần ghi nhận **không bắt buộc nhập lý do**; nút biểu tượng (✎ / 🗑) thay vì chữ. | Sửa nháp trước khi chốt sổ phải nhanh; lịch sử vẫn ghi vào log hệ thống. |
| UX-D11 | Trạng thái "đang lưu" ở màn sửa nhập hàng chỉ hiện khi request lâu hơn 300 ms. | Request local vài chục ms mà spinner chớp lên rồi tắt trông như lỗi. |
| UX-D12 | Dashboard chỉ để **nhìn**, không phải nơi bắt đầu hành động (trừ nút Nhập hàng); mọi thao tác lên ô sản xuất nằm ở chi tiết tiến độ. | Giữ dashboard gọn, tránh trùng luồng. |
| UX-D13 | Timeline tháng trên dashboard: một chấm/ngày (đạt · thiếu · đang sản xuất · chưa nhập), tooltip tự vẽ, cột hôm nay tự căn giữa. | Tooltip gốc của trình duyệt trễ ~1 s và không chỉnh cỡ chữ; tháng 31 cột gần như luôn phải cuộn. |
| UX-D14 | Thumbnail ảnh mẫu bấm mở lightbox; đơn chưa có ảnh hiện placeholder 👟 thay vì ô trống. | Ô trống trong bảng đọc như dữ liệu lỗi. |
| UX-D15 | `Select` tự vẽ thay `<select>` gốc; `DateInput` giữ `<input type=date>` nhưng vẽ đè chữ `dd/mm/yyyy`. | Popup gốc không style được; Chrome hiển thị ngày theo locale trình duyệt. |
| UX-D16 | Khối mới trên Dashboard nối **dưới cùng** layout hiện tại (khối *Chờ chốt tiến độ* nằm sau timeline). | Quy ước đã chốt với chủ dự án. |
| UX-D17 | Trang chi tiết: link quay lại nằm **riêng một dòng** phía trên tiêu đề; ảnh chỉ cùng hàng với tiêu đề. | Quy ước đã chốt với chủ dự án. |

---

## 5. Detailed Specification

### 5.1 Information architecture & navigation

```text
/login
/ (→ /dashboard)
├── /dashboard                      S-02 Dashboard
├── /goods-receipt                  S-03 Nhập hàng — danh sách
│   ├── /goods-receipt/new          S-04 Nhập hàng — tạo
│   └── /goods-receipt/$orderId     S-05 Nhập hàng — sửa
├── /progress                       S-06 Tiến độ — danh sách
│   ├── /progress/new?orderId=      S-07 Lập tiến độ (wizard)
│   ├── /progress/$orderId          S-09 Chi tiết tiến độ (ma trận + dialog)
│   └── /progress/$orderId/edit     S-08 Sửa tiến độ (wizard, chế độ sửa)
├── /settings                       S-11 Cấu hình chung
└── /settings/production-lines      S-10 Dây chuyền sản xuất
/orders, /orders/new, /orders/$id   → redirect sang /progress, /goods-receipt/new, /progress/$id
```

Luồng chính của quản lý:

```text
Nhập hàng (S-04) → [Lập tiến độ] (S-07) → Chi tiết tiến độ (S-09) → [Chốt tiến độ]
   → mỗi ngày: [Nhập SL] (modal) nhiều lần → [Xuất hàng] (dialog) → nếu thiếu: [Xử lý thiếu] (dialog 4 bước)
   → Dashboard (S-02) để nhìn tổng thể
```

### 5.2 Khung ứng dụng (AppLayout)

- Sidebar: thương hiệu "👟 Quản lý sản xuất"; nav với trạng thái active (mục *Cấu hình chung* khớp chính xác để không sáng cùng *Dây chuyền sản xuất*); panel tài khoản ở đáy: avatar (chữ cái đầu tên), tên hiển thị, `Thứ · dd/mm/yyyy`, nút **Đăng xuất**.
- Đăng xuất: gọi API, xoá toàn bộ cache query, về `/login`.
- Phiên hết hạn: route guard gọi `/auth/me` trước khi render khung; 401/403 → `/login` thẳng, không chớp màn trống.
- `≤ 900px`: sidebar thành thanh ngang trên cùng, ẩn ngày.

### 5.3 S-01 Đăng nhập

- Panel giữa màn: logo, tiêu đề "Quản lý sản xuất", phụ đề "Đăng nhập để theo dõi tiến độ đơn hàng".
- Field: Tên đăng nhập (autofocus, bắt buộc), Mật khẩu (bắt buộc). Validation client bằng zod; lỗi server inline (`INVALID_CREDENTIALS` → "Tên đăng nhập hoặc mật khẩu không đúng."; `USER_INACTIVE`).
- Thành công → `/dashboard`.

### 5.4 S-02 Dashboard

Header: "Dashboard" + "Tình hình sản xuất ngày **dd/mm/yyyy**" (ngày do backend trả) + nút **+ Nhập hàng**.

Trạng thái: loading; error (Thử lại); **chưa có đơn nào** → EmptyState "Chưa có đơn hàng" + nút Nhập hàng.

Khi có đơn:

1. **5 stat tile**: Đơn đang chạy · Đang chậm (đỏ nếu > 0) · Hoàn thành · Đã hoàn thành (đôi, hint "Gồm cả sản lượng tạm tính của ngày chưa xuất hàng") · Còn lại (đôi).
2. **Đơn hàng đang theo dõi** (card, chỉ đơn đã chốt tiến độ và chưa hoàn thành, mới lập lên đầu):
   - Nút chuyển **☰ Xem danh sách / ▦ Xem tiến độ**.
   - *Timeline*: toolbar tháng (‹ › , nhãn "Tháng M, YYYY", nút **Hôm nay**), chú giải 4 loại chấm. Bảng: Mã đơn (sticky, cắt `…`), Tiến độ %, 31 cột ngày (Chủ nhật tô riêng, cột hôm nay có nhãn "Hôm nay"), cột đệm, Tình trạng (badge). Mỗi ngày có kế hoạch: chấm `met` (đã xuất hàng, đủ), `short` (đã xuất hàng, thiếu), `progress` (đang sản xuất, đã có số tạm tính), `missing` ("!", ngày đã qua chưa xuất hàng). Ngày nghỉ/chưa tới/hôm nay chưa nhập: không chấm. Hover chấm → tooltip "dd/mm/yyyy · KH n · …". Gợi ý "← Kéo ngang để xem thêm →".
   - *Danh sách*: Mã đơn · Tiến độ (% + thanh) · Hôm nay (badge "Không SX" / chênh lệch nếu đã xuất hàng / "TT / KH Tạm tính") · Còn lại · Tình trạng.
   - Empty: nếu còn đơn chưa lập/chờ chốt → "Chưa có đơn hàng nào đang sản xuất" kèm số đơn chưa lập và chờ chốt; nếu không còn gì → "✓ Tất cả đơn hàng đã hoàn thành".
   - Bấm dòng → `/progress/$orderId`.
3. **Chờ chốt tiến độ** (card, chỉ hiện khi có đơn): Ảnh · Mã giày · Số lượng · Ngày bắt đầu · Ngày kết thúc · Tình trạng (badge "Chờ chốt", hoặc đỏ "Quá hạn — không chốt được" khi đã qua ngày kết thúc). Bấm dòng → chi tiết.

### 5.5 S-03 Nhập hàng — danh sách

- Header: "Nhập hàng" / "Quản lý hàng nhận về theo mã giày" + nút **+ Nhập hàng**.
- Toolbar: segmented filter **Tất cả · Chưa lập tiến độ · Đang sản xuất · Quá hạn · Hoàn thành**; ô tìm mã giày (submit bằng nút Tìm, nút Xoá khi có từ khoá).
- Bảng: Ảnh (thumbnail 44 px) · Mã giày (đậm, xuống dòng) · Số lượng · Trạng thái (badge) · Thao tác: **Lập tiến độ** (Pending) hoặc **Xem tiến độ**, **Sửa**, **Xoá** (chỉ Pending, đỏ).
- Phân trang: "**N** đơn hàng · hiển thị **a–b**", chọn số dòng 10/20/50, Trước/Sau, "Trang x / y". Xoá dòng cuối của trang > 1 thì lùi trang.
- Empty: có filter → "Không tìm thấy đơn hàng phù hợp"; không filter → "Chưa có đơn hàng nào" + nút.
- Dialog xoá: "Xoá đơn nhập hàng?" — "Đơn **MÃ** (**n đôi**) [cùng ảnh mẫu] sẽ bị xoá vĩnh viễn…", nút đỏ **Xoá đơn**; lỗi inline.

### 5.6 S-04 Nhập hàng — tạo

- Back-link "← Danh sách nhập hàng"; tiêu đề "Nhập hàng"; phụ đề giải thích dây chuyền và thời gian quyết định ở bước lập tiến độ.
- Lưới 2 cột: card **Ảnh mẫu** (ImageUploader) · card **Thông tin nhập hàng** (Mã giày autofocus, tối đa 50; Số lượng (đôi) chỉ nhận chữ số, hậu tố "đôi"; nút Huỷ / **Nhập hàng**).
- Validation client: mã bắt buộc ≤ 50; số lượng số nguyên > 0. Lỗi server inline (`SHOE_CODE_ALREADY_EXISTS`…).
- Thành công → toast "Đã nhập hàng **MÃ**." → chuyển sang S-05 của đơn vừa tạo.

**ImageUploader**: khung xem trước luôn hiện (placeholder "Chưa có ảnh mẫu"); vùng kéo-thả/bấm chọn "JPG/PNG/WEBP · tối đa 5MB"; kiểm MIME + kích thước + chữ ký byte ở client; nút xoá (bỏ file chọn / gỡ ảnh đang lưu) và nút phóng to (lightbox, luôn bấm được kể cả chỉ đọc).

### 5.7 S-05 Nhập hàng — sửa

- Back-link; tiêu đề = mã giày.
- Thông báo đỏ "🔒 Đơn hàng đã qua ngày kết thúc nên chỉ được xem lại." khi `isPastDueDate` → toàn bộ form và uploader disabled.
- Số lượng **khoá** khi đơn đã lập tiến độ, hint: "Đơn đã lập tiến độ nên số lượng không đổi được — nó là mốc phân bổ cho các dây chuyền."
- Ảnh: chọn ảnh mới (thay) hoặc gỡ ảnh đang lưu — chỉ đánh dấu ở client, lưu cùng lần bấm **Lưu thay đổi**; **Huỷ thay đổi** trả về trạng thái server. Nhãn "Có thay đổi chưa lưu" khi dirty.
- Sau lưu: toast "Đã lưu thay đổi."; ảnh hiển thị có query `?v=updatedAt` để trình duyệt tải ảnh mới.

### 5.8 S-06 Tiến độ — danh sách

- Header: "Tiến độ" / "Theo dõi sản xuất theo mã giày và dây chuyền" + nút **+ Lập tiến độ**.
- Toolbar: segmented **Tất cả · Chưa hoàn thành · Quá hạn · Hoàn thành** (mặc định "Tất cả" = mọi đơn đã lập tiến độ, không gồm Pending); dropdown **Dây chuyền** (Tất cả / mã); tìm mã giày.
- Bảng (dòng bấm được, Enter mở): Ảnh · Mã giày (cắt `…`, title đầy đủ) · Dây chuyền (số lượng dây chuyền) · Tổng SL · Đã làm · Ngày bắt đầu · Ngày kết thúc · **Ngày chưa xuất hàng** (badge vàng nếu > 0) · Trạng thái (badge) · Tình trạng (badge tiến độ; `—` khi chưa chốt).
- Phân trang như S-03. Empty tương tự với nút Lập tiến độ.

### 5.9 S-07 Lập tiến độ (wizard) & S-08 Sửa tiến độ

Stepper ngang 5 bước (bước đã qua bấm được để quay lại; bước chưa tới không bấm được). Chế độ sửa bỏ bước 1, điền sẵn tiến độ hiện có, nút cuối "Lưu tiến độ", back-link về chi tiết.

Chặn sớm (trước mọi bước): không có dây chuyền Active → EmptyState "Chưa có dây chuyền nào đang hoạt động" + nút tới cấu hình; (tạo mới) không có đơn Pending → "Không có đơn hàng nào chờ lập tiến độ" + nút Nhập hàng.

| Bước | Nội dung | Validation / hành vi |
|---|---|---|
| 1 Chọn mã giày | Danh sách radio đơn Pending (ảnh 56 px, mã, số lượng) + ô tìm. Vào từ nút "Lập tiến độ" (query `orderId`) thì bỏ qua bước này. | Phải chọn 1 đơn. |
| 2 Dây chuyền & ngày | Checkbox các dây chuyền Active (mã + tên); Ngày bắt đầu (mặc định hôm nay); Ngày kết thúc (min = ngày bắt đầu; đổi ngày bắt đầu thì xoá ngày kết thúc); hint "Số ngày sản xuất: **n ngày**" luôn hiện. Chế độ sửa: thông báo vàng liệt kê dây chuyền đã ngừng bị bỏ khỏi tiến độ kèm số đã phân bổ. | ≥ 1 dây chuyền; đủ 2 ngày; kết thúc ≥ bắt đầu. Sang bước 3 thì **chia đều** số lượng đơn cho các dây chuyền (chế độ sửa giữ phân bổ cũ nếu bộ dây chuyền không đổi). |
| 3 Phân bổ dây chuyền | Radio **Tự động chia đều** (khoá ô nhập, hint "phần dư dồn vào các dây chuyền đầu") / **Nhập tay**; bảng Dây chuyền · Số lượng (đôi); dòng tổng; dải trạng thái "✓ Đã phân bổ a / b" hoặc "Còn thiếu n" / "Vượt n". | Tiếp tục chỉ khi tổng = số lượng đơn **và** không dây chuyền nào = 0 (cảnh báo vàng kèm hướng dẫn). Chuyển Nhập tay → Chia đều phải xác nhận "Ghi đè phân bổ đang nhập?". |
| 4 Kế hoạch theo ngày | Ma trận ngày (hàng) × dây chuyền (cột) ô nhập số, cột Tổng theo hàng; tfoot "Phân bổ": cột khớp hiện "n ✓" xanh, cột lệch hiện "đang nhập / mốc" + "thiếu x"/"vượt x" đỏ. Nút **Chia đều lại** (xác nhận). Hint: "Ô để **0** nghĩa là dây chuyền đó nghỉ ngày đó…". Ma trận khởi tạo bằng chia đều số của từng dây chuyền cho các ngày; chế độ sửa chỉ dựng lại khi dây chuyền/ngày/phân bổ đổi. | **Xem lại** chỉ khi mọi cột khớp. Tổng theo hàng không ràng buộc. |
| 5 Xem lại | Tóm tắt (Mã giày · Tổng số lượng · Thời gian · Cách phân bổ) + ma trận chỉ đọc + tfoot "Phân bổ … ✓". Nút Quay lại / **Tạo tiến độ** (hoặc Lưu tiến độ). | Lỗi server inline. Thành công → toast → chi tiết tiến độ. |

Bản nháp wizard (tạo mới) lưu `sessionStorage`; nếu đơn trong nháp không còn Pending khi tải lại → về bước 1 + toast info.

### 5.10 S-09 Chi tiết tiến độ

**Trạng thái đặc biệt**

- Đơn `Pending` → card "Đơn hàng này chưa được lập tiến độ…" + nút **Lập tiến độ ngay**.
- `awaitingConfirmation` (chưa chốt): header có **Sửa tiến độ · Xoá tiến độ (đỏ) · Chốt tiến độ** (nút Chốt ẩn nếu đã quá hạn); thông báo vàng "Tiến độ chưa chốt nên chưa thể nhập sản lượng hay xuất hàng… sau khi chốt sẽ không sửa được nữa." hoặc (quá hạn) "…không chốt được nữa. Bấm **Sửa tiến độ** để dời ngày rồi chốt, hoặc **Xoá tiến độ**…"; ma trận ở chế độ chỉ đọc.
- `readOnly` (qua ngày kết thúc, đã chốt): thông báo đỏ "🔒 Đơn hàng đã qua ngày kết thúc (**dd/mm/yyyy**) nên chỉ được xem lại…"; ẩn mọi thao tác ghi, giữ nhãn trạng thái.
- Có ngày đã qua chưa xuất hàng (và không readOnly/awaiting): thông báo vàng "⚠ **n ngày** đã qua chưa xuất hàng (dd/mm, …)…".

**Bố cục**

1. Header: back-link riêng dòng; hàng tiêu đề = thumbnail 64 px + mã giày; actions bên phải (các nút tiến độ + **Thông tin nhập hàng**).
2. Card **Tổng quan đơn hàng**: 6 stat tile (Tổng số lượng · Đã hoàn thành (xanh) · Còn lại · Ngày bắt đầu · Ngày kết thúc với badge "Còn **n ngày**" hoặc đỏ "Đã quá hạn") + thanh tiến độ % (đỏ nếu chậm, xanh lá nếu hoàn thành) + badge trạng thái đơn + badge tình trạng tiến độ (ẩn khi chưa chốt).
3. Card **Tiến độ sản xuất theo ngày × dây chuyền** (ma trận) — chú thích "KH = kế hoạch hiện tại · TT = sản lượng thực tế · Lệch = TT − KH, là số tạm tính khi ngày chưa xuất hàng."
   - Tiêu đề 2 hàng: Ngày | mỗi dây chuyền: mã + "TT / KH" tổng, hàng dưới KH · TT · Lệch · (thao tác) | Tổng TT | Xuất hàng.
   - Mỗi hàng ngày: `dd/mm` + thứ (+ "· Hôm nay", hàng được tô); ô có kế hoạch: KH (+ "+n" nếu có bù), TT (`—` nếu chưa ghi nhận, "Tạm tính" nếu ô mở), Lệch (đỏ nếu < 0; tạm tính khi ô mở), thao tác: **Nhập SL** (ô InProduction, không readOnly, đơn chưa hoàn thành) / **Xem** + **Xử lý thiếu** (đỏ; ô Closed có thiếu, chưa có bù đang áp dụng, đơn chưa hoàn thành) + badge "Đã bù"; ô không kế hoạch: `—` gộp 4 cột; Tổng TT theo hàng; cột Xuất hàng: nút **Xuất hàng** nếu còn ô mở (kể cả đơn đã hoàn thành, trừ readOnly) / "✓ Đã xuất" nếu mọi ô đã đóng.
4. Card **Lịch sử bù sản lượng**: mỗi mục: "Bù sản lượng thiếu **n đôi**" + badge Đang áp dụng/Đã hoàn tác + badge phương thức (Chọn ngày để bù / Hệ thống chia đều) + badge mã dây chuyền; meta "Ô thiếu: dd/mm/yyyy · DC · 👤 người · thời điểm"; danh sách ngày đích "+n đôi"; meta hoàn tác; nút **Hoàn tác** (Applied, không readOnly) → dialog xác nhận liệt kê các ngày sẽ bị trừ. Empty "Chưa có lần bù sản lượng nào".
5. Card **Thống kê lũy kế**: bảng theo dây chuyền (Dây chuyền · Phân bổ · Kế hoạch (+n) · Thực tế · Thiếu) và bảng theo ngày (Ngày · Kế hoạch · Thực tế (Tạm tính) · Chênh lệch (`—` khi chưa đóng đủ) · Tình trạng (badge ngày) · KH lũy kế · TT lũy kế).

**Dialog**

- **Nhập sản lượng** (modal 1040 px, mở từ Nhập SL; cùng component ở chế độ chỉ đọc khi bấm Xem): tiêu đề "Nhập sản lượng" / "Chi tiết ô sản xuất"; phụ đề "MÃ · DC · dd/mm/yyyy · Thứ"; badge trạng thái ngày (InProduction + ngày đã qua → vàng "Chưa xuất hàng") + "🕐 Nhập gần nhất: …"; các thông báo: khoá (đơn quá hạn), vàng "Ngày này đã qua nhưng chưa xuất hàng. Bạn vẫn có thể nhập bù…", NoPlan, NotStarted, **nhắc chu kỳ** "⏰ Đã **n phút** kể từ lần ghi nhận gần nhất, quá chu kỳ **m phút**…" hoặc "Còn **k phút** nữa là tới hạn…" (nhắc trước 15 phút nếu bật); 3 stat card: Kế hoạch ngày (hint "Đã bù thêm **n đôi**") · Đã nhập hôm nay/Đã xuất hàng (hint "Tạm tính — chưa xuất hàng") · Còn được nhập (hint lý do: "Giới hạn bởi kế hoạch của ngày" / "Giới hạn bởi số lượng còn lại của đơn hàng (**n đôi**)") hoặc, khi đã đóng, Thiếu so với kế hoạch; 2 cột: **Ghi nhận thêm** (form: Số lượng (đôi) autofocus + nút **Tối đa**; Ghi chú ≤ 255; hint "Còn được nhập: **n đôi**" / "Nhấn Enter để ghi nhận nhanh"; lỗi client khi 0 hoặc vượt trần; nếu trần = 0 → thông báo "Đã nhập đủ kế hoạch của ngày") | **Các lần đã ghi nhận trong ô** (badge "n lần · tổng m đôi"; bảng Thời điểm (+ "Đã sửa", người, ghi chú cắt) · Số lượng · Lũy kế · ✎ 🗑 khi ô mở; chỉ hiện 4 dòng (3 khi đã đóng) rồi cuộn; empty "Chưa ghi nhận lần nào trong ngày" / "Ngày này được xuất hàng với sản lượng **0**"); khối tổng kết khi đã đóng (Sản lượng chốt sổ · Thiếu so với kế hoạch · Thời điểm xuất hàng · Người xuất hàng) + "Ngày sản xuất này đã chốt sổ và không thể thay đổi."
  - Sửa lần ghi nhận: modal nhỏ, trần = còn được nhập + số cũ; Xoá: modal xác nhận "Sản lượng của ngày sẽ giảm tương ứng…".
- **Xác nhận xuất hàng** (760 px): phụ đề "MÃ · dd/mm/yyyy · n dây chuyền"; bảng Dây chuyền · Kế hoạch · Đã ghi nhận · Số lần · Thiếu (+ dòng "Cả ngày" nếu > 1 dây chuyền); cảnh báo vàng nếu có dây chuyền chưa ghi nhận lần nào ("…sản lượng bằng **0** và toàn bộ kế hoạch được ghi nhận là thiếu"); bảng đầy đủ mọi lần ghi nhận; cảnh báo đỏ chốt sổ vĩnh viễn; nút **Quay lại** (focus mặc định) / **Xác nhận xuất hàng** (chỉ khi đã tải đủ). Thành công → toast "Đã xuất hàng dd/mm/yyyy: DC-01 n đôi, … [Đơn hàng đã hoàn thành.]" (tone info nếu có thiếu).
- **Xử lý sản lượng thiếu** (720 px cố định, Stepper compact): bước **Phương thức** (header: Ngày thiếu · Dây chuyền · Kế hoạch · Thực tế · Số lượng cần bù; radio *Chọn ngày để bù* / *Hệ thống đề xuất chia đều*; cảnh báo nếu dây chuyền không còn ngày nhận bù) → (Manual) **Chọn ngày** (radio các ngày hợp lệ cùng dây chuyền, sau ngày thiếu, chưa qua, chưa đóng; kèm kế hoạch hiện tại) → **Xem trước** (bảng Ngày · Hiện tại · Bù thêm · Sau khi bù chỉ gồm ngày hợp lệ, dòng nhận bù tô sáng; tổng kết 4 số; câu "Tổng số lượng đơn hàng **không thay đổi**…"; thông báo chia đều; lỗi validation từ server) → **Xác nhận** (liệt kê "dd/mm/yyyy: a → **b** đôi (+n)"; "Thao tác này sẽ được ghi vào lịch sử bù sản lượng."; nút **Xác nhận bù**). Thành công → toast "Đã bù **n đôi** cho k ngày của DC."
- **Chốt tiến độ?**: "Chốt tiến độ của **MÃ** (**bắt đầu** → **kết thúc**). Sau khi chốt, tiến độ không sửa được nữa và đơn hàng bắt đầu nhập được sản lượng."
- **Xoá tiến độ?** (đỏ): "…(**n dây chuyền**) sẽ bị xoá. Đơn hàng quay về **Chưa lập tiến độ**; mã giày, số lượng và ảnh mẫu được giữ nguyên để lập lại."

### 5.11 S-10 Dây chuyền sản xuất

- Header + phụ đề giải thích dây chuyền ngừng vẫn giữ lịch sử; nút **+ Thêm dây chuyền**.
- Bảng: Mã · Tên · Thứ tự · Ghi chú · Trạng thái (● Đang hoạt động / ○ Ngừng hoạt động) · Đang dùng ("n đơn đang sản xuất" / "Có" / "Không") · Thao tác: **Sửa**, **Ngừng** (đỏ; disabled + tooltip khi có đơn đang sản xuất) / **Bật lại**.
- Dialog thêm/sửa (520 px): Mã (≤ 30, autofocus) · Tên (≤ 100) · Thứ tự (số nguyên ≥ 0, hint "Quyết định thứ tự hiển thị và thứ tự nhận phần dư khi chia đều sản lượng.") · Ghi chú (≤ 500). Lỗi server inline (`PRODUCTION_LINE_CODE_ALREADY_EXISTS`).
- Dialog ngừng/bật lại: xác nhận, câu chữ nêu hệ quả; lỗi hiển thị bằng toast đỏ.
- Empty: "Chưa cấu hình dây chuyền" + nút.
- Không có nút xoá.

### 5.12 S-11 Cấu hình chung

- Card **Ghi nhận sản lượng**: Chu kỳ ghi nhận (phút, 5–480, lỗi inline khi ngoài khoảng) · checkbox **Nhắc trước khi tới hạn** (hint giải thích) · ghi chú "Lời nhắc không giới hạn số lần ghi nhận trong ngày và không thay đổi dữ liệu đã ghi trước đó." · nút **Lưu cấu hình** (disabled khi không hợp lệ). Toast "Đã lưu cấu hình."

### 5.13 Thành phần dùng chung & quy ước

| Thành phần | Hành vi |
|---|---|
| `Badge` | tone neutral / success / warning / danger / info |
| `OrderStatusBadge` | Pending → vàng "Chưa lập tiến độ"; Completed → xanh "Hoàn thành"; chưa chốt → vàng "Chờ chốt"; quá hạn → đỏ "Quá hạn"; chưa tới ngày bắt đầu → xám "Chưa sản xuất"; còn lại → xanh dương "Đang sản xuất" (thứ tự ưu tiên đúng như liệt kê) |
| `ScheduleStatusBadge` | "✓ Hoàn thành" / "🔴 Chậm **n** đôi" / "🟢 Đúng tiến độ" |
| `DayStatusBadge` | NoPlan "Không sản xuất" · NotStarted "Chưa tới" · InProduction "Đang sản xuất" (ngày đã qua → vàng "Chưa xuất hàng") · Closed "Đã xuất hàng" |
| `Modal` | width tuỳ chỉnh (mặc định 560); khoá cuộn các tổ tiên; Escape chỉ đóng modal trên cùng; focus vào panel nếu bên trong chưa có focus |
| `ConfirmDialog` | 480 px; nút Huỷ + nút xác nhận (primary/danger), loading |
| `Toast` | góc màn hình, `aria-live=polite`, 5 giây, tone success/error/info |
| `LoadingState / ErrorState / EmptyState / InlineError` | thống nhất mọi màn |
| `Stepper` | bước done ✓ bấm được (nếu cho phép), active, todo |
| `Select` | combobox WAI-ARIA, phím mũi tên/Home/End/Enter/Escape, tự mở ngược lên khi thiếu chỗ |
| `DateInput` | lịch trình duyệt, hiển thị dd/mm/yyyy, bấm bất kỳ đâu mở lịch |
| `ProgressBar` | 0–100, tone |
| Responsive ≤ 900 px | sidebar ngang; lưới nhập hàng, day-stats, day-grid, form row về 1 cột; phân trang xếp dọc; modal padding 12 px |

---

## 6. Acceptance Criteria

| Mã | Tiêu chí |
|---|---|
| UX-AC01 | Mọi màn hình có dữ liệu server đều hiển thị được 3 trạng thái loading / error (có Thử lại) / empty. |
| UX-AC02 | Không màn hình nào hiển thị `0` cho số lượng chưa nhập; luôn là `—`. |
| UX-AC03 | Trong modal Nhập sản lượng, nhấn Enter chỉ ghi nhận; không thể xuất hàng từ trong modal. |
| UX-AC04 | Dialog Xuất hàng mở ra với focus ở **Quay lại**; Enter không chốt sổ. |
| UX-AC05 | Mọi thao tác phá huỷ/một chiều có hộp thoại xác nhận nêu rõ hệ quả với số liệu in đậm. |
| UX-AC06 | Wizard lập tiến độ không cho đi tiếp khi tổng phân bổ ≠ số lượng đơn hoặc tổng cột ≠ phân bổ dây chuyền; cột lệch được tô đỏ kèm số chênh. |
| UX-AC07 | Ma trận ghim cột ngày khi cuộn ngang; tiêu đề giữ đúng bề rộng cột dữ liệu. |
| UX-AC08 | Đơn quá hạn: toàn bộ nút ghi bị ẩn, nhãn trạng thái vẫn hiển thị. |
| UX-AC09 | Modal luôn vừa viewport: header/footer hiển thị, chỉ vùng danh sách cuộn. |
| UX-AC10 | Giao diện vẫn dùng được ở bề rộng 900 px (không tràn ngang trang). |
