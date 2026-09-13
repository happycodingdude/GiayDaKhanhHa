import { useState } from 'react'
import { Button, Card } from '../../../shared/components/ui'
import { ConfirmDialog } from '../../../shared/dialogs/ConfirmDialog'
import { dateRange, formatShortDate, formatWeekday } from '../../../shared/lib/date'
import type { IsoDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import type { ProductionLineDto } from '../../production-lines/types'
import { cellKey, splitEvenly, toQuantity } from '../lib/allocation'

/**
 * Bước 4 — phân bổ dây chuyền cho từng ngày, tầng 2 (CR-001 §7.6).
 *
 * Ma trận được khởi tạo sẵn bằng cách chia đều số của từng dây chuyền cho các ngày. Ràng buộc duy
 * nhất là tổng mỗi CỘT phải khớp số đã phân ở bước 3 (BR-N08b) — tổng theo hàng không bị ràng buộc
 * gì cả, vì hai dây chuyền hoàn toàn có thể chạy lệch nhịp nhau.
 *
 * Ô để 0 nghĩa là dây chuyền đó nghỉ ngày đó, và sẽ không tạo dòng kế hoạch nào (BR-N10).
 */
export function PlanMatrixStep({
  startDate,
  dueDate,
  lines,
  allocations,
  matrix,
  onMatrixChange,
  onBack,
  onNext,
}: {
  startDate: IsoDate
  dueDate: IsoDate
  lines: ProductionLineDto[]
  /** Mốc từ bước 3, read-only ở màn này. */
  allocations: Record<string, number>
  matrix: Record<string, string>
  onMatrixChange: (matrix: Record<string, string>) => void
  onBack: () => void
  onNext: () => void
}) {
  const [confirmingReset, setConfirmingReset] = useState(false)
  const dates = dateRange(startDate, dueDate)

  const columnTotal = (lineId: string) =>
    dates.reduce((sum, date) => sum + toQuantity(matrix[cellKey(lineId, date)]), 0)

  const rowTotal = (date: IsoDate) =>
    lines.reduce((sum, line) => sum + toQuantity(matrix[cellKey(line.id, date)]), 0)

  const grandTotal = lines.reduce((sum, line) => sum + columnTotal(line.id), 0)
  const allocatedTotal = lines.reduce((sum, line) => sum + (allocations[line.id] ?? 0), 0)
  const everyColumnMatches = lines.every((line) => columnTotal(line.id) === (allocations[line.id] ?? 0))

  const resetToEven = () => {
    const next: Record<string, string> = {}

    for (const line of lines) {
      const shares = splitEvenly(allocations[line.id] ?? 0, dates.length)
      dates.forEach((date, index) => {
        next[cellKey(line.id, date)] = String(shares[index])
      })
    }

    onMatrixChange(next)
    setConfirmingReset(false)
  }

  return (
    <Card
      title="Kế hoạch theo ngày"
      description={`${formatShortDate(startDate)} → ${formatShortDate(dueDate)} · ${dates.length} ngày`}
      actions={<Button onClick={() => setConfirmingReset(true)}>Chia đều lại</Button>}
    >
      <div className="table-wrapper matrix-scroll">
        <table className="table matrix">
          <thead>
            <tr>
              <th className="matrix__date-col">Ngày</th>
              {lines.map((line) => (
                <th key={line.id} className="num">
                  {line.code}
                </th>
              ))}
              <th className="num">Tổng</th>
            </tr>
          </thead>
          <tbody>
            {dates.map((date) => (
              <tr key={date}>
                <td className="matrix__date-col">
                  <span className="table__strong">{formatShortDate(date)}</span>
                  <span className="table__sub">{formatWeekday(date)}</span>
                </td>
                {lines.map((line) => (
                  <td key={line.id} className="num">
                    <input
                      className="input input--number"
                      inputMode="numeric"
                      placeholder="0"
                      value={matrix[cellKey(line.id, date)] ?? ''}
                      aria-label={`Kế hoạch ${line.code} ngày ${formatShortDate(date)}`}
                      onChange={(event) => {
                        const next = event.target.value
                        if (next !== '' && !/^\d+$/.test(next)) return
                        onMatrixChange({ ...matrix, [cellKey(line.id, date)]: next })
                      }}
                    />
                  </td>
                ))}
                <td className="num table__strong">{formatNumber(rowTotal(date))}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <th className="matrix__date-col">Tổng</th>
              {lines.map((line) => (
                <th key={line.id} className="num">
                  {formatNumber(columnTotal(line.id))}
                </th>
              ))}
              <th className="num">{formatNumber(grandTotal)}</th>
            </tr>
            {/* Dòng mốc từ bước 3: đây là con số mà mỗi cột phải khớp (BR-N08b). */}
            <tr>
              <th className="matrix__date-col">Phân bổ</th>
              {lines.map((line) => {
                const allocated = allocations[line.id] ?? 0
                const actual = columnTotal(line.id)
                const gap = actual - allocated

                return (
                  <th key={line.id} className={`num ${gap === 0 ? 'positive' : 'danger'}`}>
                    {formatNumber(allocated)}
                    {gap === 0 ? (
                      ' ✓'
                    ) : (
                      <span className="matrix__gap">
                        {gap < 0 ? `thiếu ${formatNumber(-gap)}` : `vượt ${formatNumber(gap)}`}
                      </span>
                    )}
                  </th>
                )
              })}
              <th className={`num ${everyColumnMatches ? 'positive' : 'danger'}`}>
                {formatNumber(allocatedTotal)}
                {everyColumnMatches && ' ✓'}
              </th>
            </tr>
          </tfoot>
        </table>
      </div>

      {!everyColumnMatches && (
        <p className="notice notice--warning">
          Tổng của mỗi dây chuyền phải khớp đúng số đã phân bổ ở bước trước. Các cột đang lệch được
          tô đỏ kèm số chênh.
        </p>
      )}

      <p className="field__hint">
        Ô để <strong>0</strong> nghĩa là dây chuyền đó nghỉ ngày đó. Ô 0 sẽ không có kế hoạch và
        không nhập được sản lượng.
      </p>

      <div className="form__actions">
        <Button onClick={onBack}>Quay lại</Button>
        <Button variant="primary" disabled={!everyColumnMatches} onClick={onNext}>
          Xem lại
        </Button>
      </div>

      <ConfirmDialog
        open={confirmingReset}
        title="Chia đều lại toàn bộ ma trận?"
        confirmLabel="Chia đều lại"
        onCancel={() => setConfirmingReset(false)}
        onConfirm={resetToEven}
      >
        <p>
          Toàn bộ con số bạn đã sửa trong ma trận sẽ bị thay bằng kết quả chia đều. Thao tác này không
          hoàn tác được.
        </p>
      </ConfirmDialog>
    </Card>
  )
}
