import { useEffect, useRef } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button } from '../../../shared/components/ui'
import { Modal } from '../../../shared/dialogs/Modal'
import { ErrorState, InlineError, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate, formatTimestamp } from '../../../shared/lib/date'
import type { IsoDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { useCloseProductionDay, useProductionDay } from '../hooks/useProductionDay'
import type { CloseProductionDayDto } from '../types'

/**
 * Xuất hàng — chốt sổ ngày sản xuất. Ngày đã đóng là bất biến (không sửa, không xoá, không mở lại),
 * nên dialog này tuân thủ đúng các ràng buộc UX bắt buộc của CR-01 §8.1:
 *
 *   · hiển thị ĐẦY ĐỦ danh sách các lần ghi nhận cùng tổng đã ghi nhận, không thu gọn;
 *   · nút mặc định khi nhấn Enter là "Quay lại", không phải "Xác nhận";
 *   · câu cảnh báo nói rõ: không sửa, không xoá, không mở lại.
 *
 * Dialog tự tải state của ngày thay vì nhận qua props: nó được mở thẳng từ bảng tiến độ, và con số
 * sắp được chốt vĩnh viễn phải là con số mới nhất của server chứ không phải bản sao trong bảng.
 */
export function CloseDayDialog({
  open,
  orderId,
  productionDate,
  onClose,
  onClosed,
}: {
  open: boolean
  orderId: string
  productionDate: IsoDate
  onClose: () => void
  onClosed: (result: CloseProductionDayDto) => void
}) {
  const query = useProductionDay(orderId, productionDate)
  const closeDay = useCloseProductionDay(orderId, productionDate)
  const cancelRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!open) return
    closeDay.reset()
    // Focus vào "Quay lại": Enter theo phản xạ không được chốt sổ một ngày.
    cancelRef.current?.focus()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const day = query.data
  const shortage = day === undefined ? 0 : Math.max(day.plannedQuantity - day.dayActualQuantity, 0)

  const confirm = async () => {
    const result = await closeDay.mutateAsync()
    onClosed(result)
  }

  return (
    <Modal
      open={open}
      title="Xác nhận xuất hàng"
      description={day ? `${day.orderCode} · ${formatDate(productionDate)}` : undefined}
      onClose={onClose}
      width={720}
      footer={
        <>
          <Button ref={cancelRef} variant="primary" onClick={onClose} disabled={closeDay.isPending}>
            Quay lại
          </Button>
          <Button
            variant="primary"
            disabled={day === undefined}
            loading={closeDay.isPending}
            onClick={confirm}
          >
            Xác nhận xuất hàng
          </Button>
        </>
      }
    >
      {query.isPending && <LoadingState />}

      {query.isError && (
        <ErrorState
          error={query.error}
          onRetry={() => void query.refetch()}
          title="Không tải được ngày sản xuất"
        />
      )}

      {day && (
        <>
          <dl className="summary-list summary-list--compact">
            <div>
              <dt>Kế hoạch ngày</dt>
              <dd>{formatNumber(day.plannedQuantity)} đôi</dd>
            </div>
            <div>
              <dt>Tổng đã ghi nhận</dt>
              <dd className="strong">{formatNumber(day.dayActualQuantity)} đôi</dd>
            </div>
            <div>
              <dt>Số lần ghi nhận</dt>
              <dd>{day.entries.length} lần</dd>
            </div>
            <div>
              <dt>Thiếu so với kế hoạch</dt>
              <dd className={shortage > 0 ? 'danger strong' : ''}>
                {shortage > 0 ? `${formatNumber(shortage)} đôi` : 'Không thiếu'}
              </dd>
            </div>
          </dl>

          {day.entries.length === 0 ? (
            <p className="notice notice--warning">
              Ngày này chưa ghi nhận lần nào. Xuất hàng bây giờ nghĩa là sản lượng của ngày bằng{' '}
              <strong>0</strong> và toàn bộ {formatNumber(day.plannedQuantity)} đôi kế hoạch sẽ được
              ghi nhận là thiếu.
            </p>
          ) : (
            /* Danh sách hiển thị đầy đủ, không thu gọn: quản lý phải nhìn qua toàn bộ số đã nhập
               trước khi chốt một con số không sửa lại được (CR-01 §8.1). */
            <div className="table-wrapper">
              <table className="table">
                <thead>
                  <tr>
                    <th>Thời điểm</th>
                    <th className="num">Số lượng</th>
                    <th className="num">Lũy kế</th>
                    <th>Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {day.entries.map((entry) => (
                    <tr key={entry.id}>
                      <td>{formatTimestamp(entry.recordedAt)}</td>
                      <td className="num">+{formatNumber(entry.quantity)}</td>
                      <td className="num">{formatNumber(entry.runningTotal)}</td>
                      <td>{entry.note ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <p className="notice notice--danger">
            ⚠ Sau khi xuất hàng, ngày sản xuất này được chốt sổ vĩnh viễn:{' '}
            <strong>không sửa, không xoá, không mở lại</strong>. Hãy kiểm tra lại toàn bộ số lượng ở
            trên trước khi xác nhận.
          </p>
        </>
      )}

      {closeDay.isError && <InlineError message={toUserMessage(closeDay.error)} />}
    </Modal>
  )
}
