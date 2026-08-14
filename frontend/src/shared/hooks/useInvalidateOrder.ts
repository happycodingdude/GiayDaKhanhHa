import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { queryKeys } from '../../app/config/queryKeys'

/**
 * Sau một mutation về sản xuất, các query bị ảnh hưởng sẽ được invalidate và refetch — những
 * thao tác này không dùng optimistic update (Step 5 §25, §31).
 */
export function useInvalidateOrder() {
  const queryClient = useQueryClient()

  const invalidateAfterActualChange = useCallback(
    async (orderId: string) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.order(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionPlans(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderStatistics(orderId) }),
        // Sửa thực tế có thể dựng lại điều chỉnh đang hiệu lực của ngày, sinh thêm bản ghi lịch sử.
        queryClient.invalidateQueries({ queryKey: queryKeys.orderPlanAdjustments(orderId) }),
        // Cả danh sách lẫn dashboard đều hiển thị số tổng suy ra từ sản lượng thực tế.
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
    [queryClient],
  )

  const invalidateAfterAdjustmentChange = useCallback(
    async (orderId: string) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.order(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionPlans(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderStatistics(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderPlanAdjustments(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
    [queryClient],
  )

  return { invalidateAfterActualChange, invalidateAfterAdjustmentChange }
}
