import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { statisticsApi } from '../api/statisticsApi'

export function useDashboardStatistics() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: ({ signal }) => statisticsApi.dashboard(signal),
  })
}

export function useOrderStatistics(orderId: string) {
  return useQuery({
    queryKey: queryKeys.orderStatistics(orderId),
    queryFn: ({ signal }) => statisticsApi.orderStatistics(orderId, signal),
  })
}
