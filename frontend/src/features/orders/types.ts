import type { IsoDate } from '../../shared/lib/date'

export type OrderStatus = 'Incomplete' | 'Completed'
export type ScheduleStatus = 'OnSchedule' | 'Behind' | 'Completed'

export interface OrderListItemDto {
  id: number
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
}

export interface OrderDetailDto extends Omit<OrderListItemDto, 'id'> {
  id: number
  totalInitialPlan: number
  /** The production period is over, so the order is read-only. True for completed orders too. */
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
