import { Badge, Button, Card } from '../../../shared/components/ui'
import { formatDate, formatWeekday, today } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatQuantity } from '../../../shared/lib/format'
import { DayStatusBadge } from './DayStatusBadge'
import type { ProductionDayDto } from '../types'

/**
 * Bảng sản xuất theo ngày. Kế hoạch ban đầu, phần bù và kế hoạch hiện tại được tách riêng để quản lý
 * thấy được vì sao kế hoạch của một ngày thay đổi (order detail spec §3.3).
 *
 * Sau CR-01: ngày còn mở hiển thị sản lượng TẠM TÍNH và để trống cột chênh lệch — phần thiếu chỉ
 * tồn tại khi ngày đã Xuất hàng, nên lối vào Xử lý thiếu cũng chỉ có ở ngày đã đóng (OV-5).
 */
export function ProductionTimeline({
  days,
  readOnly,
  orderCompleted,
  onRecord,
  onCloseDay,
  onViewDay,
  onHandleShortage,
}: {
  days: ProductionDayDto[]
  /** Đơn hàng quá hạn chỉ đọc được; mọi cột thao tác thu lại thành dấu gạch. */
  readOnly: boolean
  orderCompleted: boolean
  onRecord: (day: ProductionDayDto) => void
  onCloseDay: (day: ProductionDayDto) => void
  onViewDay: (day: ProductionDayDto) => void
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
              const isPastDay = day.productionDate < currentDate
              const isClosed = day.dayStatus === 'Closed'

              // Ngày chưa tới thì không ghi nhận được; ngày không có kế hoạch cũng vậy.
              const canRecord = !readOnly && day.dayStatus === 'InProduction' && !orderCompleted

              // Ngày đã qua hoặc đang diễn ra đều chốt sổ được — kể cả khi đơn đã hoàn thành, để
              // các ngày còn treo không nằm mãi trong danh sách chưa xuất hàng (CR-01 §14.6).
              const canCloseDay = !readOnly && day.dayStatus === 'InProduction'

              const shortage = day.shortageQuantity ?? 0
              // Phần thiếu chỉ xử lý được khi ngày đã Xuất hàng và đơn chưa hoàn thành (CR-01 §14.6).
              const canHandleShortage =
                !readOnly && isClosed && shortage > 0 && !day.hasActiveAdjustment && !orderCompleted

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
                  {/* Ngày chưa ghi nhận lần nào hiển thị gạch ngang, không bao giờ là 0. */}
                  <td className="num">
                    {formatQuantity(day.actualQuantity)}
                    {day.isProvisional && <span className="table__sub">Tạm tính</span>}
                  </td>
                  {/* Ngày còn mở để trống cột chênh lệch: chưa có con số chính thức nào để so. */}
                  <td className={`num ${(day.difference ?? 0) < 0 ? 'danger' : ''}`}>
                    {formatDifference(day.difference)}
                  </td>
                  <td>
                    <DayStatusBadge status={day.dayStatus} isPastDay={isPastDay} />
                  </td>
                  <td className="table__actions">
                    <div>
                      {/* Nhập sản lượng và Xuất hàng đứng cạnh nhau trong cùng một nhóm thao tác:
                          cả hai đều mở modal, không rời khỏi màn hình chi tiết đơn hàng. */}
                      {canRecord && (
                        <Button variant="primary" onClick={() => onRecord(day)}>
                          Nhập sản lượng
                        </Button>
                      )}

                      {canCloseDay && (
                        <Button variant="primary" onClick={() => onCloseDay(day)}>
                          Xuất hàng
                        </Button>
                      )}

                      {/* Ngày đã chốt sổ chỉ còn xem lại. Xử lý thiếu là nút riêng bên cạnh, không
                          nằm trong màn hình xem chi tiết. */}
                      {isClosed && (
                        <Button variant="primary" onClick={() => onViewDay(day)}>
                          Xem chi tiết
                        </Button>
                      )}

                      {canHandleShortage && (
                        <Button variant="primary" onClick={() => onHandleShortage(day)}>
                          Xử lý thiếu
                        </Button>
                      )}

                      {!canRecord && !canCloseDay && !isClosed && !day.hasActiveAdjustment && (
                        <span className="muted">—</span>
                      )}

                      {/* Bám sau nút: phần thiếu đã được bù là trạng thái chứ không phải thao tác —
                          cũng vì thế nó vẫn hiện khi đơn hàng chỉ đọc. */}
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
