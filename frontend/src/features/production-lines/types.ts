export type ProductionLineStatus = 'Active' | 'Inactive'

export interface ProductionLineDto {
  id: string
  code: string
  name: string
  status: ProductionLineStatus
  sortOrder: number
  note: string | null
  /** Đã được gán cho ít nhất một đơn hàng — dùng để ẩn hành động xoá (CR-001 BR-N15). */
  inUse: boolean
}

export interface ProductionLineListDto {
  items: ProductionLineDto[]
}

export interface SaveProductionLineRequest {
  code: string
  name: string
  sortOrder: number
  note: string | null
}
