import { useMutation, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { useInvalidateOrder } from '../../../shared/hooks/useInvalidateOrder'
import { productionApi } from '../api/productionApi'
import type { CreateProductionRecordRequest, UpdateProductionRecordRequest } from '../types'

export function useProductionPlans(orderId: string) {
  return useQuery({
    queryKey: queryKeys.orderProductionPlans(orderId),
    queryFn: ({ signal }) => productionApi.getProductionPlans(orderId, signal),
  })
}

/**
 * Submit → loading → transaction ở server → thành công → refetch. Cố ý không dùng optimistic
 * update: ràng buộc tổng sản lượng thực tế do server đảm bảo (Step 5 §31).
 */
export function useCreateActual(orderId: string) {
  const { invalidateAfterActualChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (request: CreateProductionRecordRequest) => productionApi.createActual(orderId, request),
    onSuccess: () => invalidateAfterActualChange(orderId),
  })
}

export function useUpdateActual(orderId: string) {
  const { invalidateAfterActualChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: ({
      productionRecordId,
      request,
    }: {
      productionRecordId: string
      request: UpdateProductionRecordRequest
    }) => productionApi.updateActual(orderId, productionRecordId, request),
    onSuccess: () => invalidateAfterActualChange(orderId),
  })
}
