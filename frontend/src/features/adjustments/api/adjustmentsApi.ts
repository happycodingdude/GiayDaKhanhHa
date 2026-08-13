import { apiClient } from '../../../api/client'
import type {
  AdjustmentPreviewDto,
  ApplyAdjustmentRequest,
  PlanAdjustmentDto,
  PreviewAdjustmentRequest,
} from '../types'

export const adjustmentsApi = {
  preview: (productionPlanId: number, request: PreviewAdjustmentRequest) =>
    apiClient.post<AdjustmentPreviewDto>(
      `/production-plans/${productionPlanId}/adjustments/preview`,
      request,
    ),

  apply: (productionPlanId: number, request: ApplyAdjustmentRequest) =>
    apiClient.post<PlanAdjustmentDto>(`/production-plans/${productionPlanId}/adjustments`, request),

  reverse: (adjustmentId: number) =>
    apiClient.post<PlanAdjustmentDto>(`/plan-adjustments/${adjustmentId}/reverse`),

  history: (orderId: number, signal?: AbortSignal) =>
    apiClient.get<PlanAdjustmentDto[]>(`/orders/${orderId}/plan-adjustments`, { signal }),
}
