/**
 * Cấu hình vận hành. Chu kỳ ghi nhận chỉ dùng để nhắc trên màn hình nhập sản lượng — nó không bao
 * giờ chặn thao tác, và không hồi tố dữ liệu đã ghi (CR-01 §6.8, N-10).
 */
export interface SystemSettingsDto {
  /** Bao lâu thì quản lý ghi nhận sản lượng một lần. */
  recordingIntervalMinutes: number
  /** Có nhắc trước khi tới hạn hay không. Tắt thì chỉ nhắc sau khi đã quá hạn. */
  remindBeforeDue: boolean
  updatedAt: string
}

export interface UpdateSystemSettingsRequest {
  recordingIntervalMinutes: number
  remindBeforeDue: boolean
}
