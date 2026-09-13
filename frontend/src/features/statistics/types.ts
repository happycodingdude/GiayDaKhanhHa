import type { IsoDate } from '../../shared/lib/date'
import type { ScheduleStatus } from '../orders/types'
import type { DayStatus } from '../production/types'

export interface DailyStatisticsDto {
  productionDate: IsoDate
  initialPlannedQuantity: number
  addOnQuantity: number
  plannedQuantity: number
  actualQuantity: number | null
  dayStatus: DayStatus
  /** Ngày còn mở: sản lượng là số tạm tính và còn tăng tiếp (CR-01 §6.9). */
  isProvisional: boolean
  closedAt: string | null
  /** null cho ngày chưa Xuất hàng — KHÔNG phải 0 (CR-01 N-07). */
  difference: number | null
  shortageQuantity: number | null
  cumulativePlan: number
  cumulativeActual: number
}

/** Tách theo dây chuyền cho một đơn hàng (CR-001 §6.9). */
export interface OrderLineStatisticsDto {
  productionLineId: string
  productionLineCode: string
  productionLineName: string
  /** Mốc phân bổ tầng 1, bất biến (BR-N18). */
  allocatedQuantity: number
  totalPlan: number
  totalActual: number
  shortage: number
}

export interface OrderStatisticsDto {
  orderId: string
  shoeCode: string
  orderQuantity: number
  totalActual: number
  remaining: number
  totalPlan: number
  totalInitialPlan: number
  progressPercentage: number
  scheduleStatus: ScheduleStatus
  behindQuantity: number
  daysRemaining: number
  isOverdue: boolean
  /** Gộp mọi dây chuyền — đây là đường xu hướng theo thời gian. */
  daily: DailyStatisticsDto[]
  byProductionLine: OrderLineStatisticsDto[]
}

export interface DashboardTodayDto {
  plannedQuantity: number
  actualQuantity: number
  hasAnyActualEntered: boolean
  difference: number
  completionPercentage: number
}

export interface DashboardAlertDto {
  orderId: string
  shoeCode: string
  behindQuantity: number
  daysRemaining: number
  isOverdue: boolean
  dueDate: IsoDate
}

/** Một ngày CÓ kế hoạch của đơn hàng. Ngày không sản xuất không xuất hiện trong danh sách. */
export interface DashboardOrderDayDto {
  productionDate: IsoDate
  plannedQuantity: number
  /** null nghĩa là chưa ghi nhận lần nào — tuyệt đối không coi là 0. */
  actualQuantity: number | null
  dayStatus: DayStatus
}

export interface DashboardOrderDto {
  orderId: string
  shoeCode: string
  startDate: IsoDate
  dueDate: IsoDate
  progressPercentage: number
  /** Chỉ có giá trị khi hôm nay đã Xuất hàng — ngày còn mở chưa có số chính thức. */
  todayDifference: number | null
  todayHasPlan: boolean
  /** Sản lượng hôm nay; ngày còn mở thì đây là số tạm tính (CR-01 §6.9). */
  todayPlannedQuantity: number
  todayActualQuantity: number
  todayStatus: DayStatus | null
  remaining: number
  scheduleStatus: ScheduleStatus
  behindQuantity: number
  days: DashboardOrderDayDto[]
}

/** Một ô đang sản xuất hôm nay — khối "Đang sản xuất hôm nay" (CR-01 §6.9). */
export interface DashboardTodayProductionDto {
  orderId: string
  shoeCode: string
  productionDate: IsoDate
  productionLineId: string
  productionLineCode: string
  plannedQuantity: number
  dayActualQuantity: number
  lastRecordedAt: string | null
}

/** Ô đã qua mà chưa Xuất hàng — kể cả ô hoàn toàn chưa nhập gì (CR-01 §14.5). */
export interface DashboardUnclosedDayDto {
  orderId: string
  shoeCode: string
  productionDate: IsoDate
  productionLineId: string
  productionLineCode: string
  plannedQuantity: number
  dayActualQuantity: number
}

/** Phần thiếu của một ô đã Xuất hàng mà chưa được xử lý bù. */
export interface DashboardOpenShortageDto {
  orderId: string
  shoeCode: string
  productionPlanId: string
  productionDate: IsoDate
  productionLineId: string
  productionLineCode: string
  shortageQuantity: number
}

export interface DashboardStatisticsDto {
  date: IsoDate
  totalOrders: number
  /** Đơn đã nhập hàng nhưng chưa lập tiến độ. Không tham gia tính tiến độ (CR-001 §6.9). */
  pendingOrderCount: number
  incompleteOrders: number
  completedOrders: number
  behindOrders: number
  totalOrderQuantity: number
  /** Bao gồm cả sản lượng tạm tính của các ngày đang mở (CR-01 §6.9). */
  totalActualQuantity: number
  totalRemainingQuantity: number
  today: DashboardTodayDto
  alerts: DashboardAlertDto[]
  trackedOrders: DashboardOrderDto[]
  todayProduction: DashboardTodayProductionDto[]
  unclosedPastCells: DashboardUnclosedDayDto[]
  openShortages: DashboardOpenShortageDto[]
}
