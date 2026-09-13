import { Link, useNavigate, useParams } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button, Card, Field, Input } from '../../../shared/components/ui'
import { ImageUploader } from '../../../shared/components/ImageUploader'
import { ErrorState, InlineError, LoadingState } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatNumber } from '../../../shared/lib/format'
import { OrderStatusBadge } from '../components/OrderStatusBadge'
import { ProductionLineTags } from '../components/ProductionLineTags'
import { useOrder, useReceiveOrder, useUpdateOrder } from '../hooks/useOrders'

const ImageIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="4" width="18" height="16" rx="2" />
    <circle cx="8.5" cy="9.5" r="1.5" />
    <path d="m21 15-4.5-4.5L7 20" />
  </svg>
)

const DocumentIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z" />
    <path d="M14 3v5h5M9 13h6M9 17h6" />
  </svg>
)

/** Ô số lượng kèm đơn vị, dùng chung cho hai màn tạo và sửa. Chỉ nhận chữ số. */
function QuantityInput({
  id,
  value,
  onChange,
  disabled,
  placeholder,
}: {
  id: string
  value: string
  onChange: (value: string) => void
  disabled?: boolean
  placeholder?: string
}) {
  return (
    <div className="input-group receipt-form__quantity">
      <input
        id={id}
        className="input-group__input"
        inputMode="numeric"
        autoComplete="off"
        value={value}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(event) => {
          const next = event.target.value
          if (next !== '' && !/^\d+$/.test(next)) return
          onChange(next)
        }}
      />
      <span className="input-group__suffix">đôi</span>
    </div>
  )
}

/**
 * Nhập hàng — tạo mới (CR-001 §7.5). Ảnh được gửi kèm ngay trong request tạo đơn, nên không có
 * khoảnh khắc nào đơn tồn tại mà ảnh chưa kịp lên.
 */
export function CreateGoodsReceiptPage() {
  const navigate = useNavigate()
  const { showToast } = useToast()
  const receive = useReceiveOrder()

  const [shoeCode, setShoeCode] = useState('')
  const [quantity, setQuantity] = useState('')
  const [image, setImage] = useState<File | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()

    const next: Record<string, string> = {}
    if (!shoeCode.trim()) next.shoeCode = 'Vui lòng nhập mã giày.'
    else if (shoeCode.trim().length > 50) next.shoeCode = 'Mã giày tối đa 50 ký tự.'

    // Chỉ nhận số nguyên: không thập phân, không âm, phải lớn hơn 0.
    if (!/^\d+$/.test(quantity.trim())) next.quantity = 'Số lượng phải là số nguyên dương.'
    else if (Number(quantity) <= 0) next.quantity = 'Số lượng phải lớn hơn 0.'

    setErrors(next)
    if (Object.keys(next).length > 0) return

    const order = await receive.mutateAsync({
      shoeCode: shoeCode.trim(),
      quantity: Number(quantity),
      image,
    })

    showToast(`Đã nhập hàng ${order.shoeCode}.`)
    await navigate({ to: '/goods-receipt/$orderId', params: { orderId: order.id } })
  }

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <Link to="/goods-receipt" className="back-link">
            ← Danh sách nhập hàng
          </Link>
          <h1 className="page__title">Nhập hàng</h1>
          <p className="page__subtitle">
            Ghi nhận lô hàng vừa về. Dây chuyền và thời gian sản xuất được quyết định ở bước lập
            tiến độ.
          </p>
        </div>
      </header>

      <div className="receipt-grid">
        <Card className="receipt-card" title={<>{ImageIcon} Ảnh mẫu</>}>
          <ImageUploader file={image} onChange={setImage} disabled={receive.isPending} />
        </Card>

        <Card className="receipt-card" title={<>{DocumentIcon} Thông tin nhập hàng</>}>
          <form className="form receipt-form" onSubmit={submit} noValidate>
            <Field label="Mã giày" htmlFor="shoeCode" required error={errors.shoeCode}>
              <Input
                id="shoeCode"
                value={shoeCode}
                onChange={(event) => setShoeCode(event.target.value)}
                placeholder="SH-2026-001"
                autoFocus
                maxLength={50}
              />
            </Field>

            <Field label="Số lượng (đôi)" htmlFor="quantity" required error={errors.quantity}>
              <QuantityInput id="quantity" value={quantity} onChange={setQuantity} placeholder="1000" />
            </Field>

            {receive.isError && <InlineError message={toUserMessage(receive.error)} />}

            <div className="form__actions">
              <Link to="/goods-receipt">
                <Button type="button" disabled={receive.isPending}>
                  Huỷ
                </Button>
              </Link>
              <Button type="submit" variant="primary" loading={receive.isPending}>
                Nhập hàng
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </div>
  )
}

/**
 * Nhập hàng — chi tiết/sửa (CR-001 §7.2). Mọi thay đổi, kể cả ảnh, chỉ được lưu khi bấm "Lưu thay
 * đổi" — và lưu trong một request, nên hoặc tất cả được lưu, hoặc không gì cả. Trước đó ảnh mới hay
 * việc gỡ ảnh chỉ nằm ở client.
 */
export function EditGoodsReceiptPage() {
  const { orderId } = useParams({ from: '/authenticated/goods-receipt/$orderId' })
  const { showToast } = useToast()

  const query = useOrder(orderId)
  const update = useUpdateOrder()

  const [shoeCode, setShoeCode] = useState('')
  const [quantity, setQuantity] = useState('')
  const [newImage, setNewImage] = useState<File | null>(null)
  const [removeImage, setRemoveImage] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const order = query.data

  // Form được nạp lại mỗi khi server trả về một phiên bản đơn hàng khác.
  useEffect(() => {
    if (!order) return
    setShoeCode(order.shoeCode)
    setQuantity(String(order.quantity))
  }, [order?.id, order?.updatedAt])

  if (query.isPending) {
    return (
      <div className="page">
        <LoadingState />
      </div>
    )
  }

  if (query.isError || !order) {
    return (
      <div className="page">
        <ErrorState
          error={query.error}
          onRetry={() => void query.refetch()}
          title="Không tải được đơn hàng"
        />
      </div>
    )
  }

  // Đơn đã lập tiến độ thì số lượng là mốc đối chiếu của phân bổ hai tầng, không đổi được nữa.
  const quantityLocked = order.status !== 'Pending'
  const readOnly = order.isPastDueDate

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()

    const next: Record<string, string> = {}
    if (!shoeCode.trim()) next.shoeCode = 'Vui lòng nhập mã giày.'
    if (!/^\d+$/.test(quantity.trim()) || Number(quantity) <= 0) {
      next.quantity = 'Số lượng phải là số nguyên dương.'
    }

    setErrors(next)
    if (Object.keys(next).length > 0) return

    await update.mutateAsync({
      orderId,
      request: { shoeCode: shoeCode.trim(), quantity: Number(quantity), image: newImage, removeImage },
    })

    // Lưu thất bại thì mutateAsync ném trước khi tới đây: ảnh đang chọn được giữ lại để lưu lại.
    setNewImage(null)
    setRemoveImage(false)
    showToast('Đã lưu thay đổi.')
  }

  const dirty =
    shoeCode !== order.shoeCode ||
    quantity !== String(order.quantity) ||
    newImage !== null ||
    removeImage

  const discardChanges = () => {
    setShoeCode(order.shoeCode)
    setQuantity(String(order.quantity))
    setNewImage(null)
    setRemoveImage(false)
    setErrors({})
    update.reset()
  }

  // URL ảnh cố định theo đơn hàng, nên thay ảnh xong mà giữ nguyên src thì <img> vẫn hiện ảnh cũ.
  // updatedAt đổi mỗi lần thay/xoá ảnh, gắn vào query để trình duyệt tải lại đúng ảnh mới.
  const imageUrl = order.imageUrl
    ? `${order.imageUrl}?v=${encodeURIComponent(order.updatedAt)}`
    : null

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <Link to="/goods-receipt" className="back-link">
            ← Danh sách nhập hàng
          </Link>
          <h1 className="page__title">
            {order.shoeCode} <OrderStatusBadge status={order.status} />
          </h1>
          <p className="page__subtitle">
            {formatNumber(order.quantity)} đôi
            {order.productionLines.length > 0 && (
              <>
                {' · '}
                <ProductionLineTags lines={order.productionLines} showAllocation />
              </>
            )}
          </p>
        </div>

        {order.status === 'Pending' && (
          <Link to="/progress/new" search={{ orderId: order.id }}>
            <Button variant="primary">+ Lập tiến độ</Button>
          </Link>
        )}
        {order.status !== 'Pending' && (
          <Link to="/progress/$orderId" params={{ orderId: order.id }}>
            <Button variant="primary">Xem tiến độ</Button>
          </Link>
        )}
      </header>

      {readOnly && (
        <p className="notice notice--danger">
          🔒 Đơn hàng đã qua ngày kết thúc nên chỉ được xem lại.
        </p>
      )}

      <div className="receipt-grid">
        <Card className="receipt-card" title={<>{ImageIcon} Ảnh mẫu</>}>
          {/* Gỡ ảnh chỉ là đánh dấu, chưa xoá gì trên server, nên không cần hộp thoại xác nhận —
              "Huỷ thay đổi" đưa ảnh cũ trở lại. */}
          <ImageUploader
            file={newImage}
            existingUrl={removeImage ? null : imageUrl}
            disabled={update.isPending || readOnly}
            onChange={(file) => {
              setNewImage(file)
              // Chọn ảnh mới thì ảnh cũ bị thay chứ không phải bị gỡ.
              if (file) setRemoveImage(false)
            }}
            onRemoveExisting={() => setRemoveImage(true)}
          />
        </Card>

        <Card className="receipt-card" title={<>{DocumentIcon} Thông tin nhập hàng</>}>
          <form className="form receipt-form" onSubmit={submit} noValidate>
            <Field label="Mã giày" htmlFor="editShoeCode" required error={errors.shoeCode}>
              <Input
                id="editShoeCode"
                value={shoeCode}
                onChange={(event) => setShoeCode(event.target.value)}
                maxLength={50}
                disabled={readOnly}
              />
            </Field>

            <Field
              label="Số lượng (đôi)"
              htmlFor="editQuantity"
              required
              error={errors.quantity}
              hint={
                quantityLocked
                  ? 'Đơn đã lập tiến độ nên số lượng không đổi được — nó là mốc phân bổ cho các dây chuyền.'
                  : undefined
              }
            >
              <QuantityInput
                id="editQuantity"
                value={quantity}
                onChange={setQuantity}
                disabled={quantityLocked || readOnly}
              />
            </Field>

            {update.isError && <InlineError message={toUserMessage(update.error)} />}

            <div className="form__actions">
              {dirty && !readOnly && <span className="receipt-form__dirty">Có thay đổi chưa lưu</span>}
              <Button
                type="button"
                onClick={discardChanges}
                disabled={!dirty || update.isPending || readOnly}
              >
                Huỷ thay đổi
              </Button>
              <Button type="submit" variant="primary" loading={update.isPending} disabled={readOnly}>
                Lưu thay đổi
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </div>
  )
}
