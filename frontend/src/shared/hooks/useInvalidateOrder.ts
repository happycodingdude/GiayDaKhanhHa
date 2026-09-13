import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { queryKeys } from '../../app/config/queryKeys'

/**
 * Sau một mutation về sản xuất, các query bị ảnh hưởng sẽ được invalidate và refetch. Những thao
 * tác này cố ý KHÔNG dùng optimistic update: "Còn được nhập" là giá trị server tính từ hai ràng
 * buộc chéo bảng, nên client tự trừ sẽ có khoảnh khắc màn hình cho phép nhập tiếp trong khi server
 * đã từ chối (Step 5 §25, §31, CR-01 §7.4).
 */
export function useInvalidateOrder() {
  const queryClient = useQueryClient()

  /**
   * Sau khi ghi nhận / sửa / xoá một lần sản lượng (CR-01 §7.3). Ma trận cũng nằm trong danh
   * sách vì ô "Thực tế" của nó hiển thị số tạm tính của ô đang mở.
   */
  const invalidateAfterEntryChange = useCallback(
    async (orderId: string, productionDate: string, productionLineId: string) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.orderProductionCell(orderId, productionDate, productionLineId),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.order(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionPlans(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderStatistics(orderId) }),
        // Cả danh sách lẫn dashboard đều hiển thị số tổng suy ra từ sản lượng thực tế.
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
    [queryClient],
  )

  /**
   * Sau khi Xuất hàng: mọi ô của ngày đó cùng đổi trạng thái, và thêm lịch sử bù sản lượng, vì
   * đóng ngày là lúc phần thiếu mới xuất hiện (CR-01 §7.3).
   */
  const invalidateAfterDayClose = useCallback(
    async (orderId: string, productionDate: string) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionDay(orderId, productionDate) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.order(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionPlans(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderStatistics(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderPlanAdjustments(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
    [queryClient],
  )

  const invalidateAfterAdjustmentChange = useCallback(
    async (orderId: string) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.order(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderProductionPlans(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderStatistics(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.orderPlanAdjustments(orderId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.ordersList }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ])
    },
    [queryClient],
  )

  return { invalidateAfterEntryChange, invalidateAfterDayClose, invalidateAfterAdjustmentChange }
}
