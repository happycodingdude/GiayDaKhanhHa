import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { ordersApi } from '../api/ordersApi'
import type { CreateOrderRequest, OrderListFilters } from '../types'

export function useOrders(filters: OrderListFilters) {
  return useQuery({
    queryKey: queryKeys.ordersListFiltered(filters),
    queryFn: ({ signal }) => ordersApi.list(filters, signal),
    placeholderData: (previous) => previous,
  })
}

export function useOrder(orderId: string) {
  return useQuery({
    queryKey: queryKeys.order(orderId),
    queryFn: ({ signal }) => ordersApi.getById(orderId, signal),
  })
}

export function useCreateOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: CreateOrderRequest) => ordersApi.create(request),
    onSuccess: async (order) => {
      queryClient.setQueryData(queryKeys.order(order.id), order)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
  })
}
