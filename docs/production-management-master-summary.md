# Production Management Web App — Business & Product Specification
## Master Summary — Bản đã chốt

> **Vai trò:** Chuyên gia Phân tích Nghiệp vụ (BA) + Tư vấn Giải pháp Sản phẩm  
> **Mục tiêu:** Đây là bản tổng hợp tiếp nối các nội dung đã phân tích/chốt. Không phân tích lại từ đầu và không tự ý thay đổi các business rule đã chốt.

---

# 1. Bối cảnh

Doanh nghiệp gia công giày dép thủ công, hiện quản lý sản lượng bằng Excel.

Hệ thống cần thay thế Excel bằng một web app đơn giản để quản lý **tiến độ hoàn thành đơn hàng sản xuất**.

Mục tiêu:

- Lập kế hoạch sản xuất theo ngày.
- Ghi nhận sản lượng thực tế cuối ngày.
- Theo dõi tiến độ đơn hàng.
- Cảnh báo khi chậm tiến độ.
- Hỗ trợ quản lý điều chỉnh kế hoạch khi phát sinh sản lượng thiếu.

**Không xây dựng ERP hoặc hệ thống quản lý sản xuất phức tạp ở giai đoạn 1.**

---

# 2. Phạm vi giai đoạn 1

## Người dùng

- Chỉ có 1 quản lý sử dụng.
- Sử dụng trên máy tính.
- Hệ thống yêu cầu network/internet.
- Tương lai có thể mở rộng nhân viên và phân quyền.

## Quy mô

- Hiện tại: 1 ngày chỉ sản xuất 1 đơn hàng.
- Tương lai: có thể nhiều đơn hàng trong cùng một ngày.
- Nghiệp vụ không được thiết kế cứng khiến việc mở rộng sau này khó khăn.

## Sản phẩm

Chỉ quản lý **tổng số đôi cần hoàn thành**.

Không quản lý:

- Mẫu giày.
- Loại giày.
- Size.
- Màu.

---

# 3. Đối tượng trung tâm — Đơn hàng

Một đơn hàng gồm:

- Mã đơn hàng.
- Tổng số lượng cần hoàn thành.
- Ngày bắt đầu.
- Ngày cần hoàn thành.
- Kế hoạch sản xuất theo từng ngày.
- Sản lượng thực tế theo từng ngày.
- Trạng thái.

## Trạng thái

Chỉ có:

1. **Chưa hoàn thành**
2. **Hoàn thành**

Quản lý không tự đổi trạng thái.

Khi tổng sản lượng thực tế đạt đúng tổng số lượng đơn hàng:

> Đơn hàng → **Hoàn thành**

Sau khi hoàn thành, không được nhập thêm sản lượng làm tổng thực tế vượt số lượng đơn.

---

# 4. Quy tắc số lượng — Business Rule quan trọng

## 4.1. Tổng kế hoạch ban đầu

Khi lập kế hoạch ban đầu:

> **Tổng kế hoạch = Tổng số lượng đơn hàng**

Ví dụ:

- Đơn hàng: 1.000 đôi.
- Tổng kế hoạch ban đầu: 1.000 đôi.

## 4.2. Tổng thực tế

Tổng thực tế **không bao giờ được vượt tổng số lượng đơn hàng**.

Ví dụ:

- Đơn hàng: 1.000.
- Đã hoàn thành: 950.
- Nhập thêm 60.

Hệ thống từ chối vì chỉ còn:

> 1.000 - 950 = 50 đôi.

Chỉ cho phép nhập tối đa 50.

Khi nhập đúng 50:

> 950 + 50 = 1.000

→ Tự động chuyển **Hoàn thành**.

## 4.3. Kế hoạch sau khi bù thiếu

Đây là điểm đã chốt quan trọng:

**Kế hoạch ban đầu có tổng bằng tổng đơn, nhưng sau khi điều chỉnh bù, tổng kế hoạch cuối có thể lớn hơn tổng đơn.**

Lý do:

- Add-on chỉ là phần kế hoạch được dồn sang ngày khác.
- Không được giảm kế hoạch của các ngày khác để “cân” lại.
- Tổng thực tế mới là đại lượng bị giới hạn bởi tổng đơn.

---

# 5. Theo dõi tiến độ

Hệ thống tự tính:

- Kế hoạch của ngày.
- Thực tế của ngày.
- Chênh lệch ngày.
- Kế hoạch lũy kế.
- Thực tế lũy kế.
- Số lượng còn lại.
- Số ngày còn lại.
- Tình trạng tiến độ.

Ví dụ:

| Ngày | Kế hoạch | Thực tế | KH lũy kế | TT lũy kế |
|---|---:|---:|---:|---:|
| 11/08 | 100 | 80 | 100 | 80 |
| 12/08 | 120 | 130 | 220 | 210 |
| 13/08 | 200 | - | 420 | 210 |

Dashboard phải giúp quản lý nhìn nhanh:

- Đơn nào đúng tiến độ.
- Đơn nào đang chậm.
- Chậm bao nhiêu đôi.
- Đã hoàn thành bao nhiêu.
- Còn bao nhiêu.
- Còn bao nhiêu ngày.

---

# 6. Các màn hình đã chốt

## Màn hình 1 — Danh sách / Tổng quan đơn hàng

Mục đích:

- Quản lý nhanh toàn bộ đơn hàng.
- Biết trạng thái và tiến độ.
- Truy cập vào chi tiết đơn hàng.

Thông tin quan trọng:

- Mã đơn hàng.
- Tổng số lượng.
- Đã hoàn thành.
- Còn lại.
- Tiến độ.
- Deadline.
- Số ngày còn lại.
- Trạng thái.
- Cảnh báo chậm tiến độ.

---

## Màn hình 2 — Tạo đơn hàng

Quản lý nhập:

- Mã đơn hàng.
- Tổng số lượng.
- Ngày bắt đầu.
- Hạn hoàn thành.

Sau khi tạo đơn, đơn hàng ở trạng thái:

> **Chưa hoàn thành**

Sau đó chuyển sang bước lập kế hoạch.

---

## Màn hình 3 — Lập kế hoạch sản xuất

Quản lý phân bổ tổng số lượng đơn hàng vào từng ngày.

Ví dụ:

| Ngày | Kế hoạch |
|---|---:|
| Ngày 1 | 100 |
| Ngày 2 | 120 |
| Ngày 3 | 200 |
| Ngày 4 | 250 |
| Ngày 5 | 330 |
| **Tổng** | **1.000** |

Rule:

> Tổng kế hoạch ban đầu phải bằng tổng số lượng đơn hàng.

Đây là kế hoạch gốc để hệ thống theo dõi tiến độ.

---

## Màn hình 4 — Chi tiết / Theo dõi tiến độ đơn hàng

Hiển thị toàn bộ tình trạng của một đơn hàng:

### Thông tin tổng quan

- Mã đơn.
- Tổng số lượng.
- Đã hoàn thành.
- Còn lại.
- Tiến độ.
- Deadline.
- Số ngày còn lại.
- Trạng thái.

### Bảng kế hoạch và thực tế

| Ngày | KH ban đầu | Add-on | KH hiện tại | Thực tế | Chênh lệch |
|---|---:|---:|---:|---:|---:|

Màn hình phải thể hiện rõ:

- Kế hoạch gốc.
- Phần Add-on do bù thiếu.
- Kế hoạch hiện tại.
- Thực tế.
- Chênh lệch.

### Lịch sử điều chỉnh

Các lần điều chỉnh kế hoạch phải có thể xem lại.

---

## Màn hình 5 — Nhập sản lượng cuối ngày

Mục đích:

> Cho quản lý nhập nhanh số lượng thực tế hoàn thành.

Thông tin:

- Ngày.
- Đơn hàng.
- Số lượng thực tế.

### Rule đã chốt

**Kế hoạch của ngày = 0 thì không được nhập thực tế, kể cả nhập 0.**

Nếu ngày đó không có kế hoạch:

> Không cho ghi nhận sản lượng thực tế trên ngày đó.

### Nếu thực tế < kế hoạch

Ví dụ:

- Kế hoạch: 100.
- Thực tế: 80.
- Thiếu: 20.

Hệ thống:

- Ghi nhận 80.
- Tính thiếu 20.
- Hiển thị cảnh báo chậm tiến độ.

**Không bắt buộc quản lý phải xử lý thiếu ngay.**

Thiếu chỉ là cảnh báo; quản lý có thể xử lý sau.

### Sửa sản lượng

Có thể sửa sản lượng đã nhập theo rule số lượng chung.

Mọi phép tính tiến độ phải cập nhật theo giá trị thực tế mới.

---

# 7. Màn hình 6 — Xử lý sản lượng thiếu

Đây là nghiệp vụ quan trọng nhất sau bước nhập thực tế.

Ví dụ:

- Kế hoạch: 100.
- Thực tế: 80.
- Thiếu: 20.

Hệ thống phát hiện:

> Đơn hàng đang thiếu 20 đôi so với kế hoạch.

Quản lý được lựa chọn cách xử lý.

Có 2 option đã chốt:

1. **Option 1 — Chọn ngày để bù**
2. **Option 2 — Hệ thống đề xuất chia đều**

---

# 8. Màn hình 6 — Option 1: Chọn ngày để bù

## Mục đích

Cho quản lý tự chọn **một ngày nhận toàn bộ phần thiếu**.

Ví dụ:

> Thiếu 20 đôi → chọn ngày 12/08 để bù.

Kế hoạch:

| Ngày | KH ban đầu | Add-on | KH mới |
|---|---:|---:|---:|
| 11/08 | 100 | - | 100 |
| 12/08 | 120 | +20 | 140 |
| 13/08 | 200 | - | 200 |
| 14/08 | 250 | - | 250 |
| 15/08 | 330 | - | 330 |

## Business rules đã chốt

### Rule 1 — Bù toàn bộ

Không cho quản lý nhập số lượng bù tùy ý.

Nếu thiếu 20:

> Add-on phải là +20.

### Rule 2 — Chỉ ngày được chọn mới cộng Add-on

Không tự động phân bổ hoặc thay đổi các ngày khác.

### Rule 3 — Không giảm kế hoạch của các ngày còn lại

Đây là rule đã chốt rõ:

> **Không giảm kế hoạch của các ngày còn lại.**

Chỉ khi quản lý xác nhận bù vào ngày nào:

> Ngày đó được **+ Add-on**.

### Rule 4 — Tổng kế hoạch cuối có thể lớn hơn tổng đơn

Ví dụ tổng kế hoạch ban đầu = 1.000.

Sau khi có Add-on:

> Tổng kế hoạch hiện tại có thể > 1.000.

Điều này **không phải lỗi**.

Add-on thể hiện việc dồn phần sản lượng chưa hoàn thành sang một ngày khác.

### Rule 5 — Tổng thực tế vẫn không được vượt tổng đơn

Dù tổng kế hoạch sau điều chỉnh có thể lớn hơn tổng đơn:

> **Tổng thực tế tuyệt đối không được vượt tổng số lượng đơn hàng.**

### Rule 6 — Không tự động áp dụng

Flow bắt buộc:

> Phát hiện thiếu  
> → Hiển thị thiếu bao nhiêu  
> → Quản lý chọn ngày  
> → Preview kế hoạch mới  
> → Confirmation  
> → Xác nhận  
> → Apply

Chỉ sau khi xác nhận mới thay đổi kế hoạch.

### Rule 7 — Không chọn ngày đã qua

Ngày nhận bù là ngày hiện tại hoặc ngày tương lai.

Không cho chọn ngày đã qua vì việc bù là điều chỉnh kế hoạch cho thời gian còn lại và không được làm sai lệch lịch sử.

---

# 9. Màn hình 6 — Option 2: Hệ thống đề xuất chia đều

## Mục đích

Thay vì tự chọn một ngày, quản lý có thể yêu cầu hệ thống đề xuất cách phân bổ phần thiếu.

Ví dụ:

- Thiếu: 20 đôi.
- Còn: 4 ngày.

Hệ thống đề xuất:

> +5 / +5 / +5 / +5

Nếu:

> Thiếu 23 / 4 ngày

Đề xuất:

> +6 / +6 / +6 / +5

## Rule đã chốt

Phân bổ vào **các ngày liên tiếp còn lại**.

Ví dụ còn 4 ngày:

> Ngày 12 → Ngày 13 → Ngày 14 → Ngày 15

Không bỏ qua ngày ở giữa.

## Option 2 chỉ là đề xuất

Hệ thống **không được tự động áp dụng**.

Flow:

> Phát hiện thiếu  
> → Chọn Option 2  
> → Hệ thống tính đề xuất  
> → Hiển thị kế hoạch mới  
> → Quản lý xem lại  
> → Xác nhận  
> → Apply

---

# 10. Quy tắc phân bổ khi không chia hết

Nếu số lượng thiếu không chia hết cho số ngày còn lại, hệ thống phải phân bổ hợp lý.

Ví dụ:

> 23 đôi / 4 ngày

Có thể:

> +6 / +6 / +6 / +5

Tổng Add-on luôn phải đúng:

> 6 + 6 + 6 + 5 = 23

Không được làm mất hoặc tạo thêm sản lượng.

---

# 11. Nguyên tắc chung của 2 Option

Cả Option 1 và Option 2 đều tuân thủ:

### Không tự quyết định thay quản lý

Hệ thống chỉ:

- phát hiện thiếu;
- tính toán;
- đưa ra lựa chọn/đề xuất;
- preview kết quả.

Quản lý là người quyết định cuối cùng.

### Không làm thay đổi lịch sử

Không điều chỉnh kế hoạch của ngày đã qua.

### Không vượt tổng đơn ở sản lượng thực tế

> Tổng thực tế <= Tổng số lượng đơn.

### Không làm tăng số lượng đơn

Add-on không tạo thêm sản lượng cần giao.

Nó chỉ là cách **phân bổ lại phần sản lượng chưa hoàn thành vào kế hoạch tương lai**.

---

# 12. Workflow tổng thể đã chốt

```text
Tạo đơn hàng
    ↓
Lập kế hoạch ban đầu
    ↓
Sản xuất
    ↓
Nhập sản lượng cuối ngày
    ↓
Hệ thống tính tiến độ
    ↓
Đạt kế hoạch?
    ├── Có → Đúng tiến độ
    │
    └── Không
          ↓
       Cảnh báo thiếu
          ↓
    Quản lý xử lý khi phù hợp
          ↓
    ┌────────────────────────┐
    │                        │
    ▼                        ▼
Option 1                 Option 2
Chọn ngày bù             Đề xuất chia đều
    │                        │
    └──────────┬─────────────┘
               ↓
            Preview
               ↓
          Quản lý xác nhận
               ↓
             Apply
               ↓
       Theo dõi kế hoạch mới
               ↓
      Tổng thực tế đạt tổng đơn
               ↓
          Hoàn thành
```

---

# 13. Dashboard

Dashboard phải giúp quản lý nhìn vào là hiểu ngay tình trạng sản xuất.

Các thông tin chính:

- Tổng đơn đang theo dõi.
- Đơn đang đúng tiến độ.
- Đơn đang chậm.
- Đơn đã hoàn thành.
- Tổng sản lượng đã hoàn thành.
- Tổng sản lượng còn lại.
- Cảnh báo chậm tiến độ.

Ưu tiên:

> **Thông tin quan trọng phải nhìn thấy ngay, không yêu cầu người dùng đi qua nhiều màn hình.**

---

# 14. Lịch sử điều chỉnh

Mỗi lần áp dụng điều chỉnh kế hoạch phải có lịch sử.

Tối thiểu cần thể hiện:

- Thời điểm điều chỉnh.
- Phần thiếu được xử lý.
- Option đã sử dụng.
- Ngày nhận Add-on.
- Kế hoạch trước.
- Kế hoạch sau.

Mục tiêu:

> Có thể giải thích vì sao kế hoạch của một ngày tăng lên.

---

# 15. Các nguyên tắc sản phẩm đã chốt

1. **Đơn giản.**
2. **Dễ dùng cho người không rành công nghệ.**
3. **Nhập liệu nhanh.**
4. **Dashboard nhìn vào là hiểu.**
5. **Không tự động quyết định thay quản lý.**
6. **Cảnh báo nhưng để quản lý quyết định.**
7. **Không xây dựng ERP phức tạp khi chưa cần.**
8. **Có khả năng mở rộng tương lai.**
9. **Không làm sai lệch lịch sử.**
10. **Không cho tổng thực tế vượt tổng số lượng đơn.**

---

# 16. Khả năng mở rộng tương lai

Chưa triển khai ở giai đoạn 1 nhưng nghiệp vụ không được khóa cứng:

- Nhiều người dùng.
- Phân quyền quản lý/nhân viên.
- Nhiều đơn hàng trong cùng một ngày.
- Lưu người thực hiện thay đổi.
- Audit log.
- Báo cáo nâng cao.
- Export Excel.
- Các chức năng quản lý sản xuất khác.

---

# 17. Trạng thái dự án

## Đã chốt

- Mô hình đơn hàng.
- Trạng thái đơn hàng.
- Lập kế hoạch theo ngày.
- Nhập sản lượng thực tế.
- Quy tắc không vượt tổng đơn.
- Tự động chuyển Hoàn thành.
- Cảnh báo thiếu.
- Không bắt buộc xử lý thiếu ngay.
- Màn hình và workflow chính.
- Màn hình 5 — Nhập sản lượng cuối ngày.
- Màn hình 6 — Option 1.
- Màn hình 6 — Option 2.
- Preview → Confirmation → Apply.
- Không giảm kế hoạch các ngày khác khi bù.
- Add-on chỉ cộng vào ngày được xác nhận.
- Tổng kế hoạch sau điều chỉnh có thể > tổng đơn.
- Tổng thực tế không được > tổng đơn.
- Option 2 phân bổ vào các ngày liên tiếp còn lại.

## Chưa đi vào

- API.
- Database schema.
- Authentication implementation.
- Framework/technology.
- Deployment.
- Technical architecture.

Các nội dung technical chỉ được quyết định **sau khi nghiệp vụ và UI/UX được chốt**.

---

# 18. Bước tiếp theo

Phase nghiệp vụ chính đã được tổng hợp.

Bước tiếp theo phù hợp là:

> **Hoàn thiện UI/UX cho toàn bộ các màn hình đã chốt → review flow end-to-end → chốt UI/UX → sau đó mới chuyển sang thiết kế technical và triển khai.**

