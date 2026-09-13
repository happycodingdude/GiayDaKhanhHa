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

/**
 * Một ô đích của đề xuất bù. Dây chuyền được trả kèm để UI nói rõ đang bù trên dây chuyền nào —
 * mọi ô ở đây luôn cùng một dây chuyền với ô nguồn (CR-001 §6.8, BR-N12).
 */
export interface AdjustmentPreviewItemDto {
  productionPlanId: string
  productionDate: IsoDate
  productionLineId: string
  productionLineName: string
  currentPlannedQuantity: number
  addOnQuantity: number
  plannedQuantityAfter: number
}

/** Preview chỉ là state UI — không bao giờ được lưu xuống (Step 5 §20). */
export interface AdjustmentPreviewDto {
  sourceProductionPlanId: string
  sourceProductionDate: IsoDate
  productionLineId: string
  productionLineCode: string
  productionLineName: string
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

/** Bù sản lượng luôn nằm gọn trong một dây chuyền, nên nó là thuộc tính của cả lần bù. */
export interface AdjustmentLineDto {
  id: string
  code: string
  name: string
}

export interface PlanAdjustmentDto {
  id: string
  sourceProductionPlanId: string
  sourceProductionDate: IsoDate
  productionLine: AdjustmentLineDto
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
