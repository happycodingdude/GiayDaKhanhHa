import type { IsoDate } from '../../shared/lib/date'

/**
 * Vòng đời đơn hàng sau CR-001. `Pending` = đã nhập hàng, chưa lập tiến độ (§4.1).
 */
export type OrderStatus = 'Pending' | 'Incomplete' | 'Completed'
export type ScheduleStatus = 'OnSchedule' | 'Behind' | 'Completed'

/** Một dây chuyền thuộc tiến độ của đơn hàng, kèm số đã phân bổ ở tầng 1. */
export interface OrderProductionLineDto {
  id: string
  code: string
  name: string
  status: 'Active' | 'Inactive'
  sortOrder: number
  allocatedQuantity: number
}

export interface OrderListItemDto {
  id: string
  shoeCode: string
  quantity: number
  /** Đơn chưa lập tiến độ chưa có ngày (CR-001 BR-N04). */
  startDate: IsoDate | null
  dueDate: IsoDate | null
  status: OrderStatus
  hasImage: boolean
  /** Endpoint có xác thực; đường dẫn vật lý không bao giờ ra ngoài. */
  imageUrl: string | null
  productionLines: OrderProductionLineDto[]
  totalActual: number
  remaining: number
  totalPlan: number
  progressPercentage: number
  scheduleStatus: ScheduleStatus
  behindQuantity: number
  daysRemaining: number
  isOverdue: boolean
  /** Vị thế hôm nay, gộp mọi dây chuyền. null khi hôm nay không có kế hoạch. */
  todayPlannedQuantity: number | null
  todayActualQuantity: number | null
  /** Có ô đã qua chưa Xuất hàng — việc bị treo (CR-01 §14.5). */
  hasUnclosedPastCell: boolean
  /** Số ngày đã qua còn chưa Xuất hàng, đếm theo ngày. Luôn 0 với đơn không còn Chưa hoàn thành. */
  unclosedPastDayCount: number
}

export interface OrderDetailDto {
  id: string
  shoeCode: string
  quantity: number
  startDate: IsoDate | null
  dueDate: IsoDate | null
  status: OrderStatus
  hasImage: boolean
  imageUrl: string | null
  imageFileName: string | null
  productionLines: OrderProductionLineDto[]
  totalActual: number
  remaining: number
  totalPlan: number
  totalInitialPlan: number
  progressPercentage: number
  scheduleStatus: ScheduleStatus
  behindQuantity: number
  daysRemaining: number
  isOverdue: boolean
  /** Kỳ sản xuất đã kết thúc nên đơn hàng chỉ đọc. Đúng với cả đơn đã hoàn thành. */
  isPastDueDate: boolean
  createdAt: string
  updatedAt: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Nhập hàng: mã giày, số lượng, tối đa một ảnh mẫu (CR-001 §6.4). */
export interface ReceiveOrderRequest {
  shoeCode: string
  quantity: number
  image: File | null
}

/**
 * Sửa nhập hàng. Thông tin và ảnh được lưu trong cùng một request: `image` thay ảnh hiện tại,
 * `removeImage` gỡ nó. Không gửi cả hai cùng lúc.
 */
export interface UpdateOrderRequest {
  shoeCode: string
  quantity: number
  image: File | null
  removeImage: boolean
}

export interface OrderListFilters {
  status?: string
  search?: string
  productionLineId?: string
  page?: number
  pageSize?: number
}

// --- Lập tiến độ (CR-001 §6.6) ---------------------------------------------------------------

export type AllocationMode = 'Even' | 'Manual'

export interface SchedulePlanRequest {
  productionDate: IsoDate
  plannedQuantity: number
}

export interface ScheduleLineRequest {
  productionLineId: string
  allocatedQuantity: number
  plans: SchedulePlanRequest[]
}

export interface CreateProductionScheduleRequest {
  startDate: IsoDate
  dueDate: IsoDate
  allocationMode: AllocationMode
  lines: ScheduleLineRequest[]
}
