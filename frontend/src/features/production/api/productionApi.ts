import { apiClient } from '../../../api/client'
import type { IsoDate } from '../../../shared/lib/date'
import type {
  CloseProductionDayDto,
  CreateProductionEntryRequest,
  ProductionCellDetailDto,
  ProductionMatrixDto,
  UpdateProductionEntryRequest,
} from '../types'

/** Đường dẫn của một ô: đơn hàng → ngày → dây chuyền (CR-001 §6.7). */
const cellPath = (orderId: string, productionDate: IsoDate, productionLineId: string) =>
  `/orders/${orderId}/production-days/${productionDate}/lines/${productionLineId}`

/** Toàn bộ hàm gọi API của feature production nằm trong file này (CR-01 §7.2). */
export const productionApi = {
  getMatrix: (orderId: string, signal?: AbortSignal) =>
    apiClient.get<ProductionMatrixDto>(`/orders/${orderId}/production-plans`, { signal }),

  getCell: (orderId: string, productionDate: IsoDate, productionLineId: string, signal?: AbortSignal) =>
    apiClient.get<ProductionCellDetailDto>(cellPath(orderId, productionDate, productionLineId), { signal }),

  /**
   * Ghi nhận thêm một lần trong ô. Sản lượng là số cộng thêm, không phải giá trị thay thế.
   * Response là state đầy đủ của ô, nên không cần refetch sau mutation.
   */
  createEntry: (
    orderId: string,
    productionDate: IsoDate,
    productionLineId: string,
    request: CreateProductionEntryRequest,
  ) =>
    apiClient.post<ProductionCellDetailDto>(
      `${cellPath(orderId, productionDate, productionLineId)}/entries`,
      request,
    ),

  updateEntry: (entryId: string, request: UpdateProductionEntryRequest) =>
    apiClient.put<ProductionCellDetailDto>(`/production-entries/${entryId}`, request),

  deleteEntry: (entryId: string) =>
    apiClient.delete<ProductionCellDetailDto>(`/production-entries/${entryId}`),

  /**
   * Xuất hàng — chốt sổ cả ngày, mọi dây chuyền cùng lúc. Body rỗng: sản lượng do server tính,
   * client không gửi lên (CR-01 N-11).
   */
  closeDay: (orderId: string, productionDate: IsoDate) =>
    apiClient.post<CloseProductionDayDto>(`/orders/${orderId}/production-days/${productionDate}/close`),
}
