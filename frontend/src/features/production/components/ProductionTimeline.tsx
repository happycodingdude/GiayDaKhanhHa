import { Badge, Button, Card } from '../../../shared/components/ui'
import { DayStatusBadge } from '../../../shared/components/StatusBadges'
import { formatDate, formatWeekday, today } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatQuantity } from '../../../shared/lib/format'
import type { ProductionDayDto } from '../types'

/**
 * Bảng sản xuất theo ngày. Kế hoạch ban đầu, phần bù và kế hoạch hiện tại được tách riêng để
 * quản lý thấy được vì sao kế hoạch của một ngày thay đổi (order detail spec §3.3).
 */
export function ProductionTimeline({
  days,
  orderCompleted,
  readOnly,
  onEnterActual,
  onHandleShortage,
}: {
  days: ProductionDayDto[]
  orderCompleted: boolean
  /** Đơn hàng quá hạn chỉ đọc được; mọi cột thao tác thu lại thành dấu gạch. */
  readOnly: boolean
  onEnterActual: (day: ProductionDayDto) => void
  onHandleShortage: (day: ProductionDayDto) => void
}) {
  const currentDate = today()

  return (
    <Card title="Tiến độ sản xuất theo ngày">
      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Ngày</th>
              <th className="num">KH ban đầu</th>
              <th className="num">Bù thêm</th>
              <th className="num">KH cuối</th>
              <th className="num">Thực tế</th>
              <th className="num">Chênh lệch</th>
              <th>Tình trạng</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {days.map((day) => {
              const isToday = day.productionDate === currentDate
              const hasActual = day.actualQuantity !== null
              // Thực tế là số đã sản xuất, nên ngày chưa tới thì không nhập được.
              const isFuture = day.productionDate > currentDate

              return (
                <tr key={day.id} className={isToday ? 'table__row--today' : ''}>
                  <td>
                    <span className="table__strong">{formatDate(day.productionDate)}</span>
                    <span className="table__sub">
                      {formatWeekday(day.productionDate)}
                      {isToday && ' · Hôm nay'}
                    </span>
                  </td>
                  <td className="num">{formatNumber(day.initialPlannedQuantity)}</td>
                  <td className="num">
                    {day.addOnQuantity > 0 ? (
                      <span className="addon">+{formatNumber(day.addOnQuantity)}</span>
                    ) : (
                      '—'
                    )}
                  </td>
                  <td className="num table__strong">{formatNumber(day.plannedQuantity)}</td>
                  {/* A day with no record shows an em dash, never 0 (order detail spec §3.3). */}
                  <td className="num">{formatQuantity(day.actualQuantity)}</td>
                  <td className={`num ${(day.difference ?? 0) < 0 ? 'danger' : (day.difference ?? 0) > 0 ? 'positive' : ''}`}>
                    {formatDifference(day.difference)}
                  </td>
                  <td>
                    <DayStatusBadge
                      actualQuantity={day.actualQuantity}
                      shortageQuantity={day.shortageQuantity}
                      difference={day.difference}
                      plannedQuantity={day.plannedQuantity}
                      dayPosition={isFuture ? 'future' : isToday ? 'today' : 'past'}
                    />
                  </td>
                  <td className="table__actions">
                    <div>
                      {readOnly || day.plannedQuantity === 0 || (isFuture && !hasActual) ? (
                        !day.hasActiveAdjustment && <span className="muted">—</span>
                      ) : (
                        <>
                          {/* A completed order takes no further entry; corrections stay available. */}
                          {(!orderCompleted || hasActual) && (
                            <Button onClick={() => onEnterActual(day)}>
                              {hasActual ? 'Sửa sản lượng' : 'Nhập sản lượng'}
                            </Button>
                          )}
                          {day.shortageQuantity > 0 && !day.hasActiveAdjustment && !orderCompleted && (
                            <Button variant="primary" onClick={() => onHandleShortage(day)}>
                              Xử lý thiếu
                            </Button>
                          )}
                        </>
                      )}

                      {/* Trails the button: the shortage being handled is state, not an action —
                          which is also why it survives read-only. */}
                      {day.hasActiveAdjustment && <Badge tone="success">Đã bù</Badge>}
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </Card>
  )
}
