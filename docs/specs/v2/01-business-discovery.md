# 01 — Business Discovery — Bản 2

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Trạng thái** | Draft — **chưa bắt đầu Phase 1**. File này chỉ là **danh sách ứng viên** gom từ những gì tài liệu cũ đã mô tả hoặc đánh dấu "giai đoạn sau" nhưng **chưa có trong code Bản 1**. Chưa có mục nào được Chốt. |
| **Nguồn** | Đối chiếu bộ tài liệu cũ (`docs/*.md`, đã xoá, còn trong git tại commit `74a9fc2`) với codebase ngày 09/10/2026. |
| **Role** | Principal Business Analyst + Chuyên gia nghiệp vụ sản xuất giày |

> Theo VERSION RULES: mỗi yêu cầu mới phải đi lại từ Phase 1. Khi bắt đầu Bản 2, chủ dự án chọn ứng viên nào đưa vào, rồi Phase 1 sẽ hỏi lại nghiệp vụ cho từng mục trước khi ghi thành Confirmed.

---

## 1. Confirmed Requirements

Chưa có.

---

## 2. Assumptions

| Mã | Giả định |
|---|---|
| B2-A01 | Bản 1 (`docs/specs/v1/`, `01` → `06`) đã được Chốt và là baseline; mọi mục dưới đây là **thay đổi so với Bản 1**, không phải mô tả lại. |
| B2-A02 | Các mục nhóm A từng được mô tả chi tiết trong tài liệu cũ nên có thể lấy lại làm điểm xuất phát, nhưng phải xác nhận lại vì nghiệp vụ đã đổi sau CR-01/CR-001. |

---

## 3. Open Questions (mỗi ứng viên là một câu hỏi cho Phase 1)

### Nhóm A — Đã mô tả trong tài liệu cũ nhưng chưa có trong code

| Mã | Ứng viên | Tài liệu cũ mô tả | Hiện trạng Bản 1 | Câu hỏi cho chủ dự án |
|---|---|---|---|---|
| B2-Q01 | **Lịch sử thao tác tổng hợp của đơn** (Activity History): tạo đơn, lập/sửa/chốt tiến độ, ghi nhận, sửa/xoá lần ghi nhận, xuất hàng, bù/hoàn tác; có người, thời điểm, trước/sau | `order-detail-screen-spec` §9, `production-quantity-entry-screen-spec` §12 | Chỉ hiển thị lịch sử bù; vết sửa lần ghi nhận có lưu (`production_entry_logs`) nhưng chưa hiển thị; không có log cho lập/chốt tiến độ, xuất hàng | Có cần xem được? Mức chi tiết nào? Có cần "lý do" không? |
| B2-Q02 | **Điều chỉnh kế hoạch chủ động** sau khi chốt (dời số giữa các ngày, giữ tổng = số lượng đơn, có lý do) | `create-order-production-plan-screen` §10–11, `order-detail-screen-spec` §4.9 | Sau chốt, kế hoạch chỉ đổi qua bù thiếu | Thực tế có cần đổi kế hoạch giữa chừng không? Khác gì với xoá tiến độ chưa chốt? |
| B2-Q03 | **Dashboard: khối Cảnh báo chậm, Hôm nay, Đang sản xuất hôm nay, Ngày chưa xuất hàng, Thiếu chưa xử lý** | `dashboard-screen-spec` §5–7, CR-01 §8 | API đã tính và trả (`alerts`, `today`, `todayProduction`, `unclosedPastCells`, `openShortages`) nhưng UI không hiển thị | Muốn hiển thị khối nào? Đặt ở đâu (quy ước: nối dưới cùng)? |
| B2-Q04 | **Cột "Hôm nay" trên danh sách Tiến độ** (KH/TT hôm nay) | CR-01 §8 (MH1) | DTO có `todayPlannedQuantity/todayActualQuantity`, bảng không hiển thị | Cần không? |
| B2-Q05 | **Giờ bắt đầu / kết thúc ca** trong cấu hình | CR-01 §5.4, §6.8 | Đã bị bỏ bởi migration `CR01SimplifySystemSettings` | Có nghiệp vụ nào cần giờ ca (ví dụ nhắc "chưa xuất hàng" sau giờ kết thúc)? |
| B2-Q06 | **Job dọn file ảnh mồ côi** | CR-001 §10 | Chỉ ghi log khi xoá file thất bại | Có cần không, hay chấp nhận file rác? |

### Nhóm B — Tài liệu cũ ghi "giai đoạn sau / ngoài phạm vi"

| Mã | Ứng viên | Nguồn | Câu hỏi cho chủ dự án |
|---|---|---|---|
| B2-Q07 | Nhiều người dùng, phân quyền quản lý / nhân viên nhập liệu, quản lý tài khoản | `phantich` §10, Master §16, S3 §2 | Ai sẽ dùng thêm? Nhân viên được làm gì (chỉ ghi nhận? xuất hàng?)? |
| B2-Q08 | Phân quyền theo dây chuyền | CR-001 §3 | Mỗi tổ trưởng chỉ thấy dây chuyền của mình? |
| B2-Q09 | Năng suất (đôi/ngày) của dây chuyền; chia theo năng lực/trọng số thay vì chia đều | CR-001 §2 #9, Option 2 spec §12 | Có số liệu năng suất ổn định không? |
| B2-Q10 | Bù chéo dây chuyền | CR-001 §2 #10 | Thực tế có chuyển việc giữa dây chuyền không? |
| B2-Q11 | Nhiều ảnh / thư viện ảnh cho một đơn | CR-001 §3 | Một ảnh có đủ nhận diện mẫu không? |
| B2-Q12 | Nhiều tiến độ cho một đơn (chia đợt) | CR-001 §3 | Có đơn chia nhiều đợt sản xuất không? |
| B2-Q13 | Huỷ đơn (trạng thái mới), gia hạn ngày kết thúc cho đơn quá hạn | S2 §13, `01` BD-Q03 | Đơn quá hạn chưa xong xử lý thế nào ngoài đời? |
| B2-Q14 | Mở lại ngày đã xuất hàng (có kiểm soát) | CR-01 §6.6 | Có khi nào chốt nhầm và cần sửa không? Chấp nhận quy trình hoàn tác bằng điều chỉnh thay vì mở lại? |
| B2-Q15 | Xoá dây chuyền chưa từng dùng | CR-001 §7.9 | Cần không, hay bật/tắt là đủ? |
| B2-Q16 | Xuất Excel; báo cáo theo tháng/quý | `phantich` §10, Dashboard spec §16 | Ai cần báo cáo, báo cáo gì, gửi cho ai? |
| B2-Q17 | Thông báo (email/Zalo/…): chậm tiến độ, quên xuất hàng | ImplPrompt §44 | Kênh nào? Ai nhận? |
| B2-Q18 | Giao diện mobile/tablet | S5 §34 | Có ai cần thao tác ngay tại dây chuyền bằng điện thoại không? |
| B2-Q19 | Quản lý mẫu/size/màu, nguyên vật liệu, kho, máy móc, nhân công, chi phí | `phantich` §2, Dashboard spec §16 | Giữ nguyên "không ERP"? |
| B2-Q20 | Test tự động trong repo, CI | `06` IP-Q01/Q02 | Có muốn không? |

### Nhóm C — Câu hỏi kỹ thuật kế thừa từ Bản 1 (không phải tính năng, nhưng cần quyết)

| Mã | Nội dung | Nguồn |
|---|---|---|
| B2-Q21 | Đồng bộ "hôm nay" giữa frontend và backend | `05` TA-Q01 |
| B2-Q22 | Có cho bù vào ô nghỉ (kế hoạch 0) không | `03` FS-Q01 |
| B2-Q23 | Dọn technical debt TD-01…TD-08 | `06` §5.7 |

---

## 4. Decisions

Chưa có.

---

## 5. Detailed Specification

Chưa có. Sẽ viết sau khi chủ dự án chọn ứng viên và Phase 1 Bản 2 hoàn tất.

---

## 6. Acceptance Criteria

Chưa có.
