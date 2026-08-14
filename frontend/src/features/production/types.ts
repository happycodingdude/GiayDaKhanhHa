import type { IsoDate } from '../../shared/lib/date'
import type { AdjustmentRecalculationDto } from '../adjustments/types'

/** Một ngày sản xuất: kế hoạch, thực tế và các giá trị suy ra do backend tổng hợp. */
export interface ProductionDayDto {
  id: string
  productionDate: IsoDate
  initialPlannedQuantity: number
  addOnQuantity: number
  plannedQuantity: number
  /** null nghĩa là chưa nhập thực tế — tuyệt đối không render thành 0. */
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
   * Được set khi lần sửa này làm thay đổi phần thiếu mà khoản bù đang hiệu lực của ngày dựa
   * vào, khiến khoản bù được dựng lại. Luôn null khi tạo mới bản ghi.
   */
  adjustmentRecalculation: AdjustmentRecalculationDto | null
}
