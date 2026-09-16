import { Fragment, useLayoutEffect, useRef } from 'react'
import { Badge, Button, Card } from '../../../shared/components/ui'
import { dateRange, formatShortDate, formatWeekday, today } from '../../../shared/lib/date'
import type { IsoDate } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatQuantity } from '../../../shared/lib/format'
import type { ProductionCellDto, ProductionMatrixDto, ProductionMatrixLineDto } from '../types'

/** Ô được người dùng chọn để thao tác. Ngày + dây chuyền là đủ để mở mọi dialog. */
export interface CellRef {
  productionDate: IsoDate
  productionLineId: string
  cell: ProductionCellDto
}

/**
 * Ma trận ngày × dây chuyền (CR-001 §7.8). Mỗi dây chuyền có ba con số — kế hoạch, thực tế, lệch —
 * và một cột thao tác riêng: `Nhập SL` khi ô đang sản xuất, `Xem` / `Xử lý thiếu` khi ô đã chốt
 * sổ. Xuất hàng thì chung cho cả ngày, nằm ở cột cuối.
 *
 * Cột ngày được ghim và vùng còn lại cuộn ngang: với 5+ dây chuyền, không ghim cột ngày thì cuộn
 * sang phải là mất luôn mốc để đọc (CR-001 §10, rủi ro đầu tiên).
 *
 * Ô không có kế hoạch hiển thị "—" và không có thao tác nào: server không sinh dòng kế hoạch cho
 * ô bằng 0, nên không có gì để nhập vào đó (BR-N10).
 */
export function ProductionMatrix({
  matrix,
  readOnly,
  orderCompleted,
  onRecord,
  onCloseDay,
  onViewCell,
  onHandleShortage,
}: {
  matrix: ProductionMatrixDto
  /** Đơn hàng đã qua ngày kết thúc chỉ đọc được; chỉ còn lại thao tác xem. */
  readOnly: boolean
  orderCompleted: boolean
  onRecord: (ref: CellRef) => void
  /** Xuất hàng cả ngày; kèm các dây chuyền còn mở của ngày đó, theo thứ tự cột. */
  onCloseDay: (productionDate: IsoDate, openLines: ProductionMatrixLineDto[]) => void
  onViewCell: (ref: CellRef) => void
  onHandleShortage: (ref: CellRef) => void
}) {
  const currentDate = today()
  const lines = matrix.productionLines
  const dates =
    matrix.startDate && matrix.dueDate ? dateRange(matrix.startDate, matrix.dueDate) : []
  const frameRef = useRef<HTMLDivElement>(null)
  const headScrollRef = useRef<HTMLDivElement>(null)
  const headTableRef = useRef<HTMLTableElement>(null)
  const bodyTableRef = useRef<HTMLTableElement>(null)

  /*
   * Tiêu đề nằm ngoài vùng cuộn dọc: con trỏ ở trên tiêu đề thì lăn chuột cuộn trang chứ không cuộn
   * bảng, và thanh cuộn dọc chỉ chạy dọc theo các dòng dữ liệu. Vì vậy tiêu đề là một bảng riêng, và
   * phải có đúng bề rộng cột của bảng dữ liệu.
   *
   * Bề rộng cột do nội dung quyết định, nên bảng dữ liệu vẫn giữ nguyên <thead> của nó — kéo lên khuất
   * phía trên vùng cuộn — để cột đủ rộng cho cả tiêu đề lẫn dữ liệu. Đo các ô tiêu đề đó rồi áp sang
   * bảng tiêu đề. Theo dõi kích thước từng ô vì bề rộng cột đổi cả khi dữ liệu đổi, không chỉ khi
   * khung đổi.
   */
  useLayoutEffect(() => {
    const frame = frameRef.current
    const headTable = headTableRef.current
    const bodyTable = bodyTableRef.current
    const bodyHead = bodyTable?.tHead
    if (!frame || !headTable || !bodyTable || !bodyHead) return

    const [groupRow, leafRow] = Array.from(bodyHead.rows)
    const cols = headTable.querySelectorAll('col')

    const syncHead = () => {
      // Thứ tự cột lá: Ngày, bốn cột của từng dây chuyền, Tổng TT, Xuất hàng. Ngày, Tổng TT và Xuất
      // hàng trải qua cả hai hàng tiêu đề nên nằm ở hàng đầu; cột con của dây chuyền ở hàng thứ hai.
      const groupCells = Array.from(groupRow.cells)
      const leafCells = [groupCells[0], ...Array.from(leafRow.cells), ...groupCells.slice(-2)]

      leafCells.forEach((cell, index) => {
        cols[index].style.width = `${cell.getBoundingClientRect().width}px`
      })
      headTable.style.width = `${bodyTable.getBoundingClientRect().width}px`
      frame.style.setProperty('--matrix-head-height', `${bodyHead.getBoundingClientRect().height}px`)
    }

    syncHead()
    const observer = new ResizeObserver(syncHead)
    for (const cell of [...groupRow.cells, ...leafRow.cells]) observer.observe(cell)
    return () => observer.disconnect()
  }, [lines])

  // Tra cứu theo ô. Backend trả phẳng để không phải join phía client; ma trận dựng lại ở đây.
  const byCell = new Map(
    matrix.items.map((item) => [`${item.productionDate}|${item.productionLineId}`, item]),
  )

  const rowActual = (date: IsoDate) =>
    lines.reduce(
      (sum, line) => sum + (byCell.get(`${date}|${line.id}`)?.actualQuantity ?? 0),
      0,
    )

  const rowHasAnyActual = (date: IsoDate) =>
    lines.some((line) => byCell.get(`${date}|${line.id}`)?.actualQuantity != null)

  const head = (
    <thead>
      <tr>
        <th className="matrix__date-col" rowSpan={2}>
          Ngày
        </th>
        {lines.map((line) => (
          <th key={line.id} colSpan={4} className="matrix__line-head matrix__group-start">
            {line.code}
            <span className="table__sub">
              {formatNumber(line.actualQuantity)} / {formatNumber(line.currentPlanQuantity)}
            </span>
          </th>
        ))}
        <th rowSpan={2} className="num matrix__group-start">
          Tổng TT
        </th>
        <th rowSpan={2} className="matrix__group-start matrix__day-action">
          Xuất hàng
        </th>
      </tr>
      <tr>
        {lines.map((line) => (
          <Fragment key={line.id}>
            <th className="num matrix__sub-head matrix__group-start">KH</th>
            <th className="num matrix__sub-head">TT</th>
            <th className="num matrix__sub-head">Lệch</th>
            <th className="matrix__sub-head" aria-label={`Thao tác ${line.code}`} />
          </Fragment>
        ))}
      </tr>
    </thead>
  )

  return (
    <Card
      title="Tiến độ sản xuất theo ngày × dây chuyền"
      description={
        <>
          <strong>KH</strong> = kế hoạch hiện tại · <strong>TT</strong> = sản lượng thực tế ·{' '}
          <strong>Lệch</strong> = TT − KH, là số tạm tính khi ngày chưa xuất hàng.
        </>
      }
    >
      <div className="table-wrapper matrix-frame" ref={frameRef}>
        {/* Bản sao chỉ để nhìn: trình đọc màn hình đọc <thead> gốc nằm cùng bảng với dữ liệu. */}
        <div className="matrix-frame__head" ref={headScrollRef} aria-hidden="true">
          <table className="table matrix matrix--data matrix-frame__head-table" ref={headTableRef}>
            <colgroup>
              {Array.from({ length: 1 + lines.length * 4 + 2 }, (_, index) => (
                <col key={index} />
              ))}
            </colgroup>
            {head}
          </table>
        </div>

        <div
          className="matrix-frame__body"
          // Tiêu đề không tự cuộn được, nên đi theo vị trí cuộn ngang của phần dữ liệu.
          onScroll={(event) => {
            if (headScrollRef.current) headScrollRef.current.scrollLeft = event.currentTarget.scrollLeft
          }}
        >
          <table className="table matrix matrix--data" ref={bodyTableRef}>
            {head}
            <tbody>
              {dates.map((date) => {
                const isToday = date === currentDate
                const dayCells = lines.flatMap((line) => {
                  const cell = byCell.get(`${date}|${line.id}`)
                  return cell ? [{ line, cell }] : []
                })
                const openLines = dayCells
                  .filter(({ cell }) => cell.dayStatus === 'InProduction')
                  .map(({ line }) => line)
                const allClosed =
                  dayCells.length > 0 && dayCells.every(({ cell }) => cell.dayStatus === 'Closed')

                return (
                  <tr key={date} className={isToday ? 'table__row--today' : ''}>
                    <td className="matrix__date-col">
                      <span className="table__strong">{formatShortDate(date)}</span>
                      <span className="table__sub">
                        {formatWeekday(date)}
                        {isToday && ' · Hôm nay'}
                      </span>
                    </td>

                    {lines.map((line) => {
                      const cell = byCell.get(`${date}|${line.id}`)

                      if (!cell) {
                        return (
                          <td key={line.id} colSpan={4} className="num muted matrix__empty matrix__group-start">
                            —
                          </td>
                        )
                      }

                      const ref: CellRef = { productionDate: date, productionLineId: line.id, cell }
                      // Ô còn mở chưa có chênh lệch chính thức (server trả null, CR-01 N-07), nhưng vẫn
                      // cần thấy đang cách kế hoạch bao nhiêu: tính tạm đúng công thức của server, TT − KH,
                      // gắn nhãn như cột TT. Không tô đỏ — ngày chưa chốt thì số hụt chưa phải phần thiếu.
                      const provisionalDifference =
                        cell.isProvisional && cell.actualQuantity !== null
                          ? cell.actualQuantity - cell.plannedQuantity
                          : null

                      return (
                        <Fragment key={line.id}>
                          <td className="num matrix__group-start">
                            {formatNumber(cell.plannedQuantity)}
                            {cell.addOnQuantity > 0 && (
                              <span className="addon"> +{formatNumber(cell.addOnQuantity)}</span>
                            )}
                          </td>
                          <td className="num">
                            {formatQuantity(cell.actualQuantity)}
                            {cell.isProvisional && cell.actualQuantity !== null && (
                              <span className="table__sub">Tạm tính</span>
                            )}
                          </td>
                          <td className={`num ${(cell.difference ?? 0) < 0 ? 'danger' : ''}`}>
                            {formatDifference(cell.difference ?? provisionalDifference)}
                            {provisionalDifference !== null && <span className="table__sub">Tạm tính</span>}
                          </td>
                          <td className="matrix__cell-actions">
                            <CellActions
                              cell={cell}
                              lineCode={line.code}
                              readOnly={readOnly}
                              orderCompleted={orderCompleted}
                              onRecord={() => onRecord(ref)}
                              onView={() => onViewCell(ref)}
                              onHandleShortage={() => onHandleShortage(ref)}
                            />
                          </td>
                        </Fragment>
                      )
                    })}

                    <td className="num table__strong matrix__group-start">
                      {rowHasAnyActual(date) ? formatNumber(rowActual(date)) : '—'}
                    </td>

                    <td className="matrix__group-start matrix__day-action">
                      {/* Đơn đã hoàn thành vẫn Xuất hàng được các ngày còn treo, để dọn sạch cảnh
                          báo (CR-01 §14.6); chỉ đơn đã qua ngày kết thúc mới bị khoá. */}
                      {openLines.length > 0 && !readOnly ? (
                        <Button
                          className="btn--sm"
                          onClick={() => onCloseDay(date, openLines)}
                          aria-label={`Xuất hàng ngày ${formatShortDate(date)}`}
                        >
                          Xuất hàng
                        </Button>
                      ) : allClosed ? (
                        <span className="positive">✓ Đã xuất</span>
                      ) : null}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </div>
    </Card>
  )
}

/**
 * Thao tác của một dây chuyền trong một ngày. Ô chưa tới ngày hoặc không có kế hoạch thì không có
 * gì để làm nên để trống.
 */
function CellActions({
  cell,
  lineCode,
  readOnly,
  orderCompleted,
  onRecord,
  onView,
  onHandleShortage,
}: {
  cell: ProductionCellDto
  lineCode: string
  readOnly: boolean
  orderCompleted: boolean
  onRecord: () => void
  onView: () => void
  onHandleShortage: () => void
}) {
  const label = `${lineCode} ngày ${formatShortDate(cell.productionDate)}`

  if (cell.dayStatus === 'InProduction') {
    // Đơn đã hoàn thành thì không ghi nhận thêm được nữa (CR-01 §14.6).
    if (readOnly || orderCompleted) return null

    return (
      <Button className="btn--sm" onClick={onRecord} aria-label={`Nhập sản lượng ${label}`}>
        Nhập SL
      </Button>
    )
  }

  if (cell.dayStatus !== 'Closed') return null

  // Phần thiếu chỉ xử lý được khi ngày đã Xuất hàng và đơn chưa hoàn thành (CR-01 §14.6).
  const canHandleShortage =
    !readOnly && !orderCompleted && (cell.shortageQuantity ?? 0) > 0 && !cell.hasActiveAdjustment

  return (
    <div className="matrix__actions">
      <Button className="btn--sm" onClick={onView} aria-label={`Xem chi tiết ${label}`}>
        Xem
      </Button>
      {canHandleShortage && (
        <Button
          variant="danger"
          className="btn--sm"
          onClick={onHandleShortage}
          aria-label={`Xử lý thiếu ${label}`}
        >
          Xử lý thiếu
        </Button>
      )}
      {cell.hasActiveAdjustment && <Badge tone="success">Đã bù</Badge>}
    </div>
  )
}
