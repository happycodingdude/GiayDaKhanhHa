import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { productionLinesApi } from '../api/productionLinesApi'
import type { ProductionLineStatus, SaveProductionLineRequest } from '../types'

export function useProductionLines(status?: ProductionLineStatus) {
  return useQuery({
    queryKey: queryKeys.productionLinesFiltered(status),
    queryFn: ({ signal }) => productionLinesApi.list(status, signal),
  })
}

/**
 * Mọi mutation về dây chuyền đều invalidate cả prefix `["production-lines"]`: bảng cấu hình và
 * danh sách `Active` của màn lập tiến độ là hai query khác nhau trên cùng một dữ liệu (CR-001 §7.10).
 */
function useInvalidateProductionLines() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: queryKeys.productionLines })
}

export function useCreateProductionLine() {
  const invalidate = useInvalidateProductionLines()

  return useMutation({
    mutationFn: (request: SaveProductionLineRequest) => productionLinesApi.create(request),
    onSuccess: invalidate,
  })
}

export function useUpdateProductionLine() {
  const invalidate = useInvalidateProductionLines()

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveProductionLineRequest }) =>
      productionLinesApi.update(id, request),
    onSuccess: invalidate,
  })
}

export function useChangeProductionLineStatus() {
  const invalidate = useInvalidateProductionLines()

  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: ProductionLineStatus }) =>
      productionLinesApi.changeStatus(id, status),
    onSuccess: invalidate,
  })
}
