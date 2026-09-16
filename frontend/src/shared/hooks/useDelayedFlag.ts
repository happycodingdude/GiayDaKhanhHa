import { useEffect, useState } from 'react'

/**
 * `true` chỉ khi `active` đã kéo dài quá `delayMs`, và về `false` ngay khi `active` tắt.
 *
 * Dùng cho trạng thái "đang lưu": request chạy local chỉ mất vài chục ms, mà nút vẫn chuyển sang
 * spinner (rộng ra, đẩy nút bên cạnh) và vùng xung quanh bị làm mờ, rồi trở lại ngay — giao diện nháy
 * lên như bị lỗi. Request chậm hơn ngưỡng thì trạng thái chờ vẫn hiện như thường.
 */
export function useDelayedFlag(active: boolean, delayMs = 300): boolean {
  const [shown, setShown] = useState(false)

  useEffect(() => {
    if (!active) {
      setShown(false)
      return
    }

    const timer = window.setTimeout(() => setShown(true), delayMs)
    return () => window.clearTimeout(timer)
  }, [active, delayMs])

  return active && shown
}
