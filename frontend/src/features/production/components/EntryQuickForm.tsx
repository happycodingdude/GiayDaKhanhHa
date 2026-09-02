import { useEffect, useRef, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button } from '../../../shared/components/ui'
import { InlineError } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatNumber } from '../../../shared/lib/format'
import { useCreateProductionEntry } from '../hooks/useProductionDay'
import type { ProductionDayDetailDto } from '../types'

/**
 * Form ghi nhận nhanh — màn hình này được dùng 8–10 lần mỗi ngày nên phải nhanh: ô số lượng
 * auto-focus, Enter là Ghi nhận, ghi chú tuỳ chọn (CR-01 §8.1).
 *
 * Enter ở đây CHỈ ghi nhận, không bao giờ trigger Xuất hàng — đó là ràng buộc UX bắt buộc, vì đóng
 * ngày là thao tác không mở lại được.
 *
 * Nút bị khoá trong lúc mutation đang chạy: đó là tuyến phòng thủ chính chống double-submit, vì
 * production_entries cố ý không có unique constraint (CR-01 §14.7).
 */
export function EntryQuickForm({ day }: { day: ProductionDayDetailDto }) {
  const { showToast } = useToast()
  const createEntry = useCreateProductionEntry(day.orderId, day.productionDate)

  const [value, setValue] = useState('')
  const [note, setNote] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  // Sau mỗi lần ghi nhận, con trỏ quay lại ô số lượng để lần nhập kế tiếp không cần chạm chuột.
  useEffect(() => {
    inputRef.current?.focus()
  }, [day.entries.length])

  const entered = value.trim()
  const parsed = /^\d+$/.test(entered) ? Number(entered) : null

  const localError =
    entered === ''
      ? null
      : parsed === null || parsed === 0
        ? 'Số lượng phải là số nguyên lớn hơn 0.'
        : parsed > day.remainingAllowance
          ? `Vượt quá số còn được nhập (${formatNumber(day.remainingAllowance)} đôi).`
          : null

  const canSubmit = parsed !== null && parsed > 0 && localError === null && !createEntry.isPending

  const submit = async () => {
    if (!canSubmit || parsed === null) return

    await createEntry.mutateAsync({ quantity: parsed, note: note.trim() || null })

    setValue('')
    setNote('')
    showToast(`Đã ghi nhận ${formatNumber(parsed)} đôi.`)
  }

  const serverError = createEntry.isError ? toUserMessage(createEntry.error) : null

  if (day.remainingAllowance === 0) {
    return (
      <p className="notice notice--warning">
        Đã nhập đủ kế hoạch của ngày. Không còn số lượng nào được ghi nhận thêm.
      </p>
    )
  }

  return (
    <form
      className="entry-form"
      onSubmit={(event) => {
        event.preventDefault()
        void submit()
      }}
    >
      <div className="entry-form__fields">
        <div className="entry-form__field">
          <label className="field__label" htmlFor="entryQuantity">
            Số lượng (đôi) <span className="field__required">*</span>
          </label>
          {/* Đơn vị dính liền ô nhập để con số không bao giờ bị đọc trần trụi. */}
          <div className={`input-group ${localError ? 'input-group--invalid' : ''}`}>
            <input
              id="entryQuantity"
              ref={inputRef}
              className="input-group__input"
              type="number"
              min={1}
              max={day.remainingAllowance}
              step={1}
              inputMode="numeric"
              autoFocus
              autoComplete="off"
              placeholder="Nhập số lượng"
              value={value}
              onChange={(event) => {
                const next = event.target.value
                if (next !== '' && !/^\d+$/.test(next)) return
                setValue(next)
              }}
            />
            <span className="input-group__suffix">đôi</span>
          </div>
        </div>

        <div className="entry-form__field">
          <label className="field__label" htmlFor="entryNote">
            Ghi chú (tuỳ chọn)
          </label>
          <input
            id="entryNote"
            className="input"
            maxLength={255}
            autoComplete="off"
            placeholder="Ví dụ: Tổ 2 vào ca, máy 3 hoạt động lại…"
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
        </div>
      </div>

      {localError ? (
        <p className="field__error" role="alert">
          {localError}
        </p>
      ) : (
        <div className="entry-form__hints">
          <span>Còn được nhập: {formatNumber(day.remainingAllowance)} đôi</span>
          <span>Nhấn Enter để ghi nhận nhanh</span>
        </div>
      )}

      {serverError && <InlineError message={serverError} />}

      <div className="entry-form__actions">
        <Button type="submit" variant="primary" disabled={!canSubmit} loading={createEntry.isPending}>
          <span aria-hidden="true">+</span> Ghi nhận
        </Button>
      </div>
    </form>
  )
}
