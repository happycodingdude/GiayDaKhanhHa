import { useMutation, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { useInvalidateOrder } from '../../../shared/hooks/useInvalidateOrder'
import { adjustmentsApi } from '../api/adjustmentsApi'
import type { ApplyAdjustmentRequest, PreviewAdjustmentRequest } from '../types'

export function usePlanAdjustments(orderId: string) {
  return useQuery({
    queryKey: queryKeys.orderPlanAdjustments(orderId),
    queryFn: ({ signal }) => adjustmentsApi.history(orderId, signal),
  })
}

/**
 * Preview là mutation chứ không phải query: đây là một yêu cầu tính toán tường minh, kết quả
 * chỉ là state UI cục bộ và không bao giờ được cache như dữ liệu server (Step 5 §20).
 */
export function usePreviewAdjustment() {
  return useMutation({
    mutationFn: ({
      productionPlanId,
      request,
    }: {
      productionPlanId: string
      request: PreviewAdjustmentRequest
    }) => adjustmentsApi.preview(productionPlanId, request),
  })
}

export function useApplyAdjustment(orderId: string) {
  const { invalidateAfterAdjustmentChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: ({
      productionPlanId,
      request,
    }: {
      productionPlanId: string
      request: ApplyAdjustmentRequest
    }) => adjustmentsApi.apply(productionPlanId, request),
    onSuccess: () => invalidateAfterAdjustmentChange(orderId),
  })
}

export function useReverseAdjustment(orderId: string) {
  const { invalidateAfterAdjustmentChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (adjustmentId: string) => adjustmentsApi.reverse(adjustmentId),
    onSuccess: () => invalidateAfterAdjustmentChange(orderId),
  })
}
