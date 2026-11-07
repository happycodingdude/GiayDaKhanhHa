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
      `/production-plans/${encodeURIComponent(productionPlanId)}/adjustments/preview`,
      request,
    ),

  apply: (productionPlanId: string, request: ApplyAdjustmentRequest) =>
    apiClient.post<PlanAdjustmentDto>(`/production-plans/${encodeURIComponent(productionPlanId)}/adjustments`, request),

  reverse: (adjustmentId: string) =>
    apiClient.post<PlanAdjustmentDto>(`/plan-adjustments/${encodeURIComponent(adjustmentId)}/reverse`),

  history: (orderId: string, signal?: AbortSignal) =>
    apiClient.get<PlanAdjustmentDto[]>(`/orders/${encodeURIComponent(orderId)}/plan-adjustments`, { signal }),
}
