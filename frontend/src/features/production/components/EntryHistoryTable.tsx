import { useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Badge, Button } from '../../../shared/components/ui'
import { Modal } from '../../../shared/dialogs/Modal'
import { EmptyState, InlineError } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatTimestamp } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { useDeleteProductionEntry, useUpdateProductionEntry } from '../hooks/useProductionDay'
import type { ProductionDayDetailDto, ProductionEntryDto } from '../types'

/**
 * Lịch sử các lần nhập trong ngày — hiển thị sẵn, không cần bấm mở, mới nhất trên cùng (CR-01 §8.1).
 *
 * Sửa/xoá chỉ có khi ngày còn mở, và cố ý KHÔNG bắt buộc nhập lý do: đây là sửa nháp trước khi chốt
 * sổ, và màn hình cần nhanh. Mọi thay đổi vẫn được ghi vào production_entry_logs (CR-01 §12.1).
 */
export function EntryHistoryTable({
  day,
  readOnly = false,
}: {
  day: ProductionDayDetailDto
  readOnly?: boolean
}) {
  const { showToast } = useToast()
  const updateEntry = useUpdateProductionEntry(day.orderId, day.productionDate)
  const deleteEntry = useDeleteProductionEntry(day.orderId, day.productionDate)

  const [editing, setEditing] = useState<ProductionEntryDto | null>(null)
  const [deleting, setDeleting] = useState<ProductionEntryDto | null>(null)
  const [draft, setDraft] = useState({ quantity: '', note: '' })

  const editable = !readOnly && day.dayStatus !== 'Closed' && !day.isOrderReadOnly

  const openEdit = (entry: ProductionEntryDto) => {
    setDraft({ quantity: String(entry.quantity), note: entry.note ?? '' })
    updateEntry.reset()
    setEditing(entry)
  }

  const parsed = /^\d+$/.test(draft.quantity.trim()) ? Number(draft.quantity.trim()) : null

  // Trần khi sửa = số còn được nhập + chính số lượng cũ của dòng này, đúng công thức thay thế mà
  // server dùng: NewDayActual = DayActual − OldQuantity + NewQuantity (CR-01 §6.5).
  const maximum = editing === null ? 0 : day.remainingAllowance + editing.quantity
  const editError =
    parsed === null || parsed === 0
      ? 'Số lượng phải là số nguyên lớn hơn 0.'
      : parsed > maximum
        ? `Vượt quá số cho phép (${formatNumber(maximum)} đôi).`
        : null

  const saveEdit = async () => {
    if (editing === null || parsed === null || editError !== null) return

    await updateEntry.mutateAsync({
      entryId: editing.id,
      request: { quantity: parsed, note: draft.note.trim() || null },
    })

    showToast(`Đã sửa lần ghi nhận thành ${formatNumber(parsed)} đôi.`)
    setEditing(null)
  }

  const confirmDelete = async () => {
    if (deleting === null) return

    await deleteEntry.mutateAsync(deleting.id)
    showToast(`Đã xoá lần ghi nhận ${formatNumber(deleting.quantity)} đôi.`)
    setDeleting(null)
  }

  if (day.entries.length === 0) {
    return (
      <EmptyState
        icon="📝"
        title="Chưa ghi nhận lần nào trong ngày"
        description={
          day.dayStatus === 'Closed'
            ? 'Ngày này được xuất hàng với sản lượng 0.'
            : 'Nhập số lượng ở khung phía trên để ghi nhận lần đầu tiên.'
        }
      />
    )
  }

  return (
    <>
      <div className="table-wrapper entry-history">
        <table className="table">
          <thead>
            <tr>
              <th>Thời điểm</th>
              <th className="num">Số lượng</th>
              <th className="num">Lũy kế</th>
              {editable && <th aria-label="Thao tác" />}
            </tr>
          </thead>
          <tbody>
            {day.entries.map((entry) => (
              <tr key={entry.id}>
                <td>
                  <span className="table__strong">{formatTimestamp(entry.recordedAt)}</span>
                  {/* Người ghi nhận và ghi chú xếp dưới mốc thời gian, để bảng giữ đúng ba cột số
                      mà mắt cần quét: thời điểm, số lượng, lũy kế. */}
                  {entry.recordedBy && <span className="table__sub">{entry.recordedBy}</span>}
                  {entry.note && <span className="table__sub entry-history__note">{entry.note}</span>}
                </td>
                <td className="num table__strong">
                  +{formatNumber(entry.quantity)}
                  {entry.isEdited && (
                    <>
                      {' '}
                      <Badge tone="neutral">Đã sửa</Badge>
                    </>
                  )}
                </td>
                <td className="num">{formatNumber(entry.runningTotal)}</td>
                {editable && (
                  <td className="entry-history__actions">
                    {/* Lớp bọc mới là flex container. Đặt flex thẳng lên <td> sẽ tước mất
                        display: table-cell của nó, làm ô rơi khỏi lưới bảng: đường kẻ ngang đứt
                        đoạn và cụm nút lệch khỏi hàng. */}
                    <div className="entry-history__buttons">
                      {/* Nút biểu tượng theo mock: hàng thao tác lặp lại nhiều lần nên chữ sẽ làm
                          bảng ồn, và màu đã đủ phân biệt sửa với xoá. */}
                      <button
                        type="button"
                        className="icon-btn icon-btn--edit"
                        aria-label={`Sửa lần ghi nhận ${formatNumber(entry.quantity)} đôi`}
                        title="Sửa"
                        onClick={() => openEdit(entry)}
                      >
                        ✎
                      </button>
                      <button
                        type="button"
                        className="icon-btn icon-btn--delete"
                        aria-label={`Xoá lần ghi nhận ${formatNumber(entry.quantity)} đôi`}
                        title="Xoá"
                        onClick={() => {
                          deleteEntry.reset()
                          setDeleting(entry)
                        }}
                      >
                        🗑
                      </button>
                    </div>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal
        open={editing !== null}
        title="Sửa lần ghi nhận"
        description={editing ? formatTimestamp(editing.recordedAt) : undefined}
        onClose={() => setEditing(null)}
        footer={
          <>
            <Button variant="primary" onClick={() => setEditing(null)} disabled={updateEntry.isPending}>
              Huỷ
            </Button>
            <Button
              variant="primary"
              disabled={editError !== null}
              loading={updateEntry.isPending}
              onClick={saveEdit}
            >
              Lưu
            </Button>
          </>
        }
      >
        <p className="muted">
          Ngày còn mở nên sửa được tự do. Thay đổi vẫn được ghi lại đầy đủ trong lịch sử hệ thống.
        </p>

        <div className="field">
          <label className="field__label" htmlFor="editEntryQuantity">
            Số lượng (đôi) <span className="field__required">*</span>
          </label>
          <input
            id="editEntryQuantity"
            className="input input--number"
            inputMode="numeric"
            autoFocus
            value={draft.quantity}
            onChange={(event) => {
              const next = event.target.value
              if (next !== '' && !/^\d+$/.test(next)) return
              setDraft((current) => ({ ...current, quantity: next }))
            }}
          />
          {editError && (
            <p className="field__error" role="alert">
              {editError}
            </p>
          )}
        </div>

        <div className="field">
          <label className="field__label" htmlFor="editEntryNote">
            Ghi chú (tuỳ chọn)
          </label>
          <input
            id="editEntryNote"
            className="input"
            maxLength={255}
            value={draft.note}
            onChange={(event) => setDraft((current) => ({ ...current, note: event.target.value }))}
          />
        </div>

        {updateEntry.isError && <InlineError message={toUserMessage(updateEntry.error)} />}
      </Modal>

      <Modal
        open={deleting !== null}
        title="Xoá lần ghi nhận?"
        onClose={() => setDeleting(null)}
        footer={
          <>
            <Button variant="primary" onClick={() => setDeleting(null)} disabled={deleteEntry.isPending}>
              Quay lại
            </Button>
            <Button variant="primary" loading={deleteEntry.isPending} onClick={confirmDelete}>
              Xoá
            </Button>
          </>
        }
      >
        <p>
          Xoá lần ghi nhận <strong>{formatNumber(deleting?.quantity ?? 0)} đôi</strong> lúc{' '}
          {deleting ? formatTimestamp(deleting.recordedAt) : ''}?
        </p>
        <p className="muted">
          Sản lượng của ngày sẽ giảm tương ứng. Lần ghi nhận này vẫn được lưu trong lịch sử hệ thống.
        </p>

        {deleteEntry.isError && <InlineError message={toUserMessage(deleteEntry.error)} />}
      </Modal>
    </>
  )
}
