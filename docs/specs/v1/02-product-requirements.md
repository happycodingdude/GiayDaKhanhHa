# 02 — Product Requirements — Bản 1

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Trạng thái** | Draft (as-built) — chờ Chốt |
| **Nguồn** | Sinh lại ngày 09/10/2026 từ codebase (commit `74a9fc2`); bối cảnh từ tài liệu cũ. |
| **Role** | Principal Product Manager |
| **Spec đầu vào** | `01-business-discovery.md` |

---

## 1. Confirmed Requirements

### 1.1 Product vision

| Mã | Nội dung |
|---|---|
| PR-C01 | **Một màn hình thay cho một file Excel**: quản lý sản xuất nhìn vào là biết hôm nay xưởng làm được bao nhiêu, đơn nào chậm, ngày nào quên chốt sổ, và xử lý phần thiếu trong vài cú bấm. |
| PR-C02 | Giá trị cốt lõi: (1) kế hoạch rõ theo ngày × dây chuyền, (2) ghi nhận nhanh nhiều lần trong ngày, (3) chốt sổ bất biến, (4) cảnh báo đúng (không báo thiếu giả), (5) bù thiếu có kiểm soát và có lịch sử. |
| PR-C03 | Sản phẩm **không** là ERP, không quản lý vật tư, nhân công, chi phí, size/màu. |

### 1.2 Target user & MVP scope

| Mã | Nội dung |
|---|---|
| PR-C04 | Một persona duy nhất: **Quản lý sản xuất** (tài khoản `manager` do hệ thống khởi tạo). |
| PR-C05 | MVP (Bản 1) gồm đúng 10 feature ở §5.1, tất cả đã triển khai. |

### 1.3 Product boundaries

| Mã | Nội dung |
|---|---|
| PR-C06 | Một người dùng, một xưởng, một múi giờ (Asia/Ho_Chi_Minh), desktop. |
| PR-C07 | Mọi số liệu tổng hợp đều **suy ra** từ dữ liệu gốc; không có số nào do người dùng "nhập tay vào báo cáo". |
| PR-C08 | Hệ thống **không bao giờ tự thay đổi kế hoạch**; mọi thay đổi kế hoạch sau khi chốt chỉ đến từ thao tác bù thiếu do quản lý xác nhận. |
| PR-C09 | Dữ liệu đã chốt (xuất hàng, chốt tiến độ, bù đã áp dụng) là **bất biến**; "sửa" chỉ có dạng hoàn tác có lịch sử. |

---

## 2. Assumptions

| Mã | Giả định |
|---|---|
| PR-A01 | Số đơn đồng thời đang chạy nhỏ (dưới vài chục); danh sách phân trang 10/20/50 là đủ. |
| PR-A02 | Quản lý mở app trên một máy; không có hai người cùng thao tác một ô (hệ thống vẫn khoá để an toàn). |
| PR-A03 | Thứ tự ưu tiên tính năng dưới đây phản ánh thứ tự phụ thuộc kỹ thuật và tần suất dùng; chưa được chủ dự án xếp hạng lại bằng lời. |

---

## 3. Open Questions

| Mã | Câu hỏi |
|---|---|
| PR-Q01 | Dashboard có cần hiển thị các khối **Cảnh báo chậm**, **Hôm nay**, **Đang sản xuất hôm nay**, **Ngày chưa xuất hàng**, **Thiếu chưa xử lý** mà API đã tính sẵn nhưng giao diện chưa dùng không? (ứng viên Bản 2) |
| PR-Q02 | Danh sách Tiến độ có cần cột "Hôm nay" (kế hoạch/thực tế hôm nay) mà API đã trả không? |

---

## 4. Decisions

| Mã | Quyết định | Vì sao |
|---|---|---|
| PR-D01 | Nhập hàng (bước "hàng về") và Tiến độ (bước "lập lịch & theo dõi") là **hai mục điều hướng riêng**; danh sách Nhập hàng vẫn hiện mọi đơn kể cả đã lập tiến độ. | Mã giày và ảnh mẫu thuộc bước nhập hàng; quản lý cần tìm theo mã giày ở cả hai bước. |
| PR-D02 | Ghi nhận sản lượng nằm trong **modal** mở từ ma trận chi tiết, không phải trang riêng. | Người dùng không rời khỏi bức tranh toàn cảnh của đơn khi nhập số 8–10 lần/ngày. |
| PR-D03 | Dashboard là nơi **nhìn**, không phải nơi **bắt đầu hành động** (trừ Nhập hàng). | Giữ dashboard gọn; mọi thao tác đã có một nơi duy nhất là chi tiết tiến độ. |
| PR-D04 | Lịch sử hiển thị trên giao diện chỉ gồm **lịch sử bù**; vết sửa/xoá lần ghi nhận được lưu nhưng chỉ thể hiện bằng nhãn "Đã sửa". | Giữ Bản 1 gọn; lịch sử thao tác tổng hợp là ứng viên Bản 2. |
| PR-D05 | Cấu hình chỉ có **chu kỳ ghi nhận** và **nhắc trước**; giờ bắt đầu/kết thúc ca (từng được đề xuất) đã **bỏ**. | Không có nghiệp vụ nào dùng tới giờ ca; nhắc theo khoảng cách từ lần ghi nhận gần nhất là đủ. |

---

## 5. Detailed Specification

### 5.1 Feature list & priority (as-built)

| # | Feature | Mô tả ngắn | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| F-01 | Đăng nhập / phiên làm việc | Username + mật khẩu, cookie 12 giờ trượt, đăng xuất | Must | Done |
| F-02 | Danh mục dây chuyền | Thêm/sửa/bật/tắt; không xoá; chặn tắt khi đang có đơn sản xuất | Must | Done |
| F-03 | Nhập hàng | Tạo/sửa/xoá đơn (mã giày, số lượng, ảnh mẫu) | Must | Done |
| F-04 | Lập / sửa / xoá / chốt tiến độ | Wizard 5 bước, phân bổ hai tầng, chốt một chiều | Must | Done |
| F-05 | Ghi nhận sản lượng trong ngày | Nhiều lần/ô, sửa/xoá khi ô mở, trần "còn được nhập", nhắc chu kỳ | Must | Done |
| F-06 | Xuất hàng | Chốt sổ cả ngày, tính trạng thái đơn, báo thiếu | Must | Done |
| F-07 | Xử lý sản lượng thiếu | Option 1 / Option 2, xem trước → xác nhận, hoàn tác, lịch sử | Must | Done |
| F-08 | Theo dõi tiến độ | Danh sách, chi tiết ma trận, thống kê lũy kế, nhãn trạng thái/tình trạng | Must | Done |
| F-09 | Dashboard | 5 số tổng, timeline tháng / danh sách, khối chờ chốt | Should | Done |
| F-10 | Cấu hình nhắc ghi nhận | Chu kỳ 5–480 phút, nhắc trước | Could | Done |

### 5.2 User journeys

**J1 — Một lô hàng từ lúc về tới lúc xong**

1. Quản lý vào *Nhập hàng* → *+ Nhập hàng* → nhập mã giày, số lượng, kéo ảnh → *Nhập hàng*. Đơn hiện "Chưa lập tiến độ".
2. Bấm *Lập tiến độ* → wizard: chọn dây chuyền & ngày → hệ thống chia đều, quản lý chỉnh nếu cần → ma trận theo ngày → xem lại → *Tạo tiến độ*. Đơn hiện "Chờ chốt".
3. Kiểm tra lại ma trận → *Chốt tiến độ*. Đơn hiện "Chưa sản xuất" (chưa tới ngày) rồi "Đang sản xuất".
4. Mỗi ngày: mở chi tiết → *Nhập SL* từng dây chuyền nhiều lần → cuối ngày *Xuất hàng* → nếu có ô thiếu: *Xử lý thiếu*.
5. Ngày cuối xuất hàng đủ → đơn "Hoàn thành".

**J2 — Buổi sáng của quản lý**

1. Mở *Dashboard*: thấy số đơn đang chạy / đang chậm / hoàn thành; timeline tháng với chấm "!" ở ngày hôm qua chưa xuất hàng.
2. Bấm vào đơn → chi tiết hiện cảnh báo "n ngày đã qua chưa xuất hàng" → xuất hàng bù cho hôm qua → xử lý thiếu nếu có.

**J3 — Phát hiện chọn nhầm ngày bù**

1. Trong *Lịch sử bù sản lượng* bấm *Hoàn tác* → xác nhận → kế hoạch các ngày đích giảm lại.
2. Ô thiếu lại hiện nút *Xử lý thiếu* → bù lại đúng ngày.

**J4 — Dây chuyền ngừng hoạt động**

1. *Cấu hình → Dây chuyền sản xuất* → *Ngừng*. Nếu dây chuyền đang có đơn sản xuất, nút bị khoá kèm lý do.
2. Dây chuyền ngừng biến mất khỏi lựa chọn khi lập tiến độ mới; khi sửa tiến độ chưa chốt, hệ thống báo dây chuyền ngừng bị bỏ và yêu cầu phân bổ lại.

### 5.3 Success criteria của sản phẩm

- Quản lý hoàn thành chu trình J1 mà không cần tài liệu hướng dẫn.
- Không có thời điểm nào dashboard báo "thiếu" cho ngày đang sản xuất.
- Mọi con số trên dashboard khớp với tổng của chi tiết các đơn (vì cùng một cách tính ở backend).

---

## 6. Acceptance Criteria

| Mã | Tiêu chí |
|---|---|
| PR-AC01 | 10 feature ở §5.1 có thể thực hiện trọn vẹn từ giao diện, không cần gọi API thủ công. |
| PR-AC02 | Journey J1 chạy được từ đầu đến cuối với dữ liệu mẫu (`mock-data/scenarios.json` phủ đủ các trạng thái). |
| PR-AC03 | Mỗi feature có đủ trạng thái loading / lỗi / rỗng / thành công trên giao diện. |
| PR-AC04 | Không feature nào cho phép thay đổi số liệu đã chốt mà không để lại lịch sử. |
