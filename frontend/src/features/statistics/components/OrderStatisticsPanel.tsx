import { Card } from '../../../shared/components/ui'
import { ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatQuantity } from '../../../shared/lib/format'
import { useOrderStatistics } from '../hooks/useStatistics'

/** Cumulative plan vs cumulative actual — all values derived by the backend (Step 4 §16). */
export function OrderStatisticsPanel({ orderId }: { orderId: number }) {
  const query = useOrderStatistics(orderId)

  return (
    <Card title="Thống kê lũy kế">
      {query.isPending && <LoadingState />}
      {query.isError && (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} title="Không tải được thống kê" />
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
                <th className="num">KH lũy kế</th>
                <th className="num">TT lũy kế</th>
              </tr>
            </thead>
            <tbody>
              {query.data.daily.map((day) => (
                <tr key={day.productionDate}>
                  <td>{formatDate(day.productionDate)}</td>
                  <td className="num">{formatNumber(day.plannedQuantity)}</td>
                  <td className="num">{formatQuantity(day.actualQuantity)}</td>
                  <td className={`num ${(day.difference ?? 0) < 0 ? 'danger' : (day.difference ?? 0) > 0 ? 'positive' : ''}`}>
                    {formatDifference(day.difference)}
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
