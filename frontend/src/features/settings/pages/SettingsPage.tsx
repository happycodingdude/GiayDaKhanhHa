import { useEffect, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button, Card, Field } from '../../../shared/components/ui'
import { ErrorState, InlineError, LoadingState } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { useSettings, useUpdateSettings } from '../hooks/useSettings'

const MIN_INTERVAL = 5
const MAX_INTERVAL = 480

/**
 * MH7 — Cấu hình. Đúng hai thiết lập: chu kỳ ghi nhận, và có nhắc trước khi tới hạn hay không.
 * Cả hai chỉ ảnh hưởng tới lời nhắc trên màn hình nhập sản lượng — không thiết lập nào chặn thao
 * tác hay thay đổi dữ liệu đã ghi (CR-01 §6.8, N-10).
 */
export function SettingsPage() {
  const { showToast } = useToast()
  const query = useSettings()
  const update = useUpdateSettings()

  const [interval, setInterval] = useState('')
  const [remindBeforeDue, setRemindBeforeDue] = useState(true)

  // Form được nạp lại từ server mỗi khi dữ liệu cấu hình đổi.
  useEffect(() => {
    if (!query.data) return
    setInterval(String(query.data.recordingIntervalMinutes))
    setRemindBeforeDue(query.data.remindBeforeDue)
  }, [query.data])

  if (query.isPending) {
    return (
      <div className="page">
        <LoadingState />
      </div>
    )
  }

  if (query.isError) {
    return (
      <div className="page">
        <ErrorState
          error={query.error}
          onRetry={() => void query.refetch()}
          title="Không tải được cấu hình"
        />
      </div>
    )
  }

  const parsed = /^\d+$/.test(interval.trim()) ? Number(interval.trim()) : null

  const intervalError =
    parsed === null || parsed < MIN_INTERVAL || parsed > MAX_INTERVAL
      ? `Chu kỳ phải từ ${MIN_INTERVAL} đến ${MAX_INTERVAL} phút.`
      : null

  const canSave = intervalError === null && !update.isPending

  const save = async () => {
    if (!canSave || parsed === null) return

    await update.mutateAsync({ recordingIntervalMinutes: parsed, remindBeforeDue })
    showToast('Đã lưu cấu hình.')
  }

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <h1 className="page__title">Cấu hình</h1>
          <p className="page__subtitle">Thiết lập nhắc ghi nhận sản lượng</p>
        </div>
      </header>

      <Card title="Ghi nhận sản lượng">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            void save()
          }}
        >
          <Field
            label="Chu kỳ ghi nhận (phút)"
            htmlFor="recordingInterval"
            required
            error={interval !== '' && intervalError ? intervalError : undefined}
            hint="Bao lâu thì quản lý ghi nhận sản lượng một lần. Ví dụ: 60 phút."
          >
            <input
              id="recordingInterval"
              className="input input--number"
              inputMode="numeric"
              value={interval}
              onChange={(event) => {
                const next = event.target.value
                if (next !== '' && !/^\d+$/.test(next)) return
                setInterval(next)
              }}
            />
          </Field>

          <label className="confirm-check confirm-check--plain">
            <input
              type="checkbox"
              checked={remindBeforeDue}
              onChange={(event) => setRemindBeforeDue(event.target.checked)}
            />
            <span>
              <strong>Nhắc trước khi tới hạn</strong>
              <span className="option__hint">
                Bật thì màn hình nhập sản lượng nhắc sớm vài phút trước khi hết chu kỳ. Tắt thì chỉ
                nhắc sau khi đã quá hạn.
              </span>
            </span>
          </label>

          {/* Cấu hình không hồi tố dữ liệu đã ghi, và server không dùng nó để từ chối request nào. */}
          <p className="muted">
            Lời nhắc không giới hạn số lần ghi nhận trong ngày và không thay đổi dữ liệu đã ghi
            trước đó.
          </p>

          <Button type="submit" variant="primary" disabled={!canSave} loading={update.isPending}>
            Lưu cấu hình
          </Button>

          {update.isError && <InlineError message={toUserMessage(update.error)} />}
        </form>
      </Card>
    </div>
  )
}
