import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { queryKeys } from '../../app/config/queryKeys'

/**
 * After a production mutation the affected queries are invalidated and refetched — there are no
 * optimistic updates for these operations (Step 5 §25, §31).
 */
export function useInvalidateOrder() {
  const queryClient = useQueryClient()

  const invalidateAfterActualChange = useCallback(
    async (orderId: number) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.order(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionPlans(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderStatistics(orderId) }),
        // Editing an actual can rebuild the day's active adjustment, which adds history entries.
        queryClient.invalidateQueries({ queryKey: queryKeys.orderPlanAdjustments(orderId) }),
        // The list and the dashboard both show totals derived from the actual quantity.
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
    [queryClient],
  )

  const invalidateAfterAdjustmentChange = useCallback(
    async (orderId: number) => {
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
