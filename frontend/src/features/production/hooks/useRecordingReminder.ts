import { useEffect, useState } from 'react'

/** Lời nhắc đã sẵn sàng để hiển thị. `message` là null khi chưa cần nhắc gì. */
export interface RecordingReminder {
  message: string | null
  minutesSinceLastEntry: number | null
}

/** Nhắc trước bao lâu khi cấu hình bật "nhắc trước khi tới hạn". */
export const LEAD_MINUTES = 15

/**
 * Nhắc quản lý ghi nhận theo chu kỳ đã cấu hình. Thuần client: tính từ `lastRecordedAt` và cấu
 * hình, KHÔNG gọi API định kỳ và KHÔNG bao giờ chặn form — chu kỳ chỉ để nhắc (CR-01 §7.5, N-10).
 *
 * `remindBeforeDue` bật thì nhắc sớm vài phút trước khi tới hạn; tắt thì chỉ nhắc sau khi đã quá.
 */
export function useRecordingReminder(
  lastRecordedAt: string | null,
  intervalMinutes: number | undefined,
  remindBeforeDue: boolean,
  enabled: boolean,
): RecordingReminder {
  // Đồng hồ chỉ nhích mỗi phút: lời nhắc là một câu chữ, không phải bộ đếm giây.
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!enabled) return
    const timer = window.setInterval(() => setNow(Date.now()), 60_000)
    return () => window.clearInterval(timer)
  }, [enabled])

  const idle: RecordingReminder = { message: null, minutesSinceLastEntry: null }

  if (!enabled || !intervalMinutes || lastRecordedAt === null) return idle

  const recordedAt = new Date(lastRecordedAt).getTime()
  if (Number.isNaN(recordedAt)) return idle

  const minutes = Math.max(Math.floor((now - recordedAt) / 60_000), 0)

  if (minutes >= intervalMinutes) {
    return {
      message:
        `Đã ${minutes} phút kể từ lần ghi nhận gần nhất, quá chu kỳ ${intervalMinutes} phút. ` +
        'Nếu tổ đã sản xuất thêm, hãy ghi nhận để số liệu trong ngày luôn đúng.',
      minutesSinceLastEntry: minutes,
    }
  }

  if (remindBeforeDue && minutes >= intervalMinutes - LEAD_MINUTES) {
    return {
      message: `Còn ${intervalMinutes - minutes} phút nữa là tới hạn ghi nhận tiếp theo.`,
      minutesSinceLastEntry: minutes,
    }
  }

  return { message: null, minutesSinceLastEntry: minutes }
}
