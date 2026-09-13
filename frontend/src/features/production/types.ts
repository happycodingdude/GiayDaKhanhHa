import type { IsoDate } from '../../shared/lib/date'

/**
 * Trạng thái hiển thị của một ô sản xuất. Server suy ra và trả về; frontend không bao giờ tự tính
 * lại (CR-01 §14.3).
 */
export type DayStatus = 'NoPlan' | 'NotStarted' | 'InProduction' | 'Closed'

/** Ràng buộc nào đang chặn ô "Còn được nhập", để chọn đúng câu thông báo. */
export type RemainingAllowanceReason = 'DailyPlan' | 'OrderQuantity'

/** Một ô của ma trận: ngày × dây chuyền (CR-001 §6.7). */
export interface ProductionCellDto {
  /** Id của ProductionPlan — khoá của ô, và là id mà luồng bù sản lượng dùng. */
  id: string
  productionDate: IsoDate
  productionLineId: string
  initialPlannedQuantity: number
  addOnQuantity: number
  plannedQuantity: number
  dayStatus: DayStatus
  /** null nghĩa là chưa ghi nhận lần nào — tuyệt đối không render thành 0. */
  actualQuantity: number | null
  /** Ô còn mở: sản lượng là số tạm tính và còn tăng tiếp. */
  isProvisional: boolean
  productionDayId: string | null
  /** null khi ô chưa Xuất hàng — KHÔNG phải 0 (CR-01 N-07). */
  shortageQuantity: number | null
  difference: number | null
  closedAt: string | null
  hasActiveAdjustment: boolean
  activeAdjustmentId: string | null
  lastRecordedBy: string | null
  lastRecordedAt: string | null
}

export interface ProductionMatrixLineDto {
  id: string
  code: string
  name: string
  status: 'Active' | 'Inactive'
  sortOrder: number
  /** Mốc phân bổ tầng 1, bất biến sau khi lập tiến độ (BR-N18). */
  allocatedQuantity: number
  /** Kế hoạch hiện tại của cả dây chuyền — có thể lớn hơn `allocatedQuantity` sau khi bù. */
  currentPlanQuantity: number
  actualQuantity: number
}

export interface ProductionMatrixDto {
  orderId: string
  startDate: IsoDate | null
  dueDate: IsoDate | null
  productionLines: ProductionMatrixLineDto[]
  /** Trả phẳng theo ô; frontend dựng ma trận. Ô không có kế hoạch đơn giản là không xuất hiện. */
  items: ProductionCellDto[]
}

/** Một lần ghi nhận sản lượng trong ô. Tổng lũy kế do server tính (CR-01 §6.3). */
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
 * Toàn bộ state của một ô sản xuất. POST/PUT/DELETE entry cũng trả về đúng khuôn này, nên mutation
 * không cần thêm một vòng refetch (CR-01 §7.4).
 */
export interface ProductionCellDetailDto {
  orderId: string
  shoeCode: string
  productionDate: IsoDate
  productionLineId: string
  productionLineCode: string
  productionLineName: string
  dayStatus: DayStatus
  initialPlannedQuantity: number
  plannedQuantity: number
  addOnQuantity: number
  dayActualQuantity: number
  isProvisional: boolean
  remainingAllowance: number
  remainingAllowanceReason: RemainingAllowanceReason
  orderRemainingQuantity: number
  orderStatus: 'Pending' | 'Incomplete' | 'Completed'
  /** Đơn hàng đã qua ngày kết thúc thì bị đóng băng: chỉ xem lại, không thao tác. */
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

/** Một dây chuyền vừa được chốt sổ trong lần Xuất hàng cả ngày. */
export interface ClosedProductionCellDto {
  productionLineId: string
  productionLineCode: string
  productionLineName: string
  plannedQuantity: number
  actualQuantity: number
  shortageQuantity: number
  difference: number
}

/**
 * Kết quả Xuất hàng một ngày: mọi dây chuyền còn mở của ngày đó được chốt sổ cùng lúc.
 * `hasShortage` báo có phần thiếu cần xử lý (luôn false khi đơn đã hoàn thành — CR-01 §14.6).
 */
export interface CloseProductionDayDto {
  orderId: string
  productionDate: IsoDate
  closedAt: string
  cells: ClosedProductionCellDto[]
  orderStatus: 'Pending' | 'Incomplete' | 'Completed'
  orderCompleted: boolean
  hasShortage: boolean
}
