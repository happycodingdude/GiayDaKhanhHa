import type { IsoDate } from '../../shared/lib/date'
import type { ScheduleStatus } from '../orders/types'

export interface DailyStatisticsDto {
  productionDate: IsoDate
  initialPlannedQuantity: number
  addOnQuantity: number
  plannedQuantity: number
  actualQuantity: number | null
  difference: number | null
  shortageQuantity: number
  cumulativePlan: number
  cumulativeActual: number
}

export interface OrderStatisticsDto {
  orderId: string
  orderCode: string
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
  daily: DailyStatisticsDto[]
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
  orderCode: string
  behindQuantity: number
  daysRemaining: number
  isOverdue: boolean
  dueDate: IsoDate
}

export interface DashboardOrderDto {
  orderId: string
  orderCode: string
  progressPercentage: number
  todayDifference: number | null
  todayHasPlan: boolean
  remaining: number
  scheduleStatus: ScheduleStatus
  behindQuantity: number
}

export interface DashboardStatisticsDto {
  date: IsoDate
  totalOrders: number
  incompleteOrders: number
  completedOrders: number
  behindOrders: number
  totalOrderQuantity: number
  totalActualQuantity: number
  totalRemainingQuantity: number
  today: DashboardTodayDto
  alerts: DashboardAlertDto[]
  trackedOrders: DashboardOrderDto[]
}
