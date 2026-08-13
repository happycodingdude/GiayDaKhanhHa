import { useMutation, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { useInvalidateOrder } from '../../../shared/hooks/useInvalidateOrder'
import { productionApi } from '../api/productionApi'
import type { CreateProductionRecordRequest, UpdateProductionRecordRequest } from '../types'

export function useProductionPlans(orderId: number) {
  return useQuery({
    queryKey: queryKeys.orderProductionPlans(orderId),
    queryFn: ({ signal }) => productionApi.getProductionPlans(orderId, signal),
  })
}

/**
 * Submit → loading → server transaction → success → refetch. Deliberately not optimistic: the
 * total-actual invariant is enforced server-side (Step 5 §31).
 */
export function useCreateActual(orderId: number) {
  const { invalidateAfterActualChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (request: CreateProductionRecordRequest) => productionApi.createActual(orderId, request),
    onSuccess: () => invalidateAfterActualChange(orderId),
  })
}

export function useUpdateActual(orderId: number) {
  const { invalidateAfterActualChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: ({
      productionRecordId,
      request,
    }: {
      productionRecordId: number
      request: UpdateProductionRecordRequest
    }) => productionApi.updateActual(orderId, productionRecordId, request),
    onSuccess: () => invalidateAfterActualChange(orderId),
  })
}
