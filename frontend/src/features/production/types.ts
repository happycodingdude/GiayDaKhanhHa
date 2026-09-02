import type { IsoDate } from '../../shared/lib/date'

/**
 * Trạng thái hiển thị của một ngày sản xuất. Server suy ra và trả về; frontend không bao giờ tự
 * tính lại (CR-01 §14.3).
 */
export type DayStatus = 'NoPlan' | 'NotStarted' | 'InProduction' | 'Closed'

/** Ràng buộc nào đang chặn ô "Còn được nhập", để chọn đúng câu thông báo. */
export type RemainingAllowanceReason = 'DailyPlan' | 'OrderQuantity'

/** Một dòng của bảng sản xuất theo ngày ở màn hình chi tiết đơn hàng. */
export interface ProductionDayDto {
  /** Id của ProductionPlan — vẫn là khoá của dòng, và là id mà luồng bù sản lượng dùng. */
  id: string
  productionDate: IsoDate
  initialPlannedQuantity: number
  addOnQuantity: number
  plannedQuantity: number
  dayStatus: DayStatus
  /** null nghĩa là chưa ghi nhận lần nào — tuyệt đối không render thành 0. */
  actualQuantity: number | null
  /** Ngày còn mở: sản lượng là số tạm tính và còn tăng tiếp. */
  isProvisional: boolean
  productionDayId: string | null
  /** null khi ngày chưa Xuất hàng — KHÔNG phải 0 (CR-01 N-07). */
  shortageQuantity: number | null
  difference: number | null
  closedAt: string | null
  hasActiveAdjustment: boolean
  activeAdjustmentId: string | null
  lastRecordedBy: string | null
  lastRecordedAt: string | null
}

export interface ProductionPlanListDto {
  orderId: string
  items: ProductionDayDto[]
}

/** Một lần ghi nhận sản lượng trong ngày. Tổng lũy kế do server tính (CR-01 §6.3). */
export interface ProductionEntryDto {
  id: string
  quantity: number
  recordedAt: string
  note: string | null
  runningTotal: number
  isEdited: boolean
  recordedBy: string | null
}

/**
 * Toàn bộ state của một ngày sản xuất. POST/PUT/DELETE entry cũng trả về đúng khuôn này, nên
 * mutation không cần thêm một vòng refetch (CR-01 §7.4).
 */
export interface ProductionDayDetailDto {
  orderId: string
  orderCode: string
  productionDate: IsoDate
  dayStatus: DayStatus
  initialPlannedQuantity: number
  plannedQuantity: number
  addOnQuantity: number
  dayActualQuantity: number
  isProvisional: boolean
  remainingAllowance: number
  remainingAllowanceReason: RemainingAllowanceReason
  orderRemainingQuantity: number
  orderStatus: 'Incomplete' | 'Completed'
  /** Đơn hàng đã qua ngày hạn thì bị đóng băng: chỉ xem lại, không thao tác. */
  isOrderReadOnly: boolean
  lastRecordedAt: string | null
  closedAt: string | null
  closedBy: string | null
  shortageQuantity: number | null
  difference: number | null
  /** Mới nhất trên cùng. */
  entries: ProductionEntryDto[]
}

export interface CreateProductionEntryRequest {
  quantity: number
  note?: string | null
}

export interface UpdateProductionEntryRequest {
  quantity: number
  note?: string | null
}

/** Kết quả Xuất hàng. `hasShortage` mở luồng Xử lý thiếu ngay sau khi đóng ngày. */
export interface CloseProductionDayDto {
  orderId: string
  productionDate: IsoDate
  dayStatus: DayStatus
  plannedQuantity: number
  actualQuantity: number
  shortageQuantity: number
  difference: number
  closedAt: string
  orderStatus: 'Incomplete' | 'Completed'
  orderCompleted: boolean
  hasShortage: boolean
}
