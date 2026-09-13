import { useMutation, useQueries, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { useInvalidateOrder } from '../../../shared/hooks/useInvalidateOrder'
import type { IsoDate } from '../../../shared/lib/date'
import { productionApi } from '../api/productionApi'
import type { CreateProductionEntryRequest, UpdateProductionEntryRequest } from '../types'

export function useProductionMatrix(orderId: string) {
  return useQuery({
    queryKey: queryKeys.orderProductionPlans(orderId),
    queryFn: ({ signal }) => productionApi.getMatrix(orderId, signal),
  })
}

export function useProductionCell(orderId: string, productionDate: IsoDate, productionLineId: string) {
  return useQuery({
    queryKey: queryKeys.orderProductionCell(orderId, productionDate, productionLineId),
    queryFn: ({ signal }) => productionApi.getCell(orderId, productionDate, productionLineId, signal),
  })
}

/**
 * Submit → loading → transaction ở server → thành công → cập nhật từ payload server trả về.
 * Không optimistic update: trần "Còn được nhập" chỉ server mới tính đúng (CR-01 §7.4).
 */
export function useCreateProductionEntry(orderId: string, productionDate: IsoDate, lineId: string) {
  const { invalidateAfterEntryChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (request: CreateProductionEntryRequest) =>
      productionApi.createEntry(orderId, productionDate, lineId, request),
    onSuccess: () => invalidateAfterEntryChange(orderId, productionDate, lineId),
  })
}

export function useUpdateProductionEntry(orderId: string, productionDate: IsoDate, lineId: string) {
  const { invalidateAfterEntryChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: ({ entryId, request }: { entryId: string; request: UpdateProductionEntryRequest }) =>
      productionApi.updateEntry(entryId, request),
    onSuccess: () => invalidateAfterEntryChange(orderId, productionDate, lineId),
  })
}

export function useDeleteProductionEntry(orderId: string, productionDate: IsoDate, lineId: string) {
  const { invalidateAfterEntryChange } = useInvalidateOrder()

  return useMutation({
    mutationFn: (entryId: string) => productionApi.deleteEntry(entryId),
    onSuccess: () => invalidateAfterEntryChange(orderId, productionDate, lineId),
  })
}

/** State mới nhất của nhiều ô cùng một ngày — dialog Xuất hàng cần đủ mọi dây chuyền của ngày. */
export function useProductionCells(orderId: string, productionDate: IsoDate, productionLineIds: string[]) {
  return useQueries({
    queries: productionLineIds.map((lineId) => ({
      queryKey: queryKeys.orderProductionCell(orderId, productionDate, lineId),
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        productionApi.getCell(orderId, productionDate, lineId, signal),
    })),
  })
}

/** Xuất hàng cả ngày. Một chiều: không có thao tác mở lại (CR-01 N-06). */
export function useCloseProductionDay(orderId: string, productionDate: IsoDate) {
  const { invalidateAfterDayClose } = useInvalidateOrder()

  return useMutation({
    mutationFn: () => productionApi.closeDay(orderId, productionDate),
    onSuccess: () => invalidateAfterDayClose(orderId, productionDate),
  })
}
