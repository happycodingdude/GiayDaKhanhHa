import { apiClient } from '../../../api/client'
import type {
  CreateProductionRecordRequest,
  ProductionPlanListDto,
  ProductionRecordDto,
  UpdateProductionRecordRequest,
} from '../types'

export const productionApi = {
  getProductionPlans: (orderId: number, signal?: AbortSignal) =>
    apiClient.get<ProductionPlanListDto>(`/orders/${orderId}/production-plans`, { signal }),

  createActual: (orderId: number, request: CreateProductionRecordRequest) =>
    apiClient.post<ProductionRecordDto>(`/orders/${orderId}/production-records`, request),

  /** Editing replaces the recorded value; actual is never accumulated. */
  updateActual: (orderId: number, productionRecordId: number, request: UpdateProductionRecordRequest) =>
    apiClient.put<ProductionRecordDto>(
      `/orders/${orderId}/production-records/${productionRecordId}`,
      request,
    ),
}
