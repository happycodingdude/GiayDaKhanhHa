import { Button, Card } from '../../../shared/components/ui'
import { ConfirmDialog } from '../../../shared/dialogs/ConfirmDialog'
import { formatNumber } from '../../../shared/lib/format'
import type { ProductionLineDto } from '../../production-lines/types'
import { splitEvenly, toQuantity } from '../lib/allocation'
import type { AllocationMode } from '../../orders/types'

/**
 * Bước 3 — phân bổ đơn hàng cho các dây chuyền, tầng 1 (CR-001 §7.6).
 *
 * Mode chỉ là công cụ nhập liệu, không lưu xuống database (BR-N16). `Tự động chia đều` khoá ô nhập
 * và tự điền theo BR-N17; `Nhập tay` mở khoá toàn bộ. Tổng phải bằng ĐÚNG số lượng đơn mới đi tiếp
 * được (BR-N08).
 */
export function LineAllocationStep({
  orderQuantity,
  lines,
  mode,
  allocations,
  onModeChange,
  onAllocationsChange,
  onBack,
  onNext,
}: {
  orderQuantity: number
  /** Đã sắp theo sortOrder rồi code — cùng thứ tự mà backend dùng để chia dư. */
  lines: ProductionLineDto[]
  mode: AllocationMode
  allocations: Record<string, string>
  onModeChange: (mode: AllocationMode) => void
  onAllocationsChange: (allocations: Record<string, string>) => void
  onBack: () => void
  onNext: () => void
}) {
  const total = lines.reduce((sum, line) => sum + toQuantity(allocations[line.id]), 0)
  const difference = total - orderQuantity
  const matches = difference === 0
  const hasEmptyLine = lines.some((line) => toQuantity(allocations[line.id]) <= 0)

  const distributeEvenly = () => {
    const shares = splitEvenly(orderQuantity, lines.length)
    onAllocationsChange(Object.fromEntries(lines.map((line, index) => [line.id, String(shares[index])])))
  }

  return (
    <Card
      title="Phân bổ sản lượng cho dây chuyền"
      description={
        <>
          Tổng đơn hàng <strong>{formatNumber(orderQuantity)} đôi</strong> phải được chia hết cho{' '}
          <strong>{lines.length} dây chuyền</strong> đã chọn.
        </>
      }
    >
      <div className="options options--inline">
        <label className={`option ${mode === 'Even' ? 'option--selected' : ''}`}>
          <input
            type="radio"
            name="allocation-mode"
            checked={mode === 'Even'}
            onChange={() => {
              onModeChange('Even')
              distributeEvenly()
            }}
          />
          <span>
            <strong>Tự động chia đều</strong>
            <span className="option__hint">
              Hệ thống chia <strong>{formatNumber(orderQuantity)} đôi</strong> cho{' '}
              <strong>{lines.length} dây chuyền</strong>, phần dư dồn vào các dây chuyền đầu.
            </span>
          </span>
        </label>

        <label className={`option ${mode === 'Manual' ? 'option--selected' : ''}`}>
          <input
            type="radio"
            name="allocation-mode"
            checked={mode === 'Manual'}
            onChange={() => onModeChange('Manual')}
          />
          <span>
            <strong>Nhập tay</strong>
            <span className="option__hint">Tự quyết số lượng của từng dây chuyền.</span>
          </span>
        </label>
      </div>

      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Dây chuyền</th>
              <th className="num">Số lượng (đôi)</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line) => {
              const value = allocations[line.id] ?? ''
              const isEmpty = toQuantity(value) <= 0

              return (
                <tr key={line.id}>
                  <td>
                    <span className="table__strong">{line.code}</span>
                    <span className="table__sub">{line.name}</span>
                  </td>
                  <td className="num">
                    <input
                      className={`input input--number ${isEmpty && mode === 'Manual' ? 'input--invalid' : ''}`}
                      inputMode="numeric"
                      value={value}
                      placeholder="0"
                      // Mode tự động: con số do hệ thống quyết, ô nhập bị khoá (§7.6 bước 3).
                      disabled={mode === 'Even'}
                      aria-label={`Số lượng cho ${line.code}`}
                      onChange={(event) => {
                        const next = event.target.value
                        if (next !== '' && !/^\d+$/.test(next)) return
                        onAllocationsChange({ ...allocations, [line.id]: next })
                      }}
                    />
                  </td>
                </tr>
              )
            })}
          </tbody>
          <tfoot>
            <tr>
              <th>Tổng</th>
              <th className="num">{formatNumber(total)}</th>
            </tr>
          </tfoot>
        </table>
      </div>

      <div className={`allocation ${matches ? 'allocation--ok' : 'allocation--warn'}`}>
        {matches ? (
          <span>
            ✓ Đã phân bổ: {formatNumber(total)} / {formatNumber(orderQuantity)}
          </span>
        ) : difference < 0 ? (
          <span>Còn thiếu {formatNumber(-difference)} đôi chưa được phân bổ.</span>
        ) : (
          <span>Vượt {formatNumber(difference)} đôi so với số lượng đơn.</span>
        )}
      </div>

      {matches && hasEmptyLine && (
        <p className="notice notice--warning">
          Có dây chuyền đang được phân bổ 0 đôi. Hãy bỏ chọn dây chuyền đó ở bước trước, hoặc phân bổ
          cho nó một số lượng lớn hơn 0.
        </p>
      )}

      <div className="form__actions">
        <Button onClick={onBack}>Quay lại</Button>
        <Button variant="primary" disabled={!matches || hasEmptyLine} onClick={onNext}>
          Tiếp tục
        </Button>
      </div>
    </Card>
  )
}

/**
 * Đổi từ `Nhập tay` sang `Tự động` sẽ ghi đè con số quản lý vừa nhập, nên phải hỏi trước (§7.6).
 * Tách ra để trang wizard chỉ việc dựng, không phải tự viết lại nội dung cảnh báo.
 */
export function OverwriteAllocationConfirm({
  open,
  onCancel,
  onConfirm,
}: {
  open: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  return (
    <ConfirmDialog
      open={open}
      title="Ghi đè phân bổ đang nhập?"
      confirmLabel="Chia đều lại"
      onCancel={onCancel}
      onConfirm={onConfirm}
    >
      <p>
        Chuyển sang <strong>Tự động chia đều</strong> sẽ thay toàn bộ con số bạn đã nhập bằng kết quả
        chia đều. Thao tác này không hoàn tác được.
      </p>
    </ConfirmDialog>
  )
}
