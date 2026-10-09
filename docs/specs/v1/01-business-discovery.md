# 01 — Business Discovery — Bản 1

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Trạng thái** | Draft (as-built) — chờ Chốt |
| **Nguồn** | Sinh lại ngày 09/10/2026 từ codebase (commit `74a9fc2`) và bối cảnh nghiệp vụ trong bộ tài liệu cũ (`docs/phantich.md`, `production-management-master-summary.md`, `CR-01`, `CR-001`). Code là nguồn sự thật cho hành vi; tài liệu cũ chỉ cung cấp "vì sao". |
| **Role** | Principal Business Analyst + Chuyên gia nghiệp vụ sản xuất giày |

> Bản 1 đã được triển khai xong trước khi bộ spec này tồn tại (dự án đi sai quy trình từ đầu). Spec này ghi lại **bài toán nghiệp vụ mà hệ thống hiện tại đang giải**, để các bản sau có điểm xuất phát đúng.

---

## 1. Confirmed Requirements

### 1.1 Bối cảnh doanh nghiệp

| Mã | Nội dung |
|---|---|
| BD-C01 | Doanh nghiệp là **xưởng gia công giày dép thủ công**, nhiều dây chuyền sản xuất, hiện theo dõi sản lượng bằng Excel. |
| BD-C02 | Vấn đề cần giải: Excel không giúp **lập kế hoạch theo ngày**, **ghi nhận sản lượng thực tế trong ngày**, **phát hiện chậm tiến độ** và **điều chỉnh kế hoạch khi thiếu**. |
| BD-C03 | Mục tiêu: một web app đơn giản thay Excel để **quản lý tiến độ hoàn thành đơn hàng sản xuất**. Không xây ERP. |

### 1.2 Người dùng

| Mã | Nội dung |
|---|---|
| BD-C04 | **Đúng một người dùng**: quản lý sản xuất. Là người nhập hàng, lập tiến độ, ghi nhận sản lượng, xuất hàng, xử lý thiếu và xem dashboard. |
| BD-C05 | Làm việc trên **máy tính** trong xưởng, có mạng. Không yêu cầu mobile. |
| BD-C06 | Người dùng **không rành công nghệ**: giao diện tiếng Việt, nhập liệu nhanh, nhìn là hiểu. |

### 1.3 Đối tượng nghiệp vụ

| Mã | Nội dung |
|---|---|
| BD-C07 | **Đơn hàng** được nhận diện bằng **mã giày** (duy nhất toàn xưởng) và **số lượng đôi** cần hoàn thành. Mỗi đơn có tối đa **một ảnh mẫu** để nhận diện. Không quản lý mẫu, loại, size, màu. |
| BD-C08 | Xưởng có nhiều **dây chuyền sản xuất** (mã, tên, thứ tự ưu tiên, ghi chú). Dây chuyền có thể ngừng hoạt động nhưng **không bao giờ bị xoá** vì lịch sử sản xuất tham chiếu tới nó. |
| BD-C09 | Một đơn hàng chạy trên **một hoặc nhiều dây chuyền**, trong **một khoảng ngày** (ngày bắt đầu → ngày kết thúc). Đơn vị nhỏ nhất của kế hoạch và sản lượng là **ô sản xuất** = (đơn hàng, ngày, dây chuyền). |
| BD-C10 | Mỗi ô có **kế hoạch** (số đôi dự kiến) và **sản lượng thực tế** (tổng các lần ghi nhận trong ngày). Ô có kế hoạch 0 nghĩa là dây chuyền nghỉ ngày đó. |

### 1.4 Hai thời điểm nghiệp vụ tách biệt

| Mã | Nội dung |
|---|---|
| BD-C11 | **Nhập hàng**: hàng về xưởng, biết mã giày, số lượng, có ảnh mẫu. Lúc này **chưa biết** chạy dây chuyền nào, bắt đầu ngày nào. |
| BD-C12 | **Lập tiến độ**: quản lý quyết định dây chuyền, khoảng ngày, và phân bổ **hai tầng**: đơn → dây chuyền, rồi dây chuyền → từng ngày. Tổng phân bổ phải bằng đúng số lượng đơn. |
| BD-C13 | **Chốt tiến độ**: sau khi lập xong, quản lý kiểm tra lại rồi **chốt**. Chưa chốt thì còn sửa/xoá tiến độ được nhưng **chưa được sản xuất**; chốt rồi thì tiến độ bất biến và bắt đầu ghi nhận sản lượng. Chốt là một chiều. |

### 1.5 Vận hành hằng ngày

| Mã | Nội dung |
|---|---|
| BD-C14 | Trong ngày, quản lý **ghi nhận sản lượng nhiều lần** cho từng ô (theo chu kỳ, ví dụ mỗi giờ). Mỗi lần ghi nhận là một số cộng thêm, có thể kèm ghi chú. Trước khi chốt sổ, các lần ghi nhận được sửa/xoá tự do (không cần lý do) nhưng hệ thống vẫn lưu vết. |
| BD-C15 | Cuối ngày, quản lý **Xuất hàng** = chốt sổ cả ngày cho mọi dây chuyền của đơn. Sau xuất hàng, sản lượng ngày đó bất biến: không sửa, không xoá, không mở lại. Ô chưa ghi nhận gì mà xuất hàng thì sản lượng = 0. |
| BD-C16 | Tổng sản lượng ghi nhận trong một ô **không được vượt kế hoạch của ô**; tổng thực tế toàn đơn **không được vượt số lượng đơn**. |
| BD-C17 | Trạng thái đơn chỉ được đánh giá lúc xuất hàng: đủ số lượng → **Hoàn thành**; chưa đủ → **Chưa hoàn thành**. Quản lý không tự đặt trạng thái. |
| BD-C18 | Sản lượng của ô chưa xuất hàng là **số tạm tính**: dashboard và báo cáo phải gắn nhãn, và **không được báo "thiếu"** cho ô chưa chốt sổ. |

### 1.6 Xử lý sản lượng thiếu

| Mã | Nội dung |
|---|---|
| BD-C19 | **Phần thiếu** chỉ tồn tại ở ô đã xuất hàng: thiếu = kế hoạch − thực tế (nếu dương). Thiếu là cảnh báo, quản lý có thể xử lý sau, không bắt buộc ngay. |
| BD-C20 | Hệ thống **không tự quyết thay quản lý**: mọi phương án bù đều phải **xem trước rồi xác nhận** mới áp dụng. |
| BD-C21 | Hai cách bù: **Option 1 — Chọn ngày để bù**: quản lý chọn một ngày sau đó (cùng dây chuyền) nhận toàn bộ phần thiếu. **Option 2 — Hệ thống chia đều**: phần thiếu chia đều cho mọi ngày còn lại của cùng dây chuyền, phần dư dồn vào ngày gần nhất. Không cho nhập số lượng bù tuỳ ý, không cho chỉnh kết quả chia. |
| BD-C22 | Bù **chỉ cộng thêm** vào kế hoạch ngày đích, **không giảm** kế hoạch ngày khác, **không đổi** số lượng đơn. Tổng kế hoạch sau bù có thể lớn hơn số lượng đơn, đó không phải lỗi. |
| BD-C23 | Không bù vào ngày đã qua, ngày đã xuất hàng, hay dây chuyền khác. Nếu dây chuyền không còn ngày nào nhận được, hệ thống báo rõ. |
| BD-C24 | Một lần bù đã áp dụng là **lịch sử bất biến**; muốn sửa thì **hoàn tác** rồi bù lại. Mỗi ô thiếu tại một thời điểm chỉ có **một** lần bù đang áp dụng. |

### 1.7 Theo dõi và cảnh báo

| Mã | Nội dung |
|---|---|
| BD-C25 | **Chậm tiến độ** = kế hoạch lũy kế của các ô đã tới hạn lớn hơn thực tế lũy kế của chúng. "Chậm" là **tình trạng**, không phải trạng thái đơn. Tiến độ chưa chốt không bị đánh giá chậm. |
| BD-C26 | **Quá hạn** = đã qua ngày kết thúc mà chưa hoàn thành. Đơn đã qua ngày kết thúc (kể cả đã hoàn thành) bị **đóng băng**: chỉ xem, không ghi thêm gì. Ngoại lệ duy nhất: tiến độ chưa chốt vẫn sửa/xoá được để dời ngày. |
| BD-C27 | Dashboard phải cho quản lý nhìn ra: đơn nào đang chạy, đơn nào chậm, hôm nay làm được bao nhiêu, ngày nào đã qua mà chưa xuất hàng, đơn nào đang chờ chốt tiến độ. |
| BD-C28 | Hệ thống **nhắc** quản lý ghi nhận theo chu kỳ cấu hình được; nhắc **không chặn** thao tác nào. |

### 1.8 Ràng buộc nghiệp vụ

| Mã | Nội dung |
|---|---|
| BD-C29 | **Không làm sai lệch lịch sử**: không sửa dữ liệu đã chốt, không xoá dây chuyền đã dùng, không xoá đơn đã lập tiến độ, mọi thay đổi có vết người/thời điểm. |
| BD-C30 | Dữ liệu nghiệp vụ tính theo **ngày lịch tại Việt Nam** (Asia/Ho_Chi_Minh). |
| BD-C31 | Đơn chưa lập tiến độ có thể **xoá hẳn** (chưa có dữ liệu sản xuất). Số lượng đơn chỉ đổi được khi chưa lập tiến độ. |

---

## 2. Assumptions

| Mã | Giả định | Lý do đánh dấu |
|---|---|---|
| BD-A01 | Mỗi đơn hàng chỉ có **một** tiến độ trong suốt vòng đời (xoá tiến độ chưa chốt rồi lập lại vẫn là một tiến độ). | Suy từ mô hình dữ liệu; tài liệu cũ ghi "ngoài phạm vi: nhiều tiến độ/đơn". |
| BD-A02 | Năng suất dây chuyền (đôi/ngày) chưa cần quản lý; quản lý tự ước lượng khi chia kế hoạch. | Code không có trường năng suất; tài liệu cũ ghi là giai đoạn sau. |
| BD-A03 | Sản lượng vượt kế hoạch ngày không xảy ra trong nghiệp vụ (hệ thống chặn). | Quyết định từ CR-01; chưa có xác nhận lại của chủ dự án trong bộ spec mới. |
| BD-A04 | Lý do nghiệp vụ của bước **Chốt tiến độ** và luật **chặn ngừng dây chuyền đang có đơn sản xuất** là: tránh sản xuất theo một kế hoạch còn đang sửa, và tránh đơn đang chạy mất chỗ sản xuất. | Hai luật này có trong code (commit `74a9fc2`) nhưng **không có tài liệu nào** mô tả; lý do suy từ comment trong code. |

---

## 3. Open Questions

| Mã | Câu hỏi | Ảnh hưởng |
|---|---|---|
| BD-Q01 | Bước **Chốt tiến độ** có đúng ý nghĩa nghiệp vụ như BD-C13 không? Có trường hợp nào cần "bỏ chốt" không? | Hiện chốt là một chiều, không có đường lùi ngoài việc đơn hoàn thành/quá hạn. |
| BD-Q02 | Khi dây chuyền **đang có đơn sản xuất**, có tình huống thực tế nào cần ngừng khẩn cấp (hỏng máy) không? | Hiện bị chặn hoàn toàn; muốn ngừng phải chờ đơn xong hoặc quá hạn. |
| BD-Q03 | Đơn **quá hạn chưa hoàn thành** trong thực tế được xử lý thế nào (gia hạn? huỷ? làm tiếp ngoài hệ thống)? | Hiện đơn bị đóng băng vĩnh viễn, không có thao tác gia hạn hay huỷ. |
| BD-Q04 | Có cần **xem được lịch sử thao tác** đầy đủ của một đơn (tạo, lập tiến độ, chốt, ghi nhận, sửa, xuất hàng, bù) trên giao diện không? | Hiện chỉ xem được lịch sử bù; vết sửa/xoá lần ghi nhận có lưu nhưng không hiển thị. Ứng viên Bản 2. |

---

## 4. Decisions

| Mã | Quyết định đã thể hiện trong hệ thống | Vì sao |
|---|---|---|
| BD-D01 | Tách **Nhập hàng** và **Lập tiến độ** thành hai bước, hai màn hình. | Hàng về trước, lịch lập sau; lúc nhập hàng chưa có thông tin dây chuyền/ngày. |
| BD-D02 | Thêm bước **Chốt tiến độ** giữa lập và sản xuất. | Kế hoạch lập xong cần được kiểm tra lại; sản xuất theo kế hoạch còn đang sửa gây lệch số liệu. |
| BD-D03 | Ghi nhận sản lượng **nhiều lần trong ngày** và **Xuất hàng** cuối ngày thay vì một con số cuối ngày. | Khớp cách xưởng vận hành (ghi theo chu kỳ); số liệu trong ngày là tạm tính nên cần một thời điểm chốt. |
| BD-D04 | Xuất hàng chốt sổ **cả ngày** (mọi dây chuyền của đơn cùng lúc), không chốt từng dây chuyền. | Một thao tác cuối ngày thay vì n thao tác; ngày đã xuất hàng là trạng thái của ngày. |
| BD-D05 | Bù sản lượng thiếu chỉ trong **cùng dây chuyền**. | Giữ Bản 1 đơn giản; bù chéo dây chuyền là ứng viên bản sau. |
| BD-D06 | **Không** cho quản lý nhập số lượng bù tuỳ ý ở cả hai option. | Option 1 là "chuyển toàn bộ phần thiếu sang ngày khác"; Option 2 là "để hệ thống chia". |
| BD-D07 | Đơn qua ngày kết thúc bị **đóng băng** kể cả khi đã hoàn thành. | Yếu tố quyết định là lịch, không phải trạng thái; kỳ sản xuất đã kết thúc thì dữ liệu phải giữ nguyên. |
| BD-D08 | Dây chuyền chỉ **bật/tắt**, không xoá; không tắt được khi đang có đơn sản xuất. | Lịch sử tham chiếu dây chuyền; đơn đang chạy không được mất chỗ sản xuất. |
| BD-D09 | Trạng thái đơn chỉ gồm **Chưa lập tiến độ / Chưa hoàn thành / Hoàn thành**; các nhãn "Chờ chốt", "Chưa sản xuất", "Đang sản xuất", "Quá hạn" là **suy ra** từ ngày và cờ chốt. | Tránh lưu trạng thái có thể lệch với lịch. |

---

## 5. Detailed Specification

### 5.1 Vòng đời một đơn hàng (nghiệp vụ)

```text
Hàng về ──▶ NHẬP HÀNG ──▶ [Chưa lập tiến độ] ──▶ LẬP TIẾN ĐỘ ──▶ [Chờ chốt] ──▶ CHỐT ──▶ [Chưa sản xuất | Đang sản xuất]
                              │ xoá đơn                 ▲   │ sửa / xoá tiến độ                 │
                              ▼                         └───┘                                   │ mỗi ngày: ghi nhận nhiều lần → XUẤT HÀNG
                           (hết)                                                                │ nếu thiếu → XỬ LÝ THIẾU (Option 1 / 2)
                                                                                                ▼
                                                                 [Hoàn thành]  hoặc  [Quá hạn — đóng băng]
```

### 5.2 Thuật ngữ nghiệp vụ

| Thuật ngữ | Định nghĩa |
|---|---|
| Mã giày | Định danh đơn hàng, duy nhất toàn xưởng, tối đa 50 ký tự. |
| Số lượng | Tổng số đôi phải hoàn thành; > 0; khoá sau khi lập tiến độ. |
| Ảnh mẫu | Tối đa một ảnh JPG/PNG/WEBP ≤ 5 MB cho mỗi đơn. |
| Dây chuyền | Đơn vị sản xuất; có mã (≤ 30), tên (≤ 100), thứ tự, ghi chú; Đang hoạt động / Ngừng hoạt động. |
| Tiến độ | Dây chuyền + khoảng ngày + phân bổ hai tầng của một đơn. |
| Phân bổ (tầng 1) | Số đôi giao cho mỗi dây chuyền; tổng = số lượng đơn; mỗi dây chuyền được chọn phải > 0. |
| Kế hoạch ngày (tầng 2) | Số đôi của một ô; tổng theo dây chuyền = phân bổ của dây chuyền đó; 0 = nghỉ. |
| Kế hoạch ban đầu / Kế hoạch hiện tại | Số lúc lập (bất biến sau chốt) / số sau khi cộng các khoản bù. |
| Chốt tiến độ | Thời điểm tiến độ trở thành bất biến và đơn được phép sản xuất. |
| Lần ghi nhận | Một số đôi cộng thêm (> 0) vào một ô, kèm thời điểm, người ghi, ghi chú tuỳ chọn. |
| Còn được nhập | Min(kế hoạch ô − đã ghi nhận trong ô, số lượng đơn − tổng thực tế toàn đơn). |
| Xuất hàng | Chốt sổ cả ngày: sản lượng chính thức của từng ô = tổng các lần ghi nhận; một chiều. |
| Tạm tính | Số liệu của ô/ngày chưa xuất hàng. |
| Thiếu | Kế hoạch − thực tế của ô **đã xuất hàng**, nếu dương. Ô chưa xuất hàng: chưa có thiếu. |
| Bù (Option 1 / Option 2) | Cộng phần thiếu vào kế hoạch của ngày sau, cùng dây chuyền; xem trước rồi xác nhận. |
| Hoàn tác | Gỡ một khoản bù đang áp dụng, giữ lịch sử. |
| Chậm tiến độ | Kế hoạch lũy kế đã tới hạn − thực tế lũy kế tương ứng > 0 (số đôi chậm). |
| Quá hạn | Chưa hoàn thành và hôm nay đã qua ngày kết thúc. |
| Đóng băng | Đơn qua ngày kết thúc: chỉ xem. |
| Chu kỳ ghi nhận | Số phút giữa hai lần ghi nhận mà hệ thống dùng để nhắc (5–480, mặc định 60), có thể nhắc trước 15 phút. |

### 5.3 Use case chính

| # | Use case | Tác nhân | Kết quả |
|---|---|---|---|
| UC-01 | Đăng nhập | Quản lý | Vào được hệ thống bằng tài khoản được cấp. |
| UC-02 | Cấu hình danh mục dây chuyền | Quản lý | Thêm/sửa/bật/tắt dây chuyền. |
| UC-03 | Nhập hàng | Quản lý | Đơn ở trạng thái Chưa lập tiến độ, có mã giày, số lượng, ảnh. |
| UC-04 | Sửa / xoá đơn nhập hàng | Quản lý | Sửa mã, ảnh (mọi lúc, trừ quá hạn); sửa số lượng và xoá (chỉ khi chưa lập tiến độ). |
| UC-05 | Lập tiến độ | Quản lý | Đơn có dây chuyền, khoảng ngày, kế hoạch từng ô; trạng thái Chờ chốt. |
| UC-06 | Sửa / xoá / chốt tiến độ | Quản lý | Sửa hoặc xoá khi chưa chốt; chốt để bắt đầu sản xuất. |
| UC-07 | Ghi nhận sản lượng trong ngày | Quản lý | Thêm/sửa/xoá lần ghi nhận trong ô còn mở. |
| UC-08 | Xuất hàng | Quản lý | Chốt sổ cả ngày; đơn có thể chuyển Hoàn thành; biết ô nào thiếu. |
| UC-09 | Xử lý sản lượng thiếu | Quản lý | Áp dụng Option 1 hoặc 2 sau khi xem trước; hoàn tác được. |
| UC-10 | Theo dõi tiến độ | Quản lý | Danh sách tiến độ, chi tiết ma trận ngày × dây chuyền, thống kê lũy kế, lịch sử bù. |
| UC-11 | Xem dashboard | Quản lý | Tổng quan toàn xưởng và timeline các đơn đang theo dõi. |
| UC-12 | Cấu hình nhắc ghi nhận | Quản lý | Đặt chu kỳ và có/không nhắc trước. |

### 5.4 Những gì Bản 1 cố tình không làm

Nhiều người dùng / phân quyền; năng suất dây chuyền; bù chéo dây chuyền; nhiều ảnh/đơn; nhiều tiến độ/đơn; huỷ đơn; xoá dây chuyền; mở lại ngày đã xuất hàng; điều chỉnh kế hoạch chủ động sau khi chốt; lịch sử thao tác tổng hợp trên giao diện; xuất Excel; báo cáo theo kỳ; thông báo; mobile; quản lý nguyên vật liệu/kho/máy móc/nhân công. Danh sách này là đầu vào cho `01-business-discovery.md` Bản 2 (`docs/specs/v2/`).

---

## 6. Acceptance Criteria

| Mã | Tiêu chí (ở mức nghiệp vụ) |
|---|---|
| BD-AC01 | Quản lý nhập được một lô hàng mới với mã giày, số lượng, ảnh trong một thao tác và thấy nó ở trạng thái Chưa lập tiến độ. |
| BD-AC02 | Quản lý lập được tiến độ cho đơn trên nhiều dây chuyền với tổng phân bổ đúng bằng số lượng đơn, và không thể lưu nếu lệch. |
| BD-AC03 | Trước khi chốt, quản lý sửa hoặc xoá được tiến độ; sau khi chốt thì không, và chỉ sau khi chốt mới ghi nhận được sản lượng. |
| BD-AC04 | Trong một ngày, quản lý ghi nhận được nhiều lần vào một ô, không vượt kế hoạch ô và không vượt số lượng đơn; sửa/xoá được trước khi xuất hàng. |
| BD-AC05 | Sau Xuất hàng, số liệu ngày đó không đổi được; phần thiếu (nếu có) xuất hiện và có thể xử lý bằng Option 1 hoặc Option 2 sau khi xem trước; hoàn tác được. |
| BD-AC06 | Đơn đủ số lượng tự chuyển Hoàn thành ngay lúc xuất hàng; quản lý không tự đặt trạng thái. |
| BD-AC07 | Đơn qua ngày kết thúc chỉ xem được; đơn chậm tiến độ hiện số đôi chậm; ô chưa xuất hàng không bao giờ bị báo thiếu. |
| BD-AC08 | Dây chuyền ngừng hoạt động không xuất hiện khi lập tiến độ mới nhưng dữ liệu cũ vẫn hiển thị; không ngừng được dây chuyền đang có đơn sản xuất. |
