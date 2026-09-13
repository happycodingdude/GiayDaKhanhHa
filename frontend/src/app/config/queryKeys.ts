import type { OrderListFilters } from '../../features/orders/types'
import type { ProductionLineStatus } from '../../features/production-lines/types'

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
  /** Prefix cho mọi ô của một ngày — Xuất hàng chốt sổ cả ngày một lượt. */
  orderProductionDay: (orderId: string, productionDate: string) =>
    ['orders', orderId, 'production-days', productionDate] as const,
  /** Một ô của ma trận: ngày × dây chuyền (CR-001 §2 QĐ-3). */
  orderProductionCell: (orderId: string, productionDate: string, productionLineId: string) =>
    ['orders', orderId, 'production-days', productionDate, productionLineId] as const,

  /** Prefix cho mọi query dây chuyền, bất kể bộ lọc trạng thái nào. */
  productionLines: ['production-lines'] as const,
  productionLinesFiltered: (status?: ProductionLineStatus) =>
    ['production-lines', status ?? 'All'] as const,

  settings: ['settings'] as const,

  dashboard: ['statistics', 'dashboard'] as const,
}
