import { apiClient } from '../../../api/client'
import type {
  AdjustmentPreviewDto,
  ApplyAdjustmentRequest,
  PlanAdjustmentDto,
  PreviewAdjustmentRequest,
} from '../types'

export const adjustmentsApi = {
  preview: (productionPlanId: string, request: PreviewAdjustmentRequest) =>
    apiClient.post<AdjustmentPreviewDto>(
      `/production-plans/${productionPlanId}/adjustments/preview`,
      request,
    ),

  apply: (productionPlanId: string, request: ApplyAdjustmentRequest) =>
    apiClient.post<PlanAdjustmentDto>(`/production-plans/${productionPlanId}/adjustments`, request),

  reverse: (adjustmentId: string) =>
    apiClient.post<PlanAdjustmentDto>(`/plan-adjustments/${adjustmentId}/reverse`),

  history: (orderId: string, signal?: AbortSignal) =>
    apiClient.get<PlanAdjustmentDto[]>(`/orders/${orderId}/plan-adjustments`, { signal }),
}
