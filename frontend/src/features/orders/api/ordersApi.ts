import { apiClient } from '../../../api/client'
import type {
  CreateOrderRequest,
  OrderDetailDto,
  OrderListFilters,
  OrderListItemDto,
  PagedResult,
} from '../types'

export const ordersApi = {
  /** Creates the order together with its initial production plans, in one transaction. */
  create: (request: CreateOrderRequest) => apiClient.post<OrderDetailDto>('/orders', request),

  list: (filters: OrderListFilters, signal?: AbortSignal) =>
    apiClient.get<PagedResult<OrderListItemDto>>('/orders', {
      signal,
      query: {
        status: filters.status,
        search: filters.search,
        page: filters.page,
        pageSize: filters.pageSize,
      },
    }),

  getById: (orderId: number, signal?: AbortSignal) =>
    apiClient.get<OrderDetailDto>(`/orders/${orderId}`, { signal }),
}
