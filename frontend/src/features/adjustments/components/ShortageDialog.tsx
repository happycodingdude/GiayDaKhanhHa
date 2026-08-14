import { useEffect, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button } from '../../../shared/components/ui'
import { Stepper, type Step as StepDefinition } from '../../../shared/components/Stepper'
import { Modal } from '../../../shared/dialogs/Modal'
import { InlineError, LoadingState } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatDate, today } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import type { ProductionDayDto } from '../../production/types'
import { useApplyAdjustment, usePreviewAdjustment } from '../hooks/useAdjustments'
import type { AdjustmentPreviewDto, AdjustmentType } from '../types'

type Step = 'method' | 'chooseDay' | 'preview' | 'confirm'

/**
 * Every step of the flow shares one width: the step indicator is a single row that must not wrap,
 * and a dialog that resized between steps would make the indicator jump around.
 */
const DIALOG_WIDTH = 720

/** Option 1 asks which day absorbs the shortage; Option 2 has the system decide, so it has one step fewer. */
function stepsFor(type: AdjustmentType): StepDefinition[] {
  return [
    { id: 'method', label: 'Phương thức' },
    ...(type === 'Manual' ? [{ id: 'chooseDay', label: 'Chọn ngày' }] : []),
    { id: 'preview', label: 'Xem trước' },
    { id: 'confirm', label: 'Xác nhận' },
  ]
}

/**
 * Shortage handling — both approved options:
 *   Option 1 (Manual)    the manager picks one day, which absorbs the whole shortage.
 *   Option 2 (Automatic) the system splits the whole shortage across every remaining day.
 *
 * Neither option lets the manager type an add-on quantity, and nothing is applied until the
 * proposal has been previewed and confirmed (Option 1 spec §3.3/§6, Option 2 spec §3.4/§6).
 */
export function ShortageDialog({
  open,
  orderId,
  sourceDay,
  allDays,
  onClose,
}: {
  open: boolean
  orderId: string
  sourceDay: ProductionDayDto | null
  allDays: ProductionDayDto[]
  onClose: () => void
}) {
  const { showToast } = useToast()
  const preview = usePreviewAdjustment()
  const apply = useApplyAdjustment(orderId)

  const [step, setStep] = useState<Step>('method')
  const [method, setMethod] = useState<AdjustmentType>('Manual')
  const [selectedPlanId, setSelectedPlanId] = useState<string | null>(null)
  const [proposal, setProposal] = useState<AdjustmentPreviewDto | null>(null)

  useEffect(() => {
    if (!open) return
    setStep('method')
    setMethod('Manual')
    setSelectedPlanId(null)
    setProposal(null)
    preview.reset()
    apply.reset()
    // Re-initialised whenever the dialog opens for a different source day.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, sourceDay?.id])

  if (!sourceDay) return null

  const currentDate = today()
  // A past day's plan is never adjusted — that would rewrite history (master summary §11).
  const eligibleDays = allDays.filter(
    (day) => day.productionDate > sourceDay.productionDate && day.productionDate >= currentDate,
  )

  const runPreview = async (type: AdjustmentType, planId: string | null) => {
    const result = await preview.mutateAsync({
      productionPlanId: sourceDay.id,
      request:
        type === 'Automatic'
          ? { adjustmentType: 'Automatic' }
          : {
              adjustmentType: 'Manual',
              // Option 1 always transfers the whole shortage to the chosen day.
              targets: [{ productionPlanId: planId!, addOnQuantity: sourceDay.shortageQuantity }],
            },
    })

    setProposal(result)
    setStep('preview')
  }

  const confirmApply = async () => {
    if (!proposal) return

    await apply.mutateAsync({
      productionPlanId: sourceDay.id,
      request: {
        adjustmentType: proposal.adjustmentType,
        shortageQuantity: proposal.shortageQuantity,
        targets: proposal.items.map((item) => ({
          productionPlanId: item.productionPlanId,
          addOnQuantity: item.addOnQuantity,
        })),
      },
    })

    showToast(
      `Đã bù ${formatNumber(proposal.shortageQuantity)} đôi cho ${proposal.items.length} ngày sản xuất.`,
    )
    onClose()
  }

  const shortageHeader = (
    <dl className="summary-list summary-list--compact">
      <div>
        <dt>Ngày thiếu</dt>
        <dd>{formatDate(sourceDay.productionDate)}</dd>
      </div>
      <div>
        <dt>Kế hoạch</dt>
        <dd>{formatNumber(sourceDay.plannedQuantity)} đôi</dd>
      </div>
      <div>
        <dt>Thực tế</dt>
        <dd>{formatNumber(sourceDay.actualQuantity ?? 0)} đôi</dd>
      </div>
      <div>
        <dt>Số lượng cần bù</dt>
        <dd className="danger strong">{formatNumber(sourceDay.shortageQuantity)} đôi</dd>
      </div>
    </dl>
  )

  const previewError = preview.isError ? toUserMessage(preview.error) : null
  const applyError = apply.isError ? toUserMessage(apply.error) : null

  // While the method is still being chosen the radio drives the flow; afterwards the proposal does.
  const steps = stepsFor(step === 'method' ? method : proposal?.adjustmentType ?? method)

  if (step === 'method') {
    return (
      <Modal
        open={open}
        title="Xử lý sản lượng thiếu"
        onClose={onClose}
        width={DIALOG_WIDTH}
        footer={
          <>
            <Button onClick={onClose}>Huỷ</Button>
            <Button
              variant="primary"
              loading={preview.isPending}
              onClick={() => {
                if (method === 'Manual') {
                  setStep('chooseDay')
                } else {
                  void runPreview('Automatic', null)
                }
              }}
            >
              Tiếp tục
            </Button>
          </>
        }
      >
        <Stepper steps={steps} current="method" compact />

        {shortageHeader}

        <p className="field__label">Phương thức xử lý</p>
        <div className="options">
          <label className={`option ${method === 'Manual' ? 'option--selected' : ''}`}>
            <input
              type="radio"
              name="adjustment-method"
              checked={method === 'Manual'}
              onChange={() => setMethod('Manual')}
            />
            <span>
              <strong>Chọn ngày để bù</strong>
              <span className="option__hint">
                Bạn chọn một ngày sản xuất, toàn bộ {formatNumber(sourceDay.shortageQuantity)} đôi thiếu
                sẽ được bù vào ngày đó.
              </span>
            </span>
          </label>

          <label className={`option ${method === 'Automatic' ? 'option--selected' : ''}`}>
            <input
              type="radio"
              name="adjustment-method"
              checked={method === 'Automatic'}
              onChange={() => setMethod('Automatic')}
            />
            <span>
              <strong>Hệ thống đề xuất chia đều</strong>
              <span className="option__hint">
                Hệ thống tự chia toàn bộ số lượng thiếu cho tất cả các ngày sản xuất còn lại. Phần dư
                được phân bổ từ ngày gần nhất trở đi.
              </span>
            </span>
          </label>
        </div>

        {eligibleDays.length === 0 && (
          <p className="notice notice--warning">
            Không còn ngày sản xuất nào có thể nhận phần bù.
          </p>
        )}

        {previewError && <InlineError message={previewError} />}
      </Modal>
    )
  }

  if (step === 'chooseDay') {
    return (
      <Modal
        open={open}
        title="Chọn ngày muốn bù"
        description={`Bù toàn bộ ${formatNumber(sourceDay.shortageQuantity)} đôi thiếu vào một ngày sản xuất.`}
        onClose={onClose}
        width={DIALOG_WIDTH}
        footer={
          <>
            <Button onClick={() => setStep('method')}>Quay lại</Button>
            <Button
              variant="primary"
              disabled={selectedPlanId === null}
              loading={preview.isPending}
              onClick={() => void runPreview('Manual', selectedPlanId)}
            >
              Xem trước
            </Button>
          </>
        }
      >
        <Stepper steps={steps} current="chooseDay" compact />

        <div className="options">
          {eligibleDays.map((day) => (
            <label
              key={day.id}
              className={`option ${selectedPlanId === day.id ? 'option--selected' : ''}`}
            >
              <input
                type="radio"
                name="target-day"
                checked={selectedPlanId === day.id}
                onChange={() => setSelectedPlanId(day.id)}
              />
              <span>
                <strong>{formatDate(day.productionDate)}</strong>
                <span className="option__hint">
                  Kế hoạch hiện tại: {formatNumber(day.plannedQuantity)} đôi
                </span>
              </span>
            </label>
          ))}
        </div>

        {previewError && <InlineError message={previewError} />}
      </Modal>
    )
  }

  if (!proposal) {
    return (
      <Modal open={open} title="Đang tính toán phương án bù…" onClose={onClose} width={DIALOG_WIDTH}>
        <LoadingState label="Đang tính toán phương án bù…" />
      </Modal>
    )
  }

  const totalPlanBefore = allDays.reduce((sum, day) => sum + day.plannedQuantity, 0)

  if (step === 'confirm') {
    return (
      <Modal
        open={open}
        title="Xác nhận bù sản lượng"
        onClose={onClose}
        width={DIALOG_WIDTH}
        footer={
          <>
            <Button onClick={() => setStep('preview')} disabled={apply.isPending}>
              Quay lại
            </Button>
            <Button variant="primary" loading={apply.isPending} onClick={confirmApply}>
              Xác nhận bù
            </Button>
          </>
        }
      >
        <Stepper steps={steps} current="confirm" compact />

        <p>
          Ngày thiếu: <strong>{formatDate(sourceDay.productionDate)}</strong>
          <br />
          Số lượng thiếu: <strong>{formatNumber(proposal.shortageQuantity)} đôi</strong>
        </p>

        <ul className="plain-list">
          {proposal.items.map((item) => (
            <li key={item.productionPlanId}>
              {formatDate(item.productionDate)}: {formatNumber(item.currentPlannedQuantity)} →{' '}
              <strong>{formatNumber(item.plannedQuantityAfter)}</strong> đôi (+
              {formatNumber(item.addOnQuantity)})
            </li>
          ))}
        </ul>

        <p className="muted">Thao tác này sẽ được ghi vào lịch sử bù sản lượng.</p>

        {applyError && <InlineError message={applyError} />}
      </Modal>
    )
  }

  return (
    <Modal
      open={open}
      title="Kế hoạch trước và sau khi bù"
      onClose={onClose}
      width={DIALOG_WIDTH}
      footer={
        <>
          <Button onClick={() => setStep(proposal.adjustmentType === 'Manual' ? 'chooseDay' : 'method')}>
            Quay lại
          </Button>
          <Button variant="primary" disabled={!proposal.valid} onClick={() => setStep('confirm')}>
            Tiếp tục
          </Button>
        </>
      }
    >
      <Stepper steps={steps} current="preview" compact />

      {shortageHeader}

      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Ngày</th>
              <th className="num">Hiện tại</th>
              <th className="num">Bù thêm</th>
              <th className="num">Sau khi bù</th>
            </tr>
          </thead>
          <tbody>
            {allDays.map((day) => {
              const item = proposal.items.find((entry) => entry.productionPlanId === day.id)
              return (
                <tr key={day.id} className={item ? 'table__row--highlight' : ''}>
                  <td>{formatDate(day.productionDate)}</td>
                  <td className="num">{formatNumber(day.plannedQuantity)}</td>
                  <td className="num">
                    {item ? <span className="addon">+{formatNumber(item.addOnQuantity)}</span> : '—'}
                  </td>
                  <td className="num table__strong">
                    {formatNumber(item ? item.plannedQuantityAfter : day.plannedQuantity)}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <dl className="summary-list summary-list--compact">
        <div>
          <dt>Tổng số lượng thiếu</dt>
          <dd>{formatNumber(proposal.shortageQuantity)} đôi</dd>
        </div>
        <div>
          <dt>Tổng bù thêm</dt>
          <dd>+{formatNumber(proposal.totalAddOnQuantity)} đôi</dd>
        </div>
        <div>
          <dt>Số ngày nhận bù</dt>
          <dd>{proposal.items.length} ngày</dd>
        </div>
        <div>
          <dt>Tổng kế hoạch</dt>
          <dd>
            {formatNumber(totalPlanBefore)} → {formatNumber(totalPlanBefore + proposal.totalAddOnQuantity)} đôi
          </dd>
        </div>
      </dl>

      {/* The order quantity itself never changes; only the plan moves (Option 1 spec §4.4/§4.5). */}
      <p className="muted">
        Tổng số lượng đơn hàng không thay đổi. Phần bù chỉ làm thay đổi kế hoạch sản xuất, không làm
        tăng số lượng phải giao.
      </p>

      {proposal.adjustmentType === 'Automatic' && (
        <p className="muted">
          Hệ thống chia đều {formatNumber(proposal.shortageQuantity)} đôi cho {proposal.items.length} ngày
          sản xuất còn lại. Phần dư được phân bổ từ ngày gần nhất trở đi.
        </p>
      )}

      {!proposal.valid && proposal.validationMessage && (
        <InlineError message={proposal.validationMessage} />
      )}
    </Modal>
  )
}
