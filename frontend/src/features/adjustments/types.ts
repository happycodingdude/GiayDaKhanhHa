import type { IsoDate } from '../../shared/lib/date'

export type AdjustmentType = 'Manual' | 'Automatic'
export type AdjustmentStatus = 'Applied' | 'Reversed'

export interface AdjustmentTargetRequest {
  productionPlanId: number
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
  productionPlanId: number
  productionDate: IsoDate
  currentPlannedQuantity: number
  addOnQuantity: number
  plannedQuantityAfter: number
}

/** A preview is UI state only — it is never persisted (Step 5 §20). */
export interface AdjustmentPreviewDto {
  sourceProductionPlanId: number
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
  productionPlanId: number
  productionDate: IsoDate
  addOnQuantity: number
}

export type AdjustmentRecalculationOutcome = 'Recalculated' | 'Removed' | 'Unhandled'

/**
 * Reported when editing a day's actual changed the shortage its applied add-on was based on. The
 * outdated adjustment is reversed and replaced server-side, so history is never rewritten.
 */
export interface AdjustmentRecalculationDto {
  outcome: AdjustmentRecalculationOutcome
  reversedAdjustmentId: number
  previousShortageQuantity: number
  shortageQuantity: number
  adjustmentType: AdjustmentType
  adjustmentId: number | null
  items: PlanAdjustmentItemDto[]
}

export interface PlanAdjustmentDto {
  id: number
  sourceProductionPlanId: number
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
