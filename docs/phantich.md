# Project: Production Management Web App

## 1. Bối cảnh

Doanh nghiệp gia công giày dép thủ công và hiện đang quản lý sản lượng bằng Excel.

Cần xây dựng một web app đơn giản để quản lý **tiến độ hoàn thành đơn hàng sản xuất**.

Mục tiêu là thay thế Excel bằng một hệ thống dễ sử dụng, tập trung vào việc:

- Lập kế hoạch sản xuất theo ngày.
- Ghi nhận sản lượng thực tế cuối ngày.
- Theo dõi tiến độ đơn hàng.
- Cảnh báo khi chậm tiến độ.
- Hỗ trợ quản lý điều chỉnh kế hoạch khi có sản lượng thiếu.

Không xây dựng ERP hoặc hệ thống quản lý sản xuất phức tạp ở giai đoạn đầu.

---

## 2. Phạm vi giai đoạn 1

### Người dùng

- Chỉ có **1 quản lý** sử dụng.
- Sử dụng trên **máy tính**.
- Hệ thống bắt buộc có network/internet.
- Tương lai có thể mở rộng thêm nhân viên và phân quyền.

### Quy mô sản xuất

- Hiện tại: **1 ngày chỉ sản xuất 1 đơn hàng**.
- Tương lai có thể mở rộng thành **1 ngày nhiều đơn hàng**.
- Vì vậy không thiết kế nghiệp vụ quá cứng theo kiểu "mỗi ngày chỉ có một đơn".

### Sản phẩm

Không quan tâm:

- mẫu giày
- loại giày
- size
- màu

Chỉ quản lý **tổng số đôi cần hoàn thành**.

---

## 3. Khái niệm chính

Đối tượng trung tâm của hệ thống là **Đơn hàng**.

Một đơn hàng có:

- Mã đơn hàng.
- Tổng số lượng cần hoàn thành.
- Ngày bắt đầu.
- Ngày cần hoàn thành.
- Kế hoạch sản xuất theo từng ngày.
- Sản lượng thực tế theo từng ngày.
- Trạng thái.

### Trạng thái đơn hàng

Chỉ có 2 trạng thái:

1. Chưa hoàn thành
2. Hoàn thành

Khi tổng sản lượng thực tế đạt đúng tổng số lượng đơn hàng, hệ thống tự động chuyển sang **Hoàn thành**.

Quản lý không cần tự đổi trạng thái.

---

# 4. Use case chính

Ví dụ:

Có đơn hàng:

> 1.000 đôi giày
> Thời gian hoàn thành: 5 ngày

Quản lý lập kế hoạch:

| Ngày   | Kế hoạch |
| ------ | -------: |
| Ngày 1 |      100 |
| Ngày 2 |      120 |
| Ngày 3 |      200 |
| Ngày 4 |      250 |
| Ngày 5 |      330 |
| Tổng   |    1.000 |

Cuối mỗi ngày quản lý nhập số lượng thực tế.

Ví dụ ngày 1:

- Kế hoạch: 100
- Thực tế: 80
- Thiếu: 20

Hệ thống phải phát hiện và cảnh báo:

> Đơn hàng đang chậm 20 đôi so với kế hoạch.

---

# 5. Theo dõi tiến độ

Hệ thống cần tính tự động:

- Kế hoạch của ngày.
- Thực tế của ngày.
- Chênh lệch giữa kế hoạch và thực tế.
- Kế hoạch lũy kế.
- Thực tế lũy kế.
- Số lượng còn lại.
- Số ngày còn lại.
- Tình trạng tiến độ.

Ví dụ:

| Ngày  | Kế hoạch | Thực tế | KH lũy kế | TT lũy kế |
| ----- | -------: | ------: | --------: | --------: |
| 11/08 |      100 |      80 |       100 |        80 |
| 12/08 |      120 |     130 |       220 |       210 |
| 13/08 |      200 |       - |       420 |       210 |

Dashboard phải giúp quản lý nhanh chóng biết:

- Đơn hàng nào đang đúng tiến độ.
- Đơn hàng nào đang chậm.
- Đang chậm bao nhiêu đôi.
- Đã hoàn thành bao nhiêu.
- Còn bao nhiêu.
- Còn bao nhiêu ngày.

---

# 6. Xử lý khi chậm tiến độ

Đây là nghiệp vụ quan trọng.

Khi thực tế thấp hơn kế hoạch, hệ thống **không tự động thay đổi kế hoạch**.

Ví dụ:

- Kế hoạch: 100
- Thực tế: 80
- Thiếu: 20

Hệ thống cảnh báo và cho quản lý lựa chọn cách xử lý.

## Option 1 — Chọn ngày để bù

Quản lý chọn:

> Bù 20 đôi vào ngày 12/08.

Hệ thống cập nhật kế hoạch:

| Ngày  | KH ban đầu | Add-on | KH mới |
| ----- | ---------: | -----: | -----: |
| 11/08 |        100 |      - |    100 |
| 12/08 |        120 |    +20 |    140 |
| 13/08 |        200 |      - |    200 |
| 14/08 |        250 |      - |    250 |
| 15/08 |        330 |      - |    330 |

Tổng đơn hàng vẫn là 1.000 đôi.

Add-on chỉ có nghĩa là **chuyển phần sản lượng chưa hoàn thành sang ngày khác**, không làm tăng tổng số lượng đơn hàng.

## Option 2 — Hệ thống đề xuất chia đều

Ví dụ:

- Thiếu: 20 đôi.
- Còn: 4 ngày.

Hệ thống đề xuất:

> +5 đôi/ngày trong 4 ngày còn lại.

Ví dụ:

| Ngày  | KH ban đầu | Add-on | KH mới |
| ----- | ---------: | -----: | -----: |
| 11/08 |        100 |      - |    100 |
| 12/08 |        120 |     +5 |    125 |
| 13/08 |        200 |     +5 |    205 |
| 14/08 |        250 |     +5 |    255 |
| 15/08 |        330 |     +5 |    335 |

Nếu số lượng không chia hết, hệ thống phải phân bổ hợp lý.

Ví dụ:

> Thiếu 23 đôi / 4 ngày

Có thể đề xuất:

> +6 / +6 / +6 / +5

### Quan trọng

Option 2 chỉ là **đề xuất**.

Flow phải là:

> Phát hiện chậm → Hiển thị số lượng thiếu → Quản lý chọn cách xử lý → Hệ thống đưa ra kế hoạch mới → Quản lý xem lại → Xác nhận → Mới áp dụng.

Không được tự động thay đổi kế hoạch khi chưa có xác nhận của quản lý.

---

# 7. Quy tắc số lượng

Không cho phép tổng sản lượng thực tế vượt tổng số lượng của đơn hàng.

Ví dụ:

Đơn hàng: 1.000 đôi
Đã hoàn thành: 950 đôi

Quản lý nhập thêm 60 đôi:

> 950 + 60 = 1.010

Hệ thống phải từ chối và báo:

> Số lượng hoàn thành không thể vượt quá số lượng của đơn hàng. Đơn hàng còn lại: 50 đôi.

Chỉ cho nhập tối đa 50 đôi.

Khi đạt đúng:

> 950 + 50 = 1.000

→ Tự động chuyển đơn hàng sang:

> **Hoàn thành**

---

# 8. Workflow tổng thể

## Bước 1 — Tạo đơn hàng

Quản lý nhập:

- Mã đơn hàng.
- Tổng số lượng.
- Ngày bắt đầu.
- Hạn hoàn thành.

## Bước 2 — Lập kế hoạch

Quản lý phân bổ tổng số lượng cần hoàn thành vào từng ngày.

Tổng kế hoạch phải bằng tổng số lượng đơn hàng.

## Bước 3 — Sản xuất

Xưởng thực hiện theo kế hoạch.

## Bước 4 — Cuối ngày

Quản lý nhập số lượng thực tế hoàn thành.

## Bước 5 — Hệ thống đánh giá

Nếu đạt:

> Hiển thị đạt tiến độ.

Nếu thiếu:

> Cảnh báo chậm tiến độ.

## Bước 6 — Xử lý thiếu

Quản lý chọn:

- Chọn một ngày cụ thể để bù.
- Hoặc sử dụng đề xuất chia đều cho các ngày còn lại.

## Bước 7 — Xác nhận

Quản lý xác nhận kế hoạch điều chỉnh.

## Bước 8 — Tiếp tục theo dõi

Hệ thống tiếp tục tính toán tiến độ dựa trên kế hoạch mới.

## Bước 9 — Hoàn thành

Khi tổng thực tế đạt tổng số lượng:

> Đơn hàng → Hoàn thành.

---

# 9. Giao diện dự kiến

Chưa cần quyết định UI chi tiết, nhưng hệ thống dự kiến có:

### Dashboard

Hiển thị:

- Tổng đơn đang theo dõi.
- Đơn đang đúng tiến độ.
- Đơn đang chậm.
- Đơn đã hoàn thành.
- Sản lượng đã hoàn thành.
- Sản lượng còn lại.
- Cảnh báo chậm tiến độ.

### Danh sách đơn hàng

Cho phép:

- xem đơn hàng.
- xem trạng thái.
- xem tiến độ.
- mở chi tiết.

### Chi tiết đơn hàng

Hiển thị:

- thông tin đơn hàng.
- tổng số lượng.
- tiến độ tổng thể.
- kế hoạch từng ngày.
- thực tế từng ngày.
- add-on.
- lịch sử điều chỉnh kế hoạch.

### Nhập sản lượng cuối ngày

Form đơn giản:

- ngày.
- đơn hàng.
- số lượng thực tế.

Hiện tại vì mỗi ngày chỉ có 1 đơn nên thao tác phải thật nhanh.

---

# 10. Khả năng mở rộng tương lai

Không triển khai ngay nhưng kiến trúc nghiệp vụ phải có khả năng hỗ trợ:

- nhiều người dùng.
- phân quyền quản lý/nhân viên.
- nhiều đơn hàng trong cùng một ngày.
- lịch sử người thực hiện thay đổi.
- audit log.
- báo cáo nâng cao.
- export Excel.
- các chức năng quản lý sản xuất khác nếu doanh nghiệp phát triển.

Không được vì giai đoạn đầu đơn giản mà thiết kế dữ liệu quá cứng.

---

# 11. Nguyên tắc phát triển

Ưu tiên:

1. Đơn giản.
2. Dễ sử dụng cho người không rành công nghệ.
3. Nhập liệu nhanh.
4. Dashboard nhìn vào là hiểu ngay tình trạng sản xuất.
5. Không tự động quyết định thay quản lý.
6. Cảnh báo nhưng để quản lý quyết định.
7. Không xây dựng ERP phức tạp khi chưa cần.
8. Có khả năng mở rộng trong tương lai.

---

# 12. Điểm cần tiếp tục ở lần sau

Khi quay lại, **không cần phân tích lại từ đầu**.

Bước tiếp theo là:

### Phase tiếp theo: Thiết kế nghiệp vụ + UI/UX

Cần thực hiện theo thứ tự:

1. Xác định toàn bộ màn hình.
2. Xác định workflow của từng màn hình.
3. Thiết kế Dashboard.
4. Thiết kế màn hình tạo đơn hàng.
5. Thiết kế màn hình lập kế hoạch.
6. Thiết kế màn hình nhập sản lượng cuối ngày.
7. Thiết kế cảnh báo chậm tiến độ.
8. Thiết kế flow xử lý thiếu:
   - chọn ngày bù
   - tự động đề xuất chia đều

9. Thiết kế lịch sử điều chỉnh.
10. Sau khi nghiệp vụ/UI được chốt mới lựa chọn technology và bắt đầu triển khai.
