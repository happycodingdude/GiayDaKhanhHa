import { Link, useNavigate } from '@tanstack/react-router'
import { useMemo, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button, Card, Field, Input } from '../../../shared/components/ui'
import { Stepper, type Step as StepDefinition } from '../../../shared/components/Stepper'
import { InlineError } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { countDays, dateRange, formatDate, formatWeekday, today, type IsoDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { useCreateOrder } from '../hooks/useOrders'

type Step = 'info' | 'plan' | 'review'

const STEPS: StepDefinition[] = [
  { id: 'info', label: 'Thông tin đơn' },
  { id: 'plan', label: 'Lập kế hoạch' },
  { id: 'review', label: 'Xem lại' },
]

interface OrderInfo {
  orderCode: string
  quantity: number
  startDate: IsoDate
  dueDate: IsoDate
}

/**
 * Một luồng liền mạch: thông tin đơn hàng → kế hoạch theo ngày → xem lại → tạo. Không bao giờ
 * tạo đơn hàng mà thiếu kế hoạch sản xuất của nó (create-order spec §1, §17).
 */
export function CreateOrderPage() {
  const navigate = useNavigate()
  const { showToast } = useToast()
  const createOrder = useCreateOrder()

  const [step, setStep] = useState<Step>('info')
  const [info, setInfo] = useState<OrderInfo | null>(null)
  const [plan, setPlan] = useState<Record<IsoDate, string>>({})

  const goToPlan = (values: OrderInfo) => {
    setInfo(values)
    // Điền sẵn mỗi ngày sản xuất một dòng; số lượng từng ngày do quản lý tự quyết.
    setPlan((current) => {
      const next: Record<IsoDate, string> = {}
      for (const date of dateRange(values.startDate, values.dueDate)) {
        next[date] = current[date] ?? ''
      }
      return next
    })
    setStep('plan')
  }

  const submit = async () => {
    if (!info) return

    const order = await createOrder.mutateAsync({
      orderCode: info.orderCode,
      quantity: info.quantity,
      startDate: info.startDate,
      dueDate: info.dueDate,
      productionPlans: dateRange(info.startDate, info.dueDate).map((date) => ({
        productionDate: date,
        plannedQuantity: Number(plan[date] || 0),
      })),
    })

    showToast(`Tạo đơn hàng ${order.orderCode} thành công.`)
    // Quản lý thường muốn xem lại đơn hàng vừa tạo (create-order spec §9).
    await navigate({ to: '/orders/$orderId', params: { orderId: String(order.id) } })
  }

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <Link to="/orders" className="back-link">
            ← Danh sách đơn hàng
          </Link>
          <h1 className="page__title">Tạo đơn hàng</h1>
        </div>
      </header>

      <Stepper steps={STEPS} current={step} />

      {step === 'info' && <OrderInfoStep initial={info} onNext={goToPlan} />}

      {step === 'plan' && info && (
        <PlanStep
          info={info}
          plan={plan}
          onChange={setPlan}
          onBack={() => setStep('info')}
          onNext={() => setStep('review')}
        />
      )}

      {step === 'review' && info && (
        <ReviewStep
          info={info}
          plan={plan}
          submitting={createOrder.isPending}
          error={createOrder.isError ? toUserMessage(createOrder.error) : null}
          onBack={() => setStep('plan')}
          onConfirm={submit}
        />
      )}
    </div>
  )
}

function OrderInfoStep({ initial, onNext }: { initial: OrderInfo | null; onNext: (values: OrderInfo) => void }) {
  const [orderCode, setOrderCode] = useState(initial?.orderCode ?? '')
  const [quantity, setQuantity] = useState(initial ? String(initial.quantity) : '')
  const [startDate, setStartDate] = useState<IsoDate>(initial?.startDate ?? today())
  const [dueDate, setDueDate] = useState<IsoDate>(initial?.dueDate ?? '')
  const [errors, setErrors] = useState<Record<string, string>>({})

  const submit = (event: React.FormEvent) => {
    event.preventDefault()

    const next: Record<string, string> = {}

    if (!orderCode.trim()) next.orderCode = 'Vui lòng nhập mã đơn hàng.'

    // Chỉ nhận số nguyên: không thập phân, không âm, phải lớn hơn 0.
    if (!/^\d+$/.test(quantity.trim())) {
      next.quantity = 'Tổng số lượng phải là số nguyên dương.'
    } else if (Number(quantity) <= 0) {
      next.quantity = 'Tổng số lượng phải lớn hơn 0.'
    }

    if (!startDate) next.startDate = 'Vui lòng chọn ngày bắt đầu.'
    if (!dueDate) next.dueDate = 'Vui lòng chọn hạn hoàn thành.'
    if (startDate && dueDate && startDate > dueDate) {
      next.dueDate = 'Hạn hoàn thành phải bằng hoặc sau ngày bắt đầu.'
    }

    setErrors(next)
    if (Object.keys(next).length > 0) return

    onNext({ orderCode: orderCode.trim(), quantity: Number(quantity), startDate, dueDate })
  }

  const productionDays = startDate && dueDate && startDate <= dueDate ? countDays(startDate, dueDate) : 0

  return (
    <Card title="Thông tin đơn hàng">
      <form className="form" onSubmit={submit} noValidate>
        <Field label="Mã đơn hàng" htmlFor="orderCode" required error={errors.orderCode}>
          <Input
            id="orderCode"
            value={orderCode}
            onChange={(event) => setOrderCode(event.target.value)}
            placeholder="ORD-001"
            autoFocus
          />
        </Field>

        <Field label="Tổng số lượng (đôi)" htmlFor="quantity" required error={errors.quantity}>
          <Input
            id="quantity"
            inputMode="numeric"
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
            placeholder="1000"
          />
        </Field>

        <div className="form__row">
          <Field label="Ngày bắt đầu" htmlFor="startDate" required error={errors.startDate}>
            <Input
              id="startDate"
              type="date"
              value={startDate}
              onChange={(event) => setStartDate(event.target.value)}
            />
          </Field>

          <Field
            label="Hạn hoàn thành"
            htmlFor="dueDate"
            required
            error={errors.dueDate}
            hint={productionDays > 0 ? `Số ngày sản xuất: ${productionDays} ngày` : undefined}
          >
            <Input
              id="dueDate"
              type="date"
              value={dueDate}
              onChange={(event) => setDueDate(event.target.value)}
            />
          </Field>
        </div>

        <div className="form__actions">
          <Button type="submit" variant="primary">
            Tiếp tục
          </Button>
        </div>
      </form>
    </Card>
  )
}

function PlanStep({
  info,
  plan,
  onChange,
  onBack,
  onNext,
}: {
  info: OrderInfo
  plan: Record<IsoDate, string>
  onChange: (plan: Record<IsoDate, string>) => void
  onBack: () => void
  onNext: () => void
}) {
  const dates = useMemo(() => dateRange(info.startDate, info.dueDate), [info.startDate, info.dueDate])
  const total = dates.reduce((sum, date) => sum + Number(plan[date] || 0), 0)
  const difference = total - info.quantity

  const [zeroLastDayConfirmed, setZeroLastDayConfirmed] = useState(false)
  const lastDate = dates[dates.length - 1]
  const lastDayIsZero = Number(plan[lastDate] || 0) === 0

  // Tổng phải khớp chính xác với số lượng đơn hàng (create-order spec §6).
  const totalMatches = difference === 0
  const canContinue = totalMatches && (!lastDayIsZero || zeroLastDayConfirmed)

  /**
   * Rải số lượng đơn hàng cho mọi ngày để quản lý không phải gõ từng ngày một.
   * Khi chia không hết, phần dư được cộng mỗi lần một đơn vị cho các ngày sớm nhất, đúng cách
   * server chia phần thiếu, nên hai bên không bao giờ lệch nhau một đơn vị.
   */
  const distributeEvenly = () => {
    if (dates.length === 0) return

    const baseShare = Math.floor(info.quantity / dates.length)
    const remainder = info.quantity % dates.length

    onChange(
      Object.fromEntries(
        dates.map((date, index) => [date, String(baseShare + (index < remainder ? 1 : 0))]),
      ),
    )
  }

  return (
    <Card
      title="Lập kế hoạch sản xuất"
      description={`${info.orderCode} · ${formatNumber(info.quantity)} đôi · ${formatDate(info.startDate)} → ${formatDate(info.dueDate)}`}
      actions={
        <Button onClick={distributeEvenly} title="Ghi đè toàn bộ kế hoạch đang nhập">
          Chia đều cho {dates.length} ngày
        </Button>
      }
    >
      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Ngày</th>
              <th>Thứ</th>
              <th className="num">Kế hoạch (đôi)</th>
            </tr>
          </thead>
          <tbody>
            {dates.map((date) => (
              <tr key={date}>
                <td className="table__strong">{formatDate(date)}</td>
                <td className="muted">{formatWeekday(date)}</td>
                <td className="num">
                  <input
                    className="input input--number"
                    inputMode="numeric"
                    value={plan[date] ?? ''}
                    placeholder="0"
                    aria-label={`Kế hoạch ngày ${formatDate(date)}`}
                    onChange={(event) => {
                      const value = event.target.value
                      if (value !== '' && !/^\d+$/.test(value)) return
                      onChange({ ...plan, [date]: value })
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <th colSpan={2}>Tổng</th>
              <th className="num">{formatNumber(total)}</th>
            </tr>
          </tfoot>
        </table>
      </div>

      <div className={`allocation ${totalMatches ? 'allocation--ok' : 'allocation--warn'}`}>
        {totalMatches ? (
          <span>✓ Đã phân bổ: {formatNumber(total)} / {formatNumber(info.quantity)}</span>
        ) : difference < 0 ? (
          <span>Bạn còn thiếu {formatNumber(-difference)} đôi chưa được phân bổ.</span>
        ) : (
          <span>Tổng kế hoạch vượt quá số lượng đơn hàng {formatNumber(difference)} đôi.</span>
        )}
      </div>

      {totalMatches && lastDayIsZero && (
        // Số 0 vào đúng ngày hạn vẫn hợp lệ về nghiệp vụ, nên đây là xác nhận chứ không phải lỗi.
        <label className="confirm-check">
          <input
            type="checkbox"
            checked={zeroLastDayConfirmed}
            onChange={(event) => setZeroLastDayConfirmed(event.target.checked)}
          />
          Ngày hoàn thành đang có kế hoạch 0 đôi. Tôi xác nhận muốn tiếp tục.
        </label>
      )}

      <div className="form__actions">
        <Button onClick={onBack}>Quay lại</Button>
        <Button variant="primary" disabled={!canContinue} onClick={onNext}>
          Xem lại
        </Button>
      </div>
    </Card>
  )
}

function ReviewStep({
  info,
  plan,
  submitting,
  error,
  onBack,
  onConfirm,
}: {
  info: OrderInfo
  plan: Record<IsoDate, string>
  submitting: boolean
  error: string | null
  onBack: () => void
  onConfirm: () => void
}) {
  const dates = dateRange(info.startDate, info.dueDate)
  const total = dates.reduce((sum, date) => sum + Number(plan[date] || 0), 0)

  return (
    <Card title="Xác nhận tạo đơn">
      <dl className="summary-list">
        <div>
          <dt>Mã đơn</dt>
          <dd>{info.orderCode}</dd>
        </div>
        <div>
          <dt>Tổng số lượng</dt>
          <dd>{formatNumber(info.quantity)} đôi</dd>
        </div>
        <div>
          <dt>Thời gian</dt>
          <dd>
            {formatDate(info.startDate)} → {formatDate(info.dueDate)}
          </dd>
        </div>
      </dl>

      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Ngày</th>
              <th className="num">Kế hoạch</th>
            </tr>
          </thead>
          <tbody>
            {dates.map((date) => (
              <tr key={date}>
                <td>{formatDate(date)}</td>
                <td className="num">{formatNumber(Number(plan[date] || 0))}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <th>Tổng</th>
              <th className="num">{formatNumber(total)} ✓</th>
            </tr>
          </tfoot>
        </table>
      </div>

      {error && <InlineError message={error} />}

      <div className="form__actions">
        <Button onClick={onBack} disabled={submitting}>
          Quay lại
        </Button>
        <Button variant="primary" loading={submitting} onClick={onConfirm}>
          Xác nhận tạo đơn
        </Button>
      </div>
    </Card>
  )
}
