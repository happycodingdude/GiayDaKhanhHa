import { useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Badge, Button, Card } from '../../../shared/components/ui'
import { Modal } from '../../../shared/dialogs/Modal'
import { EmptyState, ErrorState, InlineError, LoadingState } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatDate, formatTimestamp } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { usePlanAdjustments, useReverseAdjustment } from '../hooks/useAdjustments'
import type { PlanAdjustmentDto } from '../types'

/**
 * Adjustment history. Applied adjustments are immutable: an entry can only be reversed, and a
 * reversed entry stays visible (Step 4 §13, Step 5 §24).
 */
export function AdjustmentHistory({ orderId, readOnly }: { orderId: number; readOnly: boolean }) {
  const { showToast } = useToast()
  const query = usePlanAdjustments(orderId)
  const reverse = useReverseAdjustment(orderId)
  const [pendingReverse, setPendingReverse] = useState<PlanAdjustmentDto | null>(null)

  const confirmReverse = async () => {
    if (!pendingReverse) return
    await reverse.mutateAsync(pendingReverse.id)
    showToast('Đã hoàn tác lần bù sản lượng.')
    setPendingReverse(null)
  }

  return (
    <Card title="Lịch sử bù sản lượng">
      {query.isPending && <LoadingState />}
      {query.isError && (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} title="Không tải được lịch sử" />
      )}

      {query.data && query.data.length === 0 && (
        <EmptyState icon="🗂" title="Chưa có lần bù sản lượng nào" />
      )}

      {query.data && query.data.length > 0 && (
        <ul className="history">
          {query.data.map((adjustment) => (
            <li
              key={adjustment.id}
              className={`history__item ${adjustment.status === 'Reversed' ? 'history__item--reversed' : ''}`}
            >
              <div className="history__head">
                <span className="history__title">
                  Bù sản lượng thiếu {formatNumber(adjustment.shortageQuantity)} đôi
                </span>
                {adjustment.status === 'Applied' ? (
                  <Badge tone="success">Đang áp dụng</Badge>
                ) : (
                  <Badge tone="neutral">Đã hoàn tác</Badge>
                )}
                <Badge tone="info">
                  {adjustment.adjustmentType === 'Manual' ? 'Chọn ngày để bù' : 'Hệ thống chia đều'}
                </Badge>
              </div>

              <p className="history__meta">
                Ngày thiếu: {formatDate(adjustment.sourceProductionDate)} · 👤 {adjustment.createdBy} ·{' '}
                {formatTimestamp(adjustment.appliedAt ?? adjustment.createdAt)}
              </p>

              <ul className="plain-list">
                {adjustment.items.map((item) => (
                  <li key={item.productionPlanId}>
                    {formatDate(item.productionDate)}: +{formatNumber(item.addOnQuantity)} đôi
                  </li>
                ))}
              </ul>

              {adjustment.status === 'Reversed' && adjustment.reversedAt && (
                <p className="history__meta">
                  Hoàn tác bởi 👤 {adjustment.reversedBy} · {formatTimestamp(adjustment.reversedAt)}
                </p>
              )}

              {/* Reversing is a change to the plan, so an overdue order keeps history read-only. */}
              {adjustment.status === 'Applied' && !readOnly && (
                <div className="history__actions">
                  <Button onClick={() => setPendingReverse(adjustment)}>Hoàn tác</Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      <Modal
        open={pendingReverse !== null}
        title="Xác nhận hoàn tác"
        onClose={() => setPendingReverse(null)}
        footer={
          <>
            <Button onClick={() => setPendingReverse(null)} disabled={reverse.isPending}>
              Quay lại
            </Button>
            <Button variant="danger" loading={reverse.isPending} onClick={confirmReverse}>
              Hoàn tác
            </Button>
          </>
        }
      >
        {pendingReverse && (
          <>
            <p>
              Phần bù {formatNumber(pendingReverse.shortageQuantity)} đôi sẽ được trừ khỏi kế hoạch của
              các ngày sau:
            </p>
            <ul className="plain-list">
              {pendingReverse.items.map((item) => (
                <li key={item.productionPlanId}>
                  {formatDate(item.productionDate)}: −{formatNumber(item.addOnQuantity)} đôi
                </li>
              ))}
            </ul>
            <p className="muted">
              Lịch sử vẫn được giữ lại. Sau khi hoàn tác, bạn có thể tạo lần bù mới cho ngày thiếu này.
            </p>
          </>
        )}

        {reverse.isError && <InlineError message={toUserMessage(reverse.error)} />}
      </Modal>
    </Card>
  )
}
