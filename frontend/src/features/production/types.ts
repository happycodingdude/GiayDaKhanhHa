import type { IsoDate } from '../../shared/lib/date'
import type { AdjustmentRecalculationDto } from '../adjustments/types'

/** One production day: plan, actual and derived values combined by the backend. */
export interface ProductionDayDto {
  id: string
  productionDate: IsoDate
  initialPlannedQuantity: number
  addOnQuantity: number
  plannedQuantity: number
  /** null means the actual has not been entered yet — never render this as 0. */
  actualQuantity: number | null
  productionRecordId: string | null
  shortageQuantity: number
  difference: number | null
  hasActiveAdjustment: boolean
  activeAdjustmentId: string | null
  actualEnteredBy: string | null
  actualUpdatedAt: string | null
}

export interface ProductionPlanListDto {
  orderId: string
  items: ProductionDayDto[]
}

export interface CreateProductionRecordRequest {
  productionDate: IsoDate
  actualQuantity: number
}

export interface UpdateProductionRecordRequest {
  actualQuantity: number
}

export interface ProductionRecordDto {
  id: string
  orderId: string
  productionDate: IsoDate
  actualQuantity: number
  createdAt: string
  updatedAt: string
  /**
   * Set when this edit changed the shortage the day's active add-on was based on, so the add-on
   * was rebuilt. Always null when creating a record.
   */
  adjustmentRecalculation: AdjustmentRecalculationDto | null
}
