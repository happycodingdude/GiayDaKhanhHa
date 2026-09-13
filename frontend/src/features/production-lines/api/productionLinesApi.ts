import { apiClient } from '../../../api/client'
import type {
  ProductionLineDto,
  ProductionLineListDto,
  ProductionLineStatus,
  SaveProductionLineRequest,
} from '../types'

export const productionLinesApi = {
  /** Bỏ trống `status` để lấy tất cả; màn lập tiến độ chỉ lấy `Active`. */
  list: (status: ProductionLineStatus | undefined, signal?: AbortSignal) =>
    apiClient.get<ProductionLineListDto>('/production-lines', { signal, query: { status } }),

  create: (request: SaveProductionLineRequest) =>
    apiClient.post<ProductionLineDto>('/production-lines', request),

  update: (productionLineId: string, request: SaveProductionLineRequest) =>
    apiClient.put<ProductionLineDto>(`/production-lines/${productionLineId}`, request),

  changeStatus: (productionLineId: string, status: ProductionLineStatus) =>
    apiClient.patch<ProductionLineDto>(`/production-lines/${productionLineId}/status`, { status }),
}
