import type { OrderListFilters } from '../../features/orders/types'

/**
 * Central query-key definitions so mutations invalidate exactly the keys the approved frontend
 * architecture specifies (Step 5 §25).
 */
export const queryKeys = {
  currentUser: ['auth', 'me'] as const,

  /** Prefix for every order-list query, regardless of filters. */
  ordersList: ['orders', 'list'] as const,
  ordersListFiltered: (filters: OrderListFilters) => ['orders', 'list', filters] as const,

  order: (orderId: number) => ['orders', orderId] as const,
  orderProductionPlans: (orderId: number) => ['orders', orderId, 'production-plans'] as const,
  orderStatistics: (orderId: number) => ['orders', orderId, 'statistics'] as const,
  orderPlanAdjustments: (orderId: number) => ['orders', orderId, 'plan-adjustments'] as const,

  dashboard: ['statistics', 'dashboard'] as const,
}
