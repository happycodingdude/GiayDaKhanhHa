import { apiClient } from '../../../api/client'
import type { IsoDate } from '../../../shared/lib/date'
import type {
  CloseProductionDayDto,
  CreateProductionEntryRequest,
  ProductionDayDetailDto,
  ProductionPlanListDto,
  UpdateProductionEntryRequest,
} from '../types'

/** Toàn bộ hàm gọi API của feature production nằm trong file này (CR-01 §7.2). */
export const productionApi = {
  getProductionPlans: (orderId: string, signal?: AbortSignal) =>
    apiClient.get<ProductionPlanListDto>(`/orders/${orderId}/production-plans`, { signal }),

  getProductionDay: (orderId: string, productionDate: IsoDate, signal?: AbortSignal) =>
    apiClient.get<ProductionDayDetailDto>(
      `/orders/${orderId}/production-days/${productionDate}`,
      { signal },
    ),

  /**
   * Ghi nhận thêm một lần trong ngày. Sản lượng là số cộng thêm, không phải giá trị thay thế.
   * Response là state đầy đủ của ngày, nên không cần refetch sau mutation.
   */
  createProductionEntry: (
    orderId: string,
    productionDate: IsoDate,
    request: CreateProductionEntryRequest,
  ) =>
    apiClient.post<ProductionDayDetailDto>(
      `/orders/${orderId}/production-days/${productionDate}/entries`,
      request,
    ),

  updateProductionEntry: (entryId: string, request: UpdateProductionEntryRequest) =>
    apiClient.put<ProductionDayDetailDto>(`/production-entries/${entryId}`, request),

  deleteProductionEntry: (entryId: string) =>
    apiClient.delete<ProductionDayDetailDto>(`/production-entries/${entryId}`),

  /** Xuất hàng. Body rỗng: sản lượng do server tính, client không gửi lên (CR-01 N-11). */
  closeProductionDay: (orderId: string, productionDate: IsoDate) =>
    apiClient.post<CloseProductionDayDto>(
      `/orders/${orderId}/production-days/${productionDate}/close`,
    ),
}
