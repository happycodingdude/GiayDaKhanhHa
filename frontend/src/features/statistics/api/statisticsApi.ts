import { apiClient } from '../../../api/client'
import type { DashboardStatisticsDto, OrderStatisticsDto } from '../types'

export const statisticsApi = {
  orderStatistics: (orderId: string, signal?: AbortSignal) =>
    apiClient.get<OrderStatisticsDto>(`/orders/${orderId}/statistics`, { signal }),

  dashboard: (signal?: AbortSignal) =>
    apiClient.get<DashboardStatisticsDto>('/statistics/dashboard', { signal }),
}
