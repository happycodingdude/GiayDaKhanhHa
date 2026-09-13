import { apiClient } from '../../../api/client'
import type {
  CreateProductionScheduleRequest,
  OrderDetailDto,
  OrderListFilters,
  OrderListItemDto,
  PagedResult,
  ReceiveOrderRequest,
  UpdateOrderRequest,
} from '../types'

export const ordersApi = {
  /** Nhập hàng. multipart vì ảnh mẫu đi kèm ngay ở bước tạo (CR-001 §6.4). */
  receive: (request: ReceiveOrderRequest) => {
    const form = new FormData()
    form.append('shoeCode', request.shoeCode)
    form.append('quantity', String(request.quantity))
    if (request.image) form.append('image', request.image)

    return apiClient.upload<OrderDetailDto>('POST', '/orders', form)
  },

  /** Sửa nhập hàng. multipart vì ảnh mới đi kèm, để thông tin và ảnh được lưu trong một lần. */
  update: (orderId: string, request: UpdateOrderRequest) => {
    const form = new FormData()
    form.append('shoeCode', request.shoeCode)
    form.append('quantity', String(request.quantity))
    if (request.image) form.append('image', request.image)
    if (request.removeImage) form.append('removeImage', 'true')

    return apiClient.upload<OrderDetailDto>('PUT', `/orders/${orderId}`, form)
  },

  /** Xoá đơn nhập hàng. Chỉ đơn chưa lập tiến độ mới xoá được; server trả 204. */
  remove: (orderId: string) => apiClient.delete<void>(`/orders/${orderId}`),

  list: (filters: OrderListFilters, signal?: AbortSignal) =>
    apiClient.get<PagedResult<OrderListItemDto>>('/orders', {
      signal,
      query: {
        status: filters.status,
        search: filters.search,
        productionLineId: filters.productionLineId,
        page: filters.page,
        pageSize: filters.pageSize,
      },
    }),

  getById: (orderId: string, signal?: AbortSignal) =>
    apiClient.get<OrderDetailDto>(`/orders/${orderId}`, { signal }),

  /** Lập tiến độ: dây chuyền + khoảng ngày + phân bổ 2 tầng, một lần duy nhất (BR-N05). */
  createSchedule: (orderId: string, request: CreateProductionScheduleRequest) =>
    apiClient.post<OrderDetailDto>(`/orders/${orderId}/production-schedule`, request),
}
