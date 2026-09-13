import { Badge } from '../../../shared/components/ui'
import { formatNumber } from '../../../shared/lib/format'
import type { OrderProductionLineDto } from '../types'

/**
 * Các dây chuyền của một đơn hàng. Dây chuyền đã ngừng hoạt động vẫn hiển thị bình thường — dữ liệu
 * lịch sử không biến mất vì một thay đổi cấu hình (CR-001 BR-N14).
 */
export function ProductionLineTags({
  lines,
  showAllocation = false,
}: {
  lines: OrderProductionLineDto[]
  showAllocation?: boolean
}) {
  if (lines.length === 0) return <span className="muted">—</span>

  return (
    <span className="line-tags">
      {lines.map((line) => (
        <Badge key={line.id} tone={line.status === 'Active' ? 'info' : 'neutral'}>
          {line.code}
          {showAllocation && ` · ${formatNumber(line.allocatedQuantity)}`}
        </Badge>
      ))}
    </span>
  )
}
