# Dữ liệu mẫu

Bộ dữ liệu để xem đủ mọi trạng thái màn hình mà không phải tự nhập tay.

---

## Chạy

```bash
# 1. Database
docker compose up -d

# 2. Backend (chọn sẵn mật khẩu cho lần khởi tạo đầu tiên)
Bootstrap__Password='<mật-khẩu-của-bạn>' dotnet run --project backend/src/ProductionManagement.Api --urls http://localhost:5080

# 3. Nạp dữ liệu mẫu
PM_PASSWORD='<mật-khẩu-của-bạn>' node mock-data/seed.mjs

# 4. Frontend
cd frontend && npm run dev
```

Biến môi trường tuỳ chọn: `PM_API` (mặc định `http://localhost:5080`),
`PM_USERNAME` (mặc định `manager`).

---

## Nạp lại từ đầu

Đơn hàng **không có API xoá** — lịch sử sản xuất là bất biến theo thiết kế đã chốt.
Vì vậy muốn nạp lại bộ dữ liệu mẫu thì phải xoá sạch database:

```bash
docker compose down -v && docker compose up -d
```

Rồi khởi động lại backend (migration + tài khoản quản lý được tạo lại) và chạy `seed.mjs`.

Nếu mã đơn đã tồn tại, script sẽ dừng lại và nhắc bạn thay vì ghi đè.

---

## Vì sao nạp qua API chứ không phải file SQL

Script gọi đúng các endpoint thật thay vì `INSERT` thẳng vào PostgreSQL. Nhờ đó:

- Mọi business rule đều được kiểm tra thật khi tạo dữ liệu — dữ liệu mẫu chắc chắn hợp lệ,
  không phải dữ liệu "vẽ tay" có thể vi phạm bất biến.
- Các lần bù sản lượng đi qua đúng luồng `preview → apply → (reverse)`, nên
  `plan_adjustments` / `plan_adjustment_items` và `planned_quantity` luôn khớp nhau.
- Các cột audit `created_by` / `applied_by` / `reversed_by` được gán từ user đăng nhập,
  không phải id bịa ra.
- Bản thân việc chạy script cũng là một lượt kiểm thử end-to-end của API.

---

## Ngày tháng

Mọi ngày trong `scenarios.json` là **độ lệch tương đối so với hôm nay**
(`dayOffset: -1` = hôm qua, `0` = hôm nay, `2` = ngày kia).

Nghĩa là bộ dữ liệu luôn hợp lệ dù bạn chạy vào ngày nào — không bao giờ bị "hết hạn",
và các ngày nhận bù luôn nằm trong tương lai đúng như rule nghiệp vụ.

---

## 8 đơn hàng trong bộ dữ liệu

| Mã đơn | Tình huống | Dùng để kiểm tra |
|---|---|---|
| `ORD-2026-001` | Đúng tiến độ, đã sản xuất đủ 3 ngày đầu | Badge 🟢 Đúng tiến độ; không xuất hiện trong cảnh báo Dashboard |
| `ORD-2026-002` | Chậm 30 đôi, **chưa xử lý thiếu** | Nút *Xử lý thiếu*; khối ⚠ Cần xử lý trên Dashboard |
| `ORD-2026-003` | Đã bù **Option 1** (chọn 1 ngày) | Cột *Bù thêm* `+20`; lịch sử "Đang áp dụng"; nút *Hoàn tác* |
| `ORD-2026-004` | Bù **Option 2** rồi hoàn tác, sau đó bù lại thủ công | Lịch sử có cả mục *Đã hoàn tác* và *Đang áp dụng* — chứng minh lịch sử bất biến |
| `ORD-2026-005` | **Hoàn thành** (thực tế = tổng đơn) | Badge Hoàn thành, tiến độ 100%, ẩn nút nhập mới |
| `ORD-2026-006` | **Quá hạn** nhưng chưa xong | Badge đỏ *Quá hạn* ở danh sách và chi tiết |
| `ORD-2026-007` | Chưa nhập sản lượng nào (bắt đầu ngày mai) | Timeline hiển thị `—` chứ **không phải `0`** |
| `ORD-2026-008` | Có ngày nghỉ, **kế hoạch = 0** | Ngày đó bị chặn nhập sản lượng, kể cả nhập `0` |

`ORD-2026-004` là đơn đáng xem nhất: nó tái hiện đúng ví dụ trong tài liệu —
thiếu 23 đôi chia đều cho 4 ngày còn lại thành **+6 / +6 / +6 / +5**.

---

## Muốn thêm tình huống của riêng bạn

Sửa `scenarios.json`, không cần đụng vào `seed.mjs`.

```json
{
  "orderCode": "ORD-2026-009",
  "label": "Mô tả ngắn",
  "startOffset": -1,
  "plan": [100, 200, 300],
  "actuals": [{ "dayOffset": -1, "quantity": 80 }],
  "adjustments": [
    { "type": "Manual", "sourceDayOffset": -1, "targetDayOffset": 1 }
  ]
}
```

- `plan` — kế hoạch từng ngày, bắt đầu từ `startOffset`. Tổng số lượng đơn được tính
  tự động bằng tổng mảng này nên không bao giờ lệch.
- `adjustments[].type` — `"Manual"` (Option 1, cần `targetDayOffset`) hoặc
  `"Automatic"` (Option 2, hệ thống tự chia).
- `adjustments[].thenReverse` — đặt `true` để hoàn tác ngay sau khi bù, dùng khi muốn
  tạo dữ liệu lịch sử.

Lưu ý rule nghiệp vụ khi tự thêm: ngày nhận bù phải **sau ngày phát sinh thiếu** và
**không được là ngày quá khứ**.
