import type { IsoDate } from '../../shared/lib/date'

export type AdjustmentType = 'Manual' | 'Automatic'
export type AdjustmentStatus = 'Applied' | 'Reversed'

export interface AdjustmentTargetRequest {
  productionPlanId: string
  addOnQuantity: number
}

export interface PreviewAdjustmentRequest {
  adjustmentType: AdjustmentType
  targets?: AdjustmentTargetRequest[]
}

export interface ApplyAdjustmentRequest {
  adjustmentType: AdjustmentType
  shortageQuantity: number
  targets: AdjustmentTargetRequest[]
}

export interface AdjustmentPreviewItemDto {
  productionPlanId: string
  productionDate: IsoDate
  currentPlannedQuantity: number
  addOnQuantity: number
  plannedQuantityAfter: number
}

/** Preview chỉ là state UI — không bao giờ được lưu xuống (Step 5 §20). */
export interface AdjustmentPreviewDto {
  sourceProductionPlanId: string
  sourceProductionDate: IsoDate
  sourcePlannedQuantity: number
  sourceActualQuantity: number | null
  shortageQuantity: number
  adjustmentType: AdjustmentType
  items: AdjustmentPreviewItemDto[]
  totalAddOnQuantity: number
  valid: boolean
  validationCode: string | null
  validationMessage: string | null
}

export interface PlanAdjustmentItemDto {
  productionPlanId: string
  productionDate: IsoDate
  addOnQuantity: number
}

export type AdjustmentRecalculationOutcome = 'Recalculated' | 'Removed' | 'Unhandled'

/**
 * Được báo về khi việc sửa sản lượng thực tế của một ngày làm thay đổi phần thiếu mà khoản bù
 * đã áp dụng dựa vào. Điều chỉnh cũ bị hoàn tác và thay thế ở phía server, nên lịch sử không
 * bị viết lại.
 */
export interface AdjustmentRecalculationDto {
  outcome: AdjustmentRecalculationOutcome
  reversedAdjustmentId: string
  previousShortageQuantity: number
  shortageQuantity: number
  adjustmentType: AdjustmentType
  adjustmentId: string | null
  items: PlanAdjustmentItemDto[]
}

export interface PlanAdjustmentDto {
  id: string
  sourceProductionPlanId: string
  sourceProductionDate: IsoDate
  shortageQuantity: number
  adjustmentType: AdjustmentType
  status: AdjustmentStatus
  items: PlanAdjustmentItemDto[]
  createdBy: string
  appliedBy: string | null
  reversedBy: string | null
  createdAt: string
  appliedAt: string | null
  reversedAt: string | null
}
