import { useMutation, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { useInvalidateOrder } from '../../../shared/hooks/useInvalidateOrder'
import type { IsoDate } from '../../../shared/lib/date'
import { productionApi } from '../api/productionApi'
import type { CreateProductionEntryRequest, UpdateProductionEntryRequest } from '../types'

export function useProductionDay(orderId: string, productionDate: IsoDate) {
  return useQuery({
    queryKey: queryKeys.orderProductionDay(orderId, productionDate),
    queryFn: ({ signal }) => productionApi.getProductionDay(orderId, productionDate, signal),
  })
}

/**
 * Submit → loading → transaction ở server → thành công → cập nhật từ payload server trả về.
 * Không optimistic update: trần "Còn được nhập" chỉ server mới tính đúng (CR-01 §7.4).
 */
export function useCreateProductionEntry(orderId: string, productionDate: IsoDate) {
  const { invalidateAfterEntryChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (request: CreateProductionEntryRequest) =>
      productionApi.createProductionEntry(orderId, productionDate, request),
    onSuccess: () => invalidateAfterEntryChange(orderId, productionDate),
  })
}

export function useUpdateProductionEntry(orderId: string, productionDate: IsoDate) {
  const { invalidateAfterEntryChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: ({ entryId, request }: { entryId: string; request: UpdateProductionEntryRequest }) =>
      productionApi.updateProductionEntry(entryId, request),
    onSuccess: () => invalidateAfterEntryChange(orderId, productionDate),
  })
}

export function useDeleteProductionEntry(orderId: string, productionDate: IsoDate) {
  const { invalidateAfterEntryChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (entryId: string) => productionApi.deleteProductionEntry(entryId),
    onSuccess: () => invalidateAfterEntryChange(orderId, productionDate),
  })
}

/** Xuất hàng. Một chiều: không có thao tác mở lại ngày (CR-01 N-06). */
export function useCloseProductionDay(orderId: string, productionDate: IsoDate) {
  const { invalidateAfterDayClose } = useInvalidateOrder()

  return useMutation({
    mutationFn: () => productionApi.closeProductionDay(orderId, productionDate),
    onSuccess: () => invalidateAfterDayClose(orderId, productionDate),
  })
}
