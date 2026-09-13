import { Link, useNavigate, useSearch } from '@tanstack/react-router'
import { useEffect, useMemo, useRef, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button, Card, Field, Input } from '../../../shared/components/ui'
import { Stepper, type Step as StepDefinition } from '../../../shared/components/Stepper'
import { EmptyState, ErrorState, InlineError, LoadingState } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { countDays, dateRange, formatDate, today, type IsoDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { OrderThumbnail } from '../../orders/components/OrderThumbnail'
import { useCreateSchedule, useOrders } from '../../orders/hooks/useOrders'
import type { AllocationMode, OrderListItemDto } from '../../orders/types'
import { useProductionLines } from '../../production-lines/hooks/useProductionLines'
import { LineAllocationStep, OverwriteAllocationConfirm } from '../components/LineAllocationStep'
import { PlanMatrixStep } from '../components/PlanMatrixStep'
import { cellKey, splitEvenly, toQuantity } from '../lib/allocation'

type Step = 'order' | 'setup' | 'lines' | 'matrix' | 'review'

const STEPS: StepDefinition[] = [
  { id: 'order', label: 'Chọn mã giày' },
  { id: 'setup', label: 'Dây chuyền & ngày' },
  { id: 'lines', label: 'Phân bổ dây chuyền' },
  { id: 'matrix', label: 'Kế hoạch theo ngày' },
  { id: 'review', label: 'Xem lại' },
]

/**
 * Bản nháp của wizard, giữ trong `sessionStorage` để reload trang không làm mất bước đang làm.
 * Chỉ sống trong tab hiện tại và bị xoá khi rời màn này bằng điều hướng trong app, nên nó phục vụ
 * reload chứ không khôi phục một lần lập tiến độ đã bỏ dở.
 */
interface ScheduleDraft {
  /** `orderId` trên URL lúc vào trang. Khác URL hiện tại thì bản nháp không thuộc lần vào này. */
  entryOrderId: string | null
  step: Step
  orderId: string | null
  selectedLineIds: string[]
  startDate: IsoDate
  dueDate: IsoDate
  mode: AllocationMode
  allocations: Record<string, string>
  matrix: Record<string, string>
}

const DRAFT_STORAGE_KEY = 'create-schedule-draft'

function readDraft(entryOrderId: string | null): ScheduleDraft | null {
  try {
    const raw = sessionStorage.getItem(DRAFT_STORAGE_KEY)
    if (!raw) return null

    const draft = JSON.parse(raw) as ScheduleDraft
    if (draft.entryOrderId !== entryOrderId || !STEPS.some((step) => step.id === draft.step)) return null
    return draft
  } catch {
    // Storage bị chặn hoặc dữ liệu hỏng: coi như không có nháp.
    return null
  }
}

function writeDraft(draft: ScheduleDraft) {
  try {
    sessionStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(draft))
  } catch {
    // Storage bị chặn hoặc hết quota: chỉ mất khả năng khôi phục khi reload, wizard vẫn chạy.
  }
}

function clearDraft() {
  try {
    sessionStorage.removeItem(DRAFT_STORAGE_KEY)
  } catch {
    // Như trên.
  }
}

/**
 * Lập tiến độ — luồng 5 bước phản ánh đúng hai tầng phân bổ (CR-001 §7.6).
 *
 * Trang chặn ngay từ đầu khi chưa có dây chuyền `Active` nào: không có dây chuyền thì không bước
 * nào phía sau đi được, và bắt quản lý phát hiện điều đó ở bước 3 là quá muộn (§7.11).
 */
export function CreateSchedulePage() {
  const navigate = useNavigate()
  const { showToast } = useToast()
  const search = useSearch({ from: '/authenticated/progress/new' })

  // Chỉ đơn Pending mới lập tiến độ được (BR-N05). pageSize lớn vì đây là dropdown chọn, không phân trang.
  const pendingQuery = useOrders({ status: 'Pending', pageSize: 200 })
  const linesQuery = useProductionLines('Active')
  const createSchedule = useCreateSchedule()

  // Chỉ đọc một lần lúc mount: bản nháp dùng để khởi tạo state, sau đó state là nguồn duy nhất.
  const [restoredDraft] = useState(() => readDraft(search.orderId ?? null))

  const [step, setStep] = useState<Step>(restoredDraft?.step ?? 'order')
  const [orderId, setOrderId] = useState<string | null>(
    restoredDraft ? restoredDraft.orderId : (search.orderId ?? null),
  )
  const [selectedLineIds, setSelectedLineIds] = useState<string[]>(restoredDraft?.selectedLineIds ?? [])
  const [startDate, setStartDate] = useState<IsoDate>(restoredDraft?.startDate ?? today())
  const [dueDate, setDueDate] = useState<IsoDate>(restoredDraft?.dueDate ?? '')
  const [setupErrors, setSetupErrors] = useState<Record<string, string>>({})
  const [mode, setMode] = useState<AllocationMode>(restoredDraft?.mode ?? 'Even')
  const [allocations, setAllocations] = useState<Record<string, string>>(restoredDraft?.allocations ?? {})
  const [matrix, setMatrix] = useState<Record<string, string>>(restoredDraft?.matrix ?? {})
  const [confirmingOverwrite, setConfirmingOverwrite] = useState(false)

  const orders = pendingQuery.data?.items ?? []
  const activeLines = linesQuery.data?.items ?? []
  const order = orders.find((candidate) => candidate.id === orderId) ?? null
  const isLoading = pendingQuery.isPending || linesQuery.isPending

  // Vào từ nút "Lập tiến độ" thì đơn đã được chọn sẵn; bỏ qua luôn bước 1 (§7.6 bước 1).
  // Khôi phục sau reload thì giữ nguyên bước đang làm, kể cả khi quản lý đã chủ động quay lại bước 1.
  useEffect(() => {
    if (restoredDraft) return
    if (search.orderId && step === 'order' && orders.some((o) => o.id === search.orderId)) {
      setStep('setup')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search.orderId, orders.length])

  // Đơn trong bản nháp có thể đã được lập tiến độ ở nơi khác trước khi reload xong; khi đó các bước
  // sau không còn đơn để hiển thị, nên đưa về bước chọn đơn. Chỉ đối chiếu một lần khi danh sách tải
  // xong — tạo tiến độ thành công cũng làm đơn biến khỏi danh sách, không được coi là nháp hỏng.
  const draftVerified = useRef(false)
  useEffect(() => {
    if (!restoredDraft?.orderId || draftVerified.current || !pendingQuery.data) return
    draftVerified.current = true

    if (!pendingQuery.data.items.some((o) => o.id === restoredDraft.orderId)) {
      setStep('order')
      setOrderId(null)
      showToast('Đơn hàng đang lập dở không còn chờ lập tiến độ. Vui lòng chọn lại đơn.', 'info')
    }
  }, [restoredDraft, pendingQuery.data, showToast])

  // Lưu nháp sau mỗi thay đổi. Lúc đang tải, state vẫn là giá trị khởi tạo (bước tự bỏ qua chọn đơn
  // chưa chạy) — ghi lúc này có thể đè mất bản nháp đúng nếu người dùng reload thêm lần nữa.
  useEffect(() => {
    if (isLoading) return
    writeDraft({
      entryOrderId: search.orderId ?? null,
      step,
      orderId,
      selectedLineIds,
      startDate,
      dueDate,
      mode,
      allocations,
      matrix,
    })
  }, [isLoading, search.orderId, step, orderId, selectedLineIds, startDate, dueDate, mode, allocations, matrix])

  // Rời màn này bằng điều hướng trong app (về danh sách, tạo xong, bấm menu…) là kết thúc lần lập
  // tiến độ nên xoá nháp. Reload không chạy cleanup của React, nên bản nháp còn nguyên cho lần mount sau.
  useEffect(() => clearDraft, [])

  const selectedLines = useMemo(
    () => activeLines.filter((line) => selectedLineIds.includes(line.id)),
    [activeLines, selectedLineIds],
  )

  const allocationNumbers = useMemo(
    () => Object.fromEntries(selectedLines.map((line) => [line.id, toQuantity(allocations[line.id])])),
    [selectedLines, allocations],
  )

  if (isLoading) {
    return (
      <div className="page">
        <LoadingState />
      </div>
    )
  }

  if (pendingQuery.isError || linesQuery.isError) {
    return (
      <div className="page">
        <ErrorState
          error={pendingQuery.error ?? linesQuery.error}
          onRetry={() => {
            void pendingQuery.refetch()
            void linesQuery.refetch()
          }}
          title="Không tải được dữ liệu để lập tiến độ"
        />
      </div>
    )
  }

  // Chặn sớm: không có dây chuyền Active thì mọi bước phía sau đều vô nghĩa (§7.11).
  if (activeLines.length === 0) {
    return (
      <div className="page">
        <header className="page__header">
          <h1 className="page__title">Lập tiến độ</h1>
        </header>
        <Card>
          <EmptyState
            icon="🏭"
            title="Chưa có dây chuyền nào đang hoạt động"
            description="Cần ít nhất một dây chuyền đang hoạt động trước khi lập tiến độ cho đơn hàng."
            action={
              <Link to="/settings/production-lines">
                <Button variant="primary">Đi tới cấu hình dây chuyền</Button>
              </Link>
            }
          />
        </Card>
      </div>
    )
  }

  if (orders.length === 0) {
    return (
      <div className="page">
        <header className="page__header">
          <h1 className="page__title">Lập tiến độ</h1>
        </header>
        <Card>
          <EmptyState
            title="Không có đơn hàng nào chờ lập tiến độ"
            description="Mọi đơn đã nhập đều đã có tiến độ. Nhập lô hàng mới để tiếp tục."
            action={
              <Link to="/goods-receipt/new">
                <Button variant="primary">+ Nhập hàng</Button>
              </Link>
            }
          />
        </Card>
      </div>
    )
  }

  const goToLines = () => {
    const next: Record<string, string> = {}
    if (selectedLineIds.length === 0) next.lines = 'Chọn ít nhất một dây chuyền.'
    if (!startDate) next.startDate = 'Vui lòng chọn ngày bắt đầu.'
    if (!dueDate) next.dueDate = 'Vui lòng chọn ngày kết thúc.'
    if (startDate && dueDate && startDate > dueDate) {
      next.dueDate = 'Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.'
    }

    setSetupErrors(next)
    if (Object.keys(next).length > 0 || !order) return

    // Mặc định là chia đều: đó là lựa chọn đúng trong đa số trường hợp, và nó cho quản lý một điểm
    // xuất phát cụ thể thay vì một bảng trống.
    const shares = splitEvenly(order.quantity, selectedLineIds.length)
    const ordered = activeLines.filter((line) => selectedLineIds.includes(line.id))

    setMode('Even')
    setAllocations(Object.fromEntries(ordered.map((line, index) => [line.id, String(shares[index])])))
    setStep('lines')
  }

  const goToMatrix = () => {
    const dates = dateRange(startDate, dueDate)
    const next: Record<string, string> = {}

    // Ma trận được khởi tạo sẵn bằng chia đều số của từng dây chuyền cho các ngày (§7.6 bước 4).
    for (const line of selectedLines) {
      const shares = splitEvenly(allocationNumbers[line.id] ?? 0, dates.length)
      dates.forEach((date, index) => {
        next[cellKey(line.id, date)] = String(shares[index])
      })
    }

    setMatrix(next)
    setStep('matrix')
  }

  const submit = async () => {
    if (!order) return

    const dates = dateRange(startDate, dueDate)

    const created = await createSchedule.mutateAsync({
      orderId: order.id,
      request: {
        startDate,
        dueDate,
        allocationMode: mode,
        lines: selectedLines.map((line) => ({
          productionLineId: line.id,
          allocatedQuantity: allocationNumbers[line.id] ?? 0,
          plans: dates.map((date) => ({
            productionDate: date,
            plannedQuantity: toQuantity(matrix[cellKey(line.id, date)]),
          })),
        })),
      },
    })

    showToast(`Đã lập tiến độ cho ${created.shoeCode}.`)
    await navigate({ to: '/progress/$orderId', params: { orderId: created.id } })
  }

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <Link to="/progress" className="back-link">
            ← Danh sách tiến độ
          </Link>
          <h1 className="page__title">Lập tiến độ</h1>
          {order && (
            <p className="page__subtitle">
              {order.shoeCode} · {formatNumber(order.quantity)} đôi
            </p>
          )}
        </div>
      </header>

      <Stepper steps={STEPS} current={step} />

      {step === 'order' && (
        <OrderPickerStep orders={orders} selectedId={orderId} onSelect={setOrderId} onNext={() => setStep('setup')} />
      )}

      {step === 'setup' && order && (
        <Card
          title="Chọn dây chuyền và khoảng thời gian"
          description={`${order.shoeCode} · ${formatNumber(order.quantity)} đôi`}
        >
          <div className="form">
            <Field label="Dây chuyền sản xuất" required error={setupErrors.lines}>
              <div className="line-picker">
                {activeLines.map((line) => {
                  const checked = selectedLineIds.includes(line.id)

                  return (
                    <label key={line.id} className={`line-option ${checked ? 'line-option--selected' : ''}`}>
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={(event) =>
                          setSelectedLineIds((current) =>
                            event.target.checked
                              ? [...current, line.id]
                              : current.filter((id) => id !== line.id),
                          )
                        }
                      />
                      <span>
                        <strong>{line.code}</strong>
                        <span className="option__hint">{line.name}</span>
                      </span>
                    </label>
                  )
                })}
              </div>
            </Field>

            <div className="form__row">
              <Field label="Ngày bắt đầu" htmlFor="startDate" required error={setupErrors.startDate}>
                <Input
                  id="startDate"
                  type="date"
                  value={startDate}
                  onChange={(event) => setStartDate(event.target.value)}
                />
              </Field>

              <Field
                label="Ngày kết thúc"
                htmlFor="dueDate"
                required
                error={setupErrors.dueDate}
                hint={
                  startDate && dueDate && startDate <= dueDate
                    ? `Số ngày sản xuất: ${countDays(startDate, dueDate)} ngày`
                    : undefined
                }
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
              <Button onClick={() => setStep('order')}>Quay lại</Button>
              <Button variant="primary" onClick={goToLines}>
                Tiếp tục
              </Button>
            </div>
          </div>
        </Card>
      )}

      {step === 'lines' && order && (
        <>
          <LineAllocationStep
            orderQuantity={order.quantity}
            lines={selectedLines}
            mode={mode}
            allocations={allocations}
            onModeChange={(next) => {
              // Chuyển sang Tự động sẽ ghi đè con số đã nhập tay — phải hỏi trước (§7.6 bước 3).
              if (next === 'Even' && mode === 'Manual') {
                setConfirmingOverwrite(true)
                return
              }
              setMode(next)
            }}
            onAllocationsChange={setAllocations}
            onBack={() => setStep('setup')}
            onNext={goToMatrix}
          />

          <OverwriteAllocationConfirm
            open={confirmingOverwrite}
            onCancel={() => setConfirmingOverwrite(false)}
            onConfirm={() => {
              const shares = splitEvenly(order.quantity, selectedLines.length)
              setAllocations(
                Object.fromEntries(selectedLines.map((line, index) => [line.id, String(shares[index])])),
              )
              setMode('Even')
              setConfirmingOverwrite(false)
            }}
          />
        </>
      )}

      {step === 'matrix' && (
        <PlanMatrixStep
          startDate={startDate}
          dueDate={dueDate}
          lines={selectedLines}
          allocations={allocationNumbers}
          matrix={matrix}
          onMatrixChange={setMatrix}
          onBack={() => setStep('lines')}
          onNext={() => setStep('review')}
        />
      )}

      {step === 'review' && order && (
        <Card title="Xác nhận lập tiến độ">
          <dl className="summary-list">
            <div>
              <dt>Mã giày</dt>
              <dd>{order.shoeCode}</dd>
            </div>
            <div>
              <dt>Tổng số lượng</dt>
              <dd>{formatNumber(order.quantity)} đôi</dd>
            </div>
            <div>
              <dt>Thời gian</dt>
              <dd>
                {formatDate(startDate)} → {formatDate(dueDate)}
              </dd>
            </div>
            <div>
              <dt>Cách phân bổ</dt>
              <dd>{mode === 'Even' ? 'Tự động chia đều' : 'Nhập tay'}</dd>
            </div>
          </dl>

          <div className="table-wrapper matrix-scroll">
            <table className="table matrix">
              <thead>
                <tr>
                  <th className="matrix__date-col">Ngày</th>
                  {selectedLines.map((line) => (
                    <th key={line.id} className="num">
                      {line.code}
                    </th>
                  ))}
                  <th className="num">Tổng</th>
                </tr>
              </thead>
              <tbody>
                {dateRange(startDate, dueDate).map((date) => (
                  <tr key={date}>
                    <td className="matrix__date-col">{formatDate(date)}</td>
                    {selectedLines.map((line) => (
                      <td key={line.id} className="num">
                        {formatNumber(toQuantity(matrix[cellKey(line.id, date)]))}
                      </td>
                    ))}
                    <td className="num table__strong">
                      {formatNumber(
                        selectedLines.reduce(
                          (sum, line) => sum + toQuantity(matrix[cellKey(line.id, date)]),
                          0,
                        ),
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <th className="matrix__date-col">Phân bổ</th>
                  {selectedLines.map((line) => (
                    <th key={line.id} className="num">
                      {formatNumber(allocationNumbers[line.id] ?? 0)} ✓
                    </th>
                  ))}
                  <th className="num">{formatNumber(order.quantity)} ✓</th>
                </tr>
              </tfoot>
            </table>
          </div>

          {createSchedule.isError && <InlineError message={toUserMessage(createSchedule.error)} />}

          <div className="form__actions">
            <Button onClick={() => setStep('matrix')} disabled={createSchedule.isPending}>
              Quay lại
            </Button>
            <Button variant="primary" loading={createSchedule.isPending} onClick={() => void submit()}>
              Tạo tiến độ
            </Button>
          </div>
        </Card>
      )}
    </div>
  )
}

/** Bước 1 — chỉ liệt kê đơn `Pending`, kèm ảnh và số lượng để chọn không nhầm (§7.6 bước 1). */
function OrderPickerStep({
  orders,
  selectedId,
  onSelect,
  onNext,
}: {
  orders: OrderListItemDto[]
  selectedId: string | null
  onSelect: (orderId: string) => void
  onNext: () => void
}) {
  const [search, setSearch] = useState('')

  const filtered = orders.filter((order) =>
    order.shoeCode.toLowerCase().includes(search.trim().toLowerCase()),
  )

  return (
    <Card title="Chọn đơn hàng cần lập tiến độ" description="Chỉ những đơn chưa có tiến độ mới xuất hiện ở đây.">
      <div className="search">
        <input
          className="input search__input"
          placeholder="🔍 Tìm mã giày…"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label="Tìm mã giày"
        />
      </div>

      {filtered.length === 0 && (
        <EmptyState icon="🔍" title="Không tìm thấy mã giày phù hợp" />
      )}

      <div className="order-picker">
        {filtered.map((order) => (
          <label
            key={order.id}
            className={`order-option ${selectedId === order.id ? 'order-option--selected' : ''}`}
          >
            <input
              type="radio"
              name="pending-order"
              checked={selectedId === order.id}
              onChange={() => onSelect(order.id)}
            />
            <OrderThumbnail imageUrl={order.imageUrl} shoeCode={order.shoeCode} size={56} />
            <span className="order-option__body">
              <strong>{order.shoeCode}</strong>
              <span className="option__hint">{formatNumber(order.quantity)} đôi</span>
            </span>
          </label>
        ))}
      </div>

      <div className="form__actions">
        <Button variant="primary" disabled={selectedId === null} onClick={onNext}>
          Tiếp tục
        </Button>
      </div>
    </Card>
  )
}
