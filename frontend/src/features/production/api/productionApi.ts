import { apiClient } from '../../../api/client'
import type {
  CreateProductionRecordRequest,
  ProductionPlanListDto,
  ProductionRecordDto,
  UpdateProductionRecordRequest,
} from '../types'

export const productionApi = {
  getProductionPlans: (orderId: string, signal?: AbortSignal) =>
    apiClient.get<ProductionPlanListDto>(`/orders/${orderId}/production-plans`, { signal }),

  createActual: (orderId: string, request: CreateProductionRecordRequest) =>
    apiClient.post<ProductionRecordDto>(`/orders/${orderId}/production-records`, request),

  /** Sửa là thay thế giá trị đã ghi; sản lượng thực tế không bao giờ được cộng dồn. */
  updateActual: (orderId: string, productionRecordId: string, request: UpdateProductionRecordRequest) =>
    apiClient.put<ProductionRecordDto>(
      `/orders/${orderId}/production-records/${productionRecordId}`,
      request,
    ),
}
