import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { ordersApi } from '../api/ordersApi'
import type {
  CreateProductionScheduleRequest,
  OrderDetailDto,
  OrderListFilters,
  ReceiveOrderRequest,
  UpdateOrderRequest,
} from '../types'

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

/** Dùng chung cho mọi mutation ghi vào một đơn hàng (CR-001 §7.10). */
function useAfterOrderChange() {
  const queryClient = useQueryClient()

  return async (order: OrderDetailDto) => {
    queryClient.setQueryData(queryKeys.order(order.id), order)
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
    ])
  }
}

export function useReceiveOrder() {
  const afterChange = useAfterOrderChange()

  return useMutation({
    mutationFn: (request: ReceiveOrderRequest) => ordersApi.receive(request),
    onSuccess: afterChange,
  })
}

export function useUpdateOrder() {
  const afterChange = useAfterOrderChange()

  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: UpdateOrderRequest }) =>
      ordersApi.update(orderId, request),
    onSuccess: afterChange,
  })
}

/** Xoá đơn chưa lập tiến độ. Đơn đã không còn nên bỏ hẳn cache chi tiết của nó, không cập nhật. */
export function useDeleteOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (orderId: string) => ordersApi.remove(orderId),
    onSuccess: async (_, orderId) => {
      queryClient.removeQueries({ queryKey: queryKeys.order(orderId) })
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
  })
}

/**
 * Lập tiến độ đụng tới gần như mọi thứ của đơn hàng: trạng thái, ngày, dây chuyền và toàn bộ ma
 * trận kế hoạch (CR-001 §7.10).
 */
export function useCreateSchedule() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: CreateProductionScheduleRequest }) =>
      ordersApi.createSchedule(orderId, request),
    onSuccess: async (order) => {
      queryClient.setQueryData(queryKeys.order(order.id), order)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionPlans(order.id) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderStatistics(order.id) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
  })
}
