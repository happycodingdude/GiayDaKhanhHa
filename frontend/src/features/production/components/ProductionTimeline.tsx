import { Button, Card } from '../../../shared/components/ui'
import { DayStatusBadge } from '../../../shared/components/StatusBadges'
import { formatDate, formatWeekday, today } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatQuantity } from '../../../shared/lib/format'
import type { ProductionDayDto } from '../types'

/**
 * The daily production table. Initial plan, add-on and current plan are shown separately so the
 * manager can see why a day's plan changed (order detail spec §3.3).
 */
export function ProductionTimeline({
  days,
  orderCompleted,
  onEnterActual,
  onHandleShortage,
}: {
  days: ProductionDayDto[]
  orderCompleted: boolean
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
                    />
                  </td>
                  <td className="table__actions">
                    <div>
                      {day.plannedQuantity === 0 ? (
                        <span className="muted">—</span>
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
                          {day.hasActiveAdjustment && <span className="muted">Đã bù</span>}
                        </>
                      )}
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
