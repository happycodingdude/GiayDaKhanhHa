import { useEffect, useRef } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button } from '../../../shared/components/ui'
import { Modal } from '../../../shared/dialogs/Modal'
import { ErrorState, InlineError, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate, formatTimestamp } from '../../../shared/lib/date'
import type { IsoDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { useCloseProductionDay, useProductionCells } from '../hooks/useProductionCell'
import type { CloseProductionDayDto, ProductionMatrixLineDto } from '../types'

/**
 * Xuất hàng — chốt sổ CẢ NGÀY: mọi dây chuyền còn mở của ngày đó đóng cùng lúc. Ô đã đóng là bất
 * biến (không sửa, không xoá, không mở lại), nên dialog giữ đúng các ràng buộc UX bắt buộc của
 * CR-01 §8.1, nay cho từng dây chuyền:
 *
 *   · hiển thị ĐẦY ĐỦ các lần ghi nhận của mọi dây chuyền cùng tổng đã ghi nhận, không thu gọn;
 *   · nút mặc định khi nhấn Enter là "Quay lại", không phải "Xác nhận";
 *   · câu cảnh báo nói rõ: không sửa, không xoá, không mở lại.
 *
 * Dialog tự tải state mới nhất của từng ô thay vì dùng số trên ma trận: con số sắp được chốt vĩnh
 * viễn phải là con số mới nhất của server chứ không phải bản sao trong bảng.
 */
export function CloseDayDialog({
  open,
  orderId,
  productionDate,
  lines,
  onClose,
  onClosed,
}: {
  open: boolean
  orderId: string
  productionDate: IsoDate
  /** Các dây chuyền còn mở của ngày, theo đúng thứ tự cột của ma trận. */
  lines: ProductionMatrixLineDto[]
  onClose: () => void
  onClosed: (result: CloseProductionDayDto) => void
}) {
  const queries = useProductionCells(
    orderId,
    productionDate,
    lines.map((line) => line.id),
  )
  const closeDay = useCloseProductionDay(orderId, productionDate)
  const cancelRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!open) return
    closeDay.reset()
    // Focus vào "Quay lại": Enter theo phản xạ không được chốt sổ cả một ngày.
    cancelRef.current?.focus()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const failed = queries.find((query) => query.isError)
  const cells = queries.flatMap((query) => (query.data ? [query.data] : []))
  const ready = failed === undefined && cells.length === lines.length

  const shortageOf = (planned: number, actual: number) => Math.max(planned - actual, 0)
  const totalPlanned = cells.reduce((sum, cell) => sum + cell.plannedQuantity, 0)
  const totalActual = cells.reduce((sum, cell) => sum + cell.dayActualQuantity, 0)
  const withoutEntries = cells.filter((cell) => cell.entries.length === 0)
  const hasEntries = cells.some((cell) => cell.entries.length > 0)

  const confirm = async () => {
    const result = await closeDay.mutateAsync()
    onClosed(result)
  }

  return (
    <Modal
      open={open}
      title="Xác nhận xuất hàng"
      description={
        cells[0] ? `${cells[0].shoeCode} · ${formatDate(productionDate)} · ${lines.length} dây chuyền` : undefined
      }
      onClose={onClose}
      width={760}
      footer={
        <>
          <Button ref={cancelRef} variant="primary" onClick={onClose} disabled={closeDay.isPending}>
            Quay lại
          </Button>
          <Button variant="primary" disabled={!ready} loading={closeDay.isPending} onClick={confirm}>
            Xác nhận xuất hàng
          </Button>
        </>
      }
    >
      {failed && (
        <ErrorState
          error={failed.error}
          onRetry={() => queries.forEach((query) => query.isError && void query.refetch())}
          title="Không tải được số liệu của ngày"
        />
      )}

      {!failed && !ready && <LoadingState />}

      {ready && (
        <>
          <div className="table-wrapper">
            <table className="table">
              <thead>
                <tr>
                  <th>Dây chuyền</th>
                  <th className="num">Kế hoạch</th>
                  <th className="num">Đã ghi nhận</th>
                  <th className="num">Số lần</th>
                  <th className="num">Thiếu</th>
                </tr>
              </thead>
              <tbody>
                {cells.map((cell) => {
                  const shortage = shortageOf(cell.plannedQuantity, cell.dayActualQuantity)

                  return (
                    <tr key={cell.productionLineId}>
                      <td className="table__strong">{cell.productionLineCode}</td>
                      <td className="num">{formatNumber(cell.plannedQuantity)}</td>
                      <td className="num table__strong">{formatNumber(cell.dayActualQuantity)}</td>
                      <td className="num">{cell.entries.length}</td>
                      <td className={`num ${shortage > 0 ? 'danger table__strong' : 'muted'}`}>
                        {shortage > 0 ? formatNumber(shortage) : 'Không thiếu'}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
              {cells.length > 1 && (
                <tfoot>
                  <tr>
                    <th>Cả ngày</th>
                    <th className="num">{formatNumber(totalPlanned)}</th>
                    <th className="num">{formatNumber(totalActual)}</th>
                    <th />
                    <th className="num">{formatNumber(shortageOf(totalPlanned, totalActual))}</th>
                  </tr>
                </tfoot>
              )}
            </table>
          </div>

          {withoutEntries.length > 0 && (
            <p className="notice notice--warning">
              <strong>{withoutEntries.map((cell) => cell.productionLineCode).join(', ')}</strong> chưa ghi
              nhận lần nào.
              Xuất hàng bây giờ nghĩa là sản lượng của {withoutEntries.length > 1 ? 'các dây chuyền này' : 'dây chuyền này'}{' '}
              bằng <strong>0</strong> và toàn bộ kế hoạch được ghi nhận là thiếu.
            </p>
          )}

          {/* Danh sách hiển thị đầy đủ, không thu gọn: quản lý phải nhìn qua toàn bộ số đã nhập
              trước khi chốt một con số không sửa lại được (CR-01 §8.1). Bảng tự cuộn khi dài — vẫn
              là danh sách đầy đủ, chỉ để modal không cao quá khung nhìn. */}
          {hasEntries && (
            <div className="table-wrapper modal-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>Dây chuyền</th>
                    <th>Thời điểm</th>
                    <th className="num">Số lượng</th>
                    <th className="num">Lũy kế</th>
                    <th className="table__col--fill">Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {cells.flatMap((cell) =>
                    cell.entries.map((entry) => (
                      <tr key={entry.id}>
                        <td className="table__strong">{cell.productionLineCode}</td>
                        <td>{formatTimestamp(entry.recordedAt)}</td>
                        <td className="num">+{formatNumber(entry.quantity)}</td>
                        <td className="num">{formatNumber(entry.runningTotal)}</td>
                        <td>
                          <span className="table__truncate" title={entry.note ?? undefined}>
                            {entry.note ?? '—'}
                          </span>
                        </td>
                      </tr>
                    )),
                  )}
                </tbody>
              </table>
            </div>
          )}

          <p className="notice notice--danger">
            ⚠ Sau khi xuất hàng, ngày <strong>{formatDate(productionDate)}</strong> của{' '}
            {lines.length > 1 ? <strong>cả {lines.length} dây chuyền</strong> : 'dây chuyền này'} được chốt
            sổ vĩnh viễn:{' '}
            <strong>không sửa, không xoá, không mở lại</strong>. Hãy kiểm tra lại toàn bộ số lượng ở trên
            trước khi xác nhận.
          </p>
        </>
      )}

      {closeDay.isError && <InlineError message={toUserMessage(closeDay.error)} />}
    </Modal>
  )
}
