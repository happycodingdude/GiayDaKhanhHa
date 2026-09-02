import type { IsoDate } from '../../shared/lib/date'
import type { DayStatus } from '../production/types'

export type OrderStatus = 'Incomplete' | 'Completed'
export type ScheduleStatus = 'OnSchedule' | 'Behind' | 'Completed'

export interface OrderListItemDto {
  id: string
  orderCode: string
  quantity: number
  startDate: IsoDate
  dueDate: IsoDate
  status: OrderStatus
  totalActual: number
  remaining: number
  totalPlan: number
  progressPercentage: number
  scheduleStatus: ScheduleStatus
  behindQuantity: number
  daysRemaining: number
  isOverdue: boolean
  /** Vị thế của hôm nay. null khi hôm nay không có kế hoạch cho đơn này (CR-01 §8, MH1). */
  todayPlannedQuantity: number | null
  todayActualQuantity: number | null
  todayStatus: DayStatus | null
  /** Có ngày đã qua chưa Xuất hàng — việc bị treo (CR-01 §14.5). */
  hasUnclosedPastDay: boolean
}

export interface OrderDetailDto extends Omit<OrderListItemDto, 'id'> {
  id: string
  totalInitialPlan: number
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

export interface CreateOrderRequest {
  orderCode: string
  quantity: number
  startDate: IsoDate
  dueDate: IsoDate
  productionPlans: { productionDate: IsoDate; plannedQuantity: number }[]
}

export interface OrderListFilters {
  status?: string
  search?: string
  page?: number
  pageSize?: number
}
