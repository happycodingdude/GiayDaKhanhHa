import { useEffect, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button, Field } from '../../../shared/components/ui'
import { Stepper, type Step } from '../../../shared/components/Stepper'
import { Modal } from '../../../shared/dialogs/Modal'
import { InlineError } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import type { AdjustmentRecalculationDto } from '../../adjustments/types'
import { useCreateActual, useUpdateActual } from '../hooks/useProduction'
import type { ProductionDayDto } from '../types'

/** Tells the manager what happened to an add-on that was based on the shortage they just changed. */
function recalculationMessage(recalculation: AdjustmentRecalculationDto | null): string {
  if (!recalculation) return ''

  switch (recalculation.outcome) {
    case 'Recalculated':
      return recalculation.adjustmentType === 'Automatic'
        ? ` Phần bù đã được chia đều lại: ${formatNumber(recalculation.shortageQuantity)} đôi cho ${recalculation.items.length} ngày sản xuất.`
        : ` Phần bù đã được cập nhật thành ${formatNumber(recalculation.shortageQuantity)} đôi.`
    case 'Removed':
      return ' Ngày này không còn thiếu nên phần bù đã được gỡ khỏi kế hoạch.'
    case 'Unhandled':
      return ` Phần bù cũ đã được gỡ, nhưng ${formatNumber(recalculation.shortageQuantity)} đôi thiếu chưa có ngày sản xuất nào nhận được.`
  }
}

const STEPS: Step[] = [
  { id: 'input', label: 'Nhập sản lượng' },
  { id: 'confirm', label: 'Xác nhận' },
]

/**
 * Records the actual for one day. The actual is a value, not an increment: there is no "+quantity"
 * interaction here (Step 5 §17). An explicit 0 is a valid value and is distinct from "not entered".
 */
export function ActualInputDialog({
  open,
  day,
  orderId,
  orderCode,
  orderQuantity,
  totalActual,
  onClose,
  onShortageRecorded,
}: {
  open: boolean
  day: ProductionDayDto | null
  orderId: number
  orderCode: string
  orderQuantity: number
  totalActual: number
  onClose: () => void
  onShortageRecorded: (day: ProductionDayDto) => void
}) {
  const { showToast } = useToast()
  const createActual = useCreateActual(orderId)
  const updateActual = useUpdateActual(orderId)

  const isEdit = day?.productionRecordId != null
  const [value, setValue] = useState('')
  const [confirming, setConfirming] = useState(false)

  useEffect(() => {
    if (!open || !day) return
    setValue(day.actualQuantity === null ? '' : String(day.actualQuantity))
    setConfirming(false)
    createActual.reset()
    updateActual.reset()
    // The dialog is re-initialised whenever it opens for a different day.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, day?.id])

  if (!day) return null

  // Everything already recorded on other days, used for the "how much is left" hints.
  const totalOtherDays = totalActual - (day.actualQuantity ?? 0)
  const maximum = Math.max(orderQuantity - totalOtherDays, 0)

  const entered = value.trim()
  const parsed = /^\d+$/.test(entered) ? Number(entered) : null

  const localError =
    entered === ''
      ? 'Vui lòng nhập sản lượng thực tế.'
      : parsed === null
        ? 'Sản lượng phải là số nguyên không âm.'
        : parsed > maximum
          ? `Số lượng hoàn thành không thể vượt quá số lượng của đơn hàng. Đơn hàng còn lại: ${formatNumber(maximum)} đôi.`
          : null

  const difference = parsed === null ? null : parsed - day.plannedQuantity
  const shortage = difference === null ? 0 : Math.max(-difference, 0)

  const mutation = isEdit ? updateActual : createActual
  const serverError = mutation.isError ? toUserMessage(mutation.error) : null

  const save = async () => {
    if (parsed === null) return

    const saved = isEdit
      ? await updateActual.mutateAsync({
          productionRecordId: day.productionRecordId!,
          request: { actualQuantity: parsed },
        })
      : await createActual.mutateAsync({ productionDate: day.productionDate, actualQuantity: parsed })

    const recalculation = saved.adjustmentRecalculation

    showToast(
      `Đã ghi nhận ${formatNumber(parsed)} đôi cho ngày ${formatDate(day.productionDate)}.` +
        recalculationMessage(recalculation),
      recalculation?.outcome === 'Unhandled' ? 'info' : 'success',
    )
    onClose()

    // Handling the shortage is only ever a suggestion — never forced (actual entry spec §7, §8).
    // A day that already had an add-on applied has just had it recalculated from the new shortage,
    // so there is nothing left for the manager to decide.
    if (shortage > 0 && !day.hasActiveAdjustment) {
      onShortageRecorded({ ...day, actualQuantity: parsed, shortageQuantity: shortage, difference })
    }
  }

  // A day planned for 0 cannot receive an actual at all, not even 0 (master summary §6).
  if (day.plannedQuantity === 0) {
    return (
      <Modal
        open={open}
        title="Không thể nhập sản lượng"
        description={`${orderCode} · ${formatDate(day.productionDate)}`}
        onClose={onClose}
        footer={<Button onClick={onClose}>Đóng</Button>}
      >
        <p className="notice notice--warning">
          Ngày này không có kế hoạch sản xuất. Không thể nhập sản lượng thực tế.
        </p>
        <p className="muted">
          Nếu muốn sản xuất vào ngày này, phải điều chỉnh kế hoạch trước, sau đó mới được nhập sản lượng.
        </p>
      </Modal>
    )
  }

  if (confirming && parsed !== null) {
    return (
      <Modal
        open={open}
        title="Xác nhận sản lượng"
        onClose={onClose}
        footer={
          <>
            <Button onClick={() => setConfirming(false)} disabled={mutation.isPending}>
              Quay lại
            </Button>
            <Button variant="primary" loading={mutation.isPending} onClick={save}>
              Xác nhận
            </Button>
          </>
        }
      >
        <Stepper steps={STEPS} current="confirm" compact />

        <dl className="summary-list">
          <div>
            <dt>Ngày</dt>
            <dd>{formatDate(day.productionDate)}</dd>
          </div>
          <div>
            <dt>Đơn hàng</dt>
            <dd>{orderCode}</dd>
          </div>
          <div>
            <dt>Sản lượng thực tế</dt>
            <dd className="strong">{formatNumber(parsed)} đôi</dd>
          </div>
          <div>
            <dt>Kế hoạch hôm nay</dt>
            <dd>{formatNumber(day.plannedQuantity)} đôi</dd>
          </div>
          {shortage > 0 && (
            <div>
              <dt>Thiếu</dt>
              <dd className="danger">{formatNumber(shortage)} đôi</dd>
            </div>
          )}
          {difference !== null && difference > 0 && (
            <div>
              <dt>Vượt kế hoạch</dt>
              <dd>{formatNumber(difference)} đôi</dd>
            </div>
          )}
        </dl>

        {serverError && <InlineError message={serverError} />}
      </Modal>
    )
  }

  return (
    <Modal
      open={open}
      title={isEdit ? 'Sửa sản lượng' : 'Nhập sản lượng'}
      description={`${orderCode} · ${formatDate(day.productionDate)}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Huỷ</Button>
          <Button variant="primary" disabled={localError !== null} onClick={() => setConfirming(true)}>
            Xác nhận lưu
          </Button>
        </>
      }
    >
      <Stepper steps={STEPS} current="input" compact />

      <dl className="summary-list summary-list--compact">
        <div>
          <dt>Kế hoạch hôm nay</dt>
          <dd>{formatNumber(day.plannedQuantity)} đôi</dd>
        </div>
        <div>
          <dt>Đã hoàn thành trước đó</dt>
          <dd>{formatNumber(totalOtherDays)} đôi</dd>
        </div>
        <div>
          <dt>Còn lại trước khi nhập</dt>
          <dd>{formatNumber(maximum)} đôi</dd>
        </div>
      </dl>

      <Field
        label="Sản lượng thực tế (đôi)"
        htmlFor="actualQuantity"
        required
        error={entered !== '' && localError ? localError : undefined}
        hint="Nhập tổng sản lượng của ngày. Có thể nhập 0 nếu ngày đó không sản xuất được đôi nào."
      >
        <input
          id="actualQuantity"
          className="input input--number"
          inputMode="numeric"
          autoFocus
          value={value}
          onChange={(event) => {
            const next = event.target.value
            if (next !== '' && !/^\d+$/.test(next)) return
            setValue(next)
          }}
        />
      </Field>

      {parsed !== null && !localError && (
        <div className={`notice ${shortage > 0 ? 'notice--warning' : 'notice--success'}`}>
          {shortage > 0
            ? `⚠ Thiếu ${formatNumber(shortage)} đôi so với kế hoạch.`
            : difference && difference > 0
              ? `✓ Vượt kế hoạch ${formatNumber(difference)} đôi.`
              : '✓ Đạt kế hoạch hôm nay.'}
        </div>
      )}

      {isEdit && day.actualQuantity !== null && parsed !== null && parsed !== day.actualQuantity && (
        <p className="muted">
          Thay đổi: {formatNumber(day.actualQuantity)} → {formatNumber(parsed)} đôi
        </p>
      )}

      {/* The add-on was calculated from the old shortage, so changing the actual rebuilds it. */}
      {day.hasActiveAdjustment && (
        <p className="notice notice--warning">
          Ngày này đang có một lần bù sản lượng. Nếu sản lượng thay đổi, phần bù sẽ được tính lại
          theo số lượng thiếu mới và được ghi vào lịch sử bù sản lượng.
        </p>
      )}

      {serverError && <InlineError message={serverError} />}
    </Modal>
  )
}
