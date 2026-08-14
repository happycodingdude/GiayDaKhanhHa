import type { OrderListFilters } from '../../features/orders/types'

/**
 * Định nghĩa query key tập trung, để mutation invalidate đúng những key mà kiến trúc frontend
 * đã duyệt quy định (Step 5 §25).
 */
export const queryKeys = {
  currentUser: ['auth', 'me'] as const,

  /** Prefix cho mọi query danh sách đơn hàng, bất kể bộ lọc nào. */
  ordersList: ['orders', 'list'] as const,
  ordersListFiltered: (filters: OrderListFilters) => ['orders', 'list', filters] as const,

  order: (orderId: string) => ['orders', orderId] as const,
  orderProductionPlans: (orderId: string) => ['orders', orderId, 'production-plans'] as const,
  orderStatistics: (orderId: string) => ['orders', orderId, 'statistics'] as const,
  orderPlanAdjustments: (orderId: string) => ['orders', orderId, 'plan-adjustments'] as const,

  dashboard: ['statistics', 'dashboard'] as const,
}
