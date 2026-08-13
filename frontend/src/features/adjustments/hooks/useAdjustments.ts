import { useMutation, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { useInvalidateOrder } from '../../../shared/hooks/useInvalidateOrder'
import { adjustmentsApi } from '../api/adjustmentsApi'
import type { ApplyAdjustmentRequest, PreviewAdjustmentRequest } from '../types'

export function usePlanAdjustments(orderId: number) {
  return useQuery({
    queryKey: queryKeys.orderPlanAdjustments(orderId),
    queryFn: ({ signal }) => adjustmentsApi.history(orderId, signal),
  })
}

/**
 * Preview is a mutation rather than a query: it is an explicit calculation request whose result is
 * local UI state and is never cached as server data (Step 5 §20).
 */
export function usePreviewAdjustment() {
  return useMutation({
    mutationFn: ({
      productionPlanId,
      request,
    }: {
      productionPlanId: number
      request: PreviewAdjustmentRequest
    }) => adjustmentsApi.preview(productionPlanId, request),
  })
}

export function useApplyAdjustment(orderId: number) {
  const { invalidateAfterAdjustmentChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: ({
      productionPlanId,
      request,
    }: {
      productionPlanId: number
      request: ApplyAdjustmentRequest
    }) => adjustmentsApi.apply(productionPlanId, request),
    onSuccess: () => invalidateAfterAdjustmentChange(orderId),
  })
}

export function useReverseAdjustment(orderId: number) {
  const { invalidateAfterAdjustmentChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (adjustmentId: number) => adjustmentsApi.reverse(adjustmentId),
    onSuccess: () => invalidateAfterAdjustmentChange(orderId),
  })
}
