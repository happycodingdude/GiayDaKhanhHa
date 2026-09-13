import { Card } from '../../../shared/components/ui'
import { ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { DayStatusBadge } from '../../production/components/DayStatusBadge'
import { formatDate, today } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatQuantity } from '../../../shared/lib/format'
import { useOrderStatistics } from '../hooks/useStatistics'

/**
 * Thống kê của một đơn hàng: tách theo dây chuyền, rồi kế hoạch lũy kế so với thực tế lũy kế theo
 * ngày. Mọi giá trị đều do backend suy ra (Step 4 §16).
 *
 * Bảng theo ngày GỘP mọi dây chuyền, nên cột chênh lệch chỉ có số khi MỌI ô của ngày đó đã xuất
 * hàng — còn một ô mở là cả ngày chưa chốt (CR-001 §6.9).
 */
export function OrderStatisticsPanel({ orderId }: { orderId: string }) {
  const query = useOrderStatistics(orderId)
  const currentDate = today()

  return (
    <Card title="Thống kê lũy kế">
      {query.isPending && <LoadingState />}
      {query.isError && (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} title="Không tải được thống kê" />
      )}

      {/* Tách theo dây chuyền: đường xu hướng bên dưới gộp mọi dây chuyền, nên nếu không có bảng
          này thì không nhìn ra dây chuyền nào đang kéo tiến độ xuống (CR-001 §6.9, §7.8). */}
      {query.data && query.data.byProductionLine.length > 0 && (
        <div className="table-wrapper">
          <table className="table">
            <thead>
              <tr>
                <th>Dây chuyền</th>
                <th className="num">Phân bổ</th>
                <th className="num">Kế hoạch</th>
                <th className="num">Thực tế</th>
                <th className="num">Thiếu</th>
              </tr>
            </thead>
            <tbody>
              {query.data.byProductionLine.map((line) => (
                <tr key={line.productionLineId}>
                  <td>
                    <span className="table__strong">{line.productionLineCode}</span>
                    <span className="table__sub">{line.productionLineName}</span>
                  </td>
                  <td className="num">{formatNumber(line.allocatedQuantity)}</td>
                  <td className="num">
                    {formatNumber(line.totalPlan)}
                    {line.totalPlan > line.allocatedQuantity && (
                      <span className="addon"> +{formatNumber(line.totalPlan - line.allocatedQuantity)}</span>
                    )}
                  </td>
                  <td className="num">{formatNumber(line.totalActual)}</td>
                  <td className={`num ${line.shortage > 0 ? 'danger' : ''}`}>
                    {line.shortage > 0 ? formatNumber(line.shortage) : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {query.data && (
        <div className="table-wrapper">
          <table className="table">
            <thead>
              <tr>
                <th>Ngày</th>
                <th className="num">Kế hoạch</th>
                <th className="num">Thực tế</th>
                <th className="num">Chênh lệch</th>
                <th>Tình trạng</th>
                <th className="num">KH lũy kế</th>
                <th className="num">TT lũy kế</th>
              </tr>
            </thead>
            <tbody>
              {query.data.daily.map((day) => (
                <tr key={day.productionDate}>
                  <td>{formatDate(day.productionDate)}</td>
                  <td className="num">{formatNumber(day.plannedQuantity)}</td>
                  <td className="num">
                    {formatQuantity(day.actualQuantity)}
                    {day.isProvisional && <span className="table__sub">Tạm tính</span>}
                  </td>
                  {/* Ngày chưa Xuất hàng để trống ô này: chưa có con số chính thức nào để so. */}
                  <td className={`num ${(day.difference ?? 0) < 0 ? 'danger' : ''}`}>
                    {formatDifference(day.difference)}
                  </td>
                  <td>
                    <DayStatusBadge status={day.dayStatus} isPastDay={day.productionDate < currentDate} />
                  </td>
                  <td className="num muted">{formatNumber(day.cumulativePlan)}</td>
                  <td className="num muted">{formatNumber(day.cumulativeActual)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  )
}
