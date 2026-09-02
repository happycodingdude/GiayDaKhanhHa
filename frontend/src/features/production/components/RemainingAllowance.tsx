import type { ReactNode } from 'react'
import { formatNumber } from '../../../shared/lib/format'
import type { ProductionDayDetailDto } from '../types'

/**
 * Tổng quan ngày. "Còn được nhập" là con số quan trọng nhất của màn hình: nó do server tính từ hai
 * ràng buộc chéo bảng — kế hoạch ngày và số lượng đơn hàng — nên không bao giờ được suy ra ở client
 * (CR-01 §7.4, N-03).
 */
function StatCard({
  tone,
  icon,
  label,
  value,
  hint,
}: {
  tone: 'blue' | 'amber' | 'green' | 'red'
  icon: ReactNode
  label: string
  value: string
  hint?: string
}) {
  return (
    <div className="day-stat">
      <span className={`day-stat__icon day-stat__icon--${tone}`} aria-hidden="true">
        {icon}
      </span>
      <div className="day-stat__body">
        <p className="day-stat__label">{label}</p>
        <p className="day-stat__value">{value}</p>
        {hint && <p className="day-stat__hint">{hint}</p>}
      </div>
    </div>
  )
}

const CalendarIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
    <rect x="3" y="5" width="18" height="16" rx="2" />
    <path d="M3 10h18M8 3v4M16 3v4" />
  </svg>
)

const ClockIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
    <circle cx="12" cy="12" r="9" />
    <path d="M12 7v5l3.5 2" />
  </svg>
)

const BoxIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round">
    <path d="M21 8 12 3 3 8v8l9 5 9-5V8Z" />
    <path d="m3 8 9 5 9-5M12 13v8" />
  </svg>
)

export function RemainingAllowance({ day }: { day: ProductionDayDetailDto }) {
  const closed = day.dayStatus === 'Closed'
  const shortage = day.shortageQuantity ?? 0

  return (
    <div className="day-stats">
      <StatCard
        tone="blue"
        icon={CalendarIcon}
        label="Kế hoạch ngày"
        value={`${formatNumber(day.plannedQuantity)} đôi`}
        hint={day.addOnQuantity > 0 ? `Đã bù thêm ${formatNumber(day.addOnQuantity)} đôi` : undefined}
      />

      <StatCard
        tone="amber"
        icon={ClockIcon}
        label={closed ? 'Đã xuất hàng' : 'Đã nhập hôm nay'}
        value={`${formatNumber(day.dayActualQuantity)} đôi`}
        hint={day.isProvisional ? 'Tạm tính — chưa xuất hàng' : undefined}
      />

      {/* Ngày đã chốt sổ không còn nhập được gì nữa, nên ô thứ ba đổi sang con số duy nhất còn
          đáng quan tâm của nó: phần thiếu (CR-01 OV-5). */}
      {closed ? (
        <StatCard
          tone={shortage > 0 ? 'red' : 'green'}
          icon={BoxIcon}
          label="Thiếu so với kế hoạch"
          value={shortage > 0 ? `${formatNumber(shortage)} đôi` : 'Không thiếu'}
        />
      ) : (
        <StatCard
          tone="green"
          icon={BoxIcon}
          label="Còn được nhập"
          value={`${formatNumber(day.remainingAllowance)} đôi`}
          hint={
            day.remainingAllowanceReason === 'OrderQuantity'
              ? `Giới hạn bởi số lượng còn lại của đơn hàng (${formatNumber(day.orderRemainingQuantity)} đôi)`
              : 'Giới hạn bởi kế hoạch của ngày'
          }
        />
      )}
    </div>
  )
}
