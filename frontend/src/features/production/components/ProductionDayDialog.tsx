import { Badge, Button } from '../../../shared/components/ui'
import { Modal } from '../../../shared/dialogs/Modal'
import { ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate, formatTimestamp, formatWeekday, today } from '../../../shared/lib/date'
import type { IsoDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { useSettings } from '../../settings/hooks/useSettings'
import { DayStatusBadge } from './DayStatusBadge'
import { EntryHistoryTable } from './EntryHistoryTable'
import { EntryQuickForm } from './EntryQuickForm'
import { RemainingAllowance } from './RemainingAllowance'
import { useProductionDay } from '../hooks/useProductionDay'
import { useRecordingReminder } from '../hooks/useRecordingReminder'

/**
 * MH5 — Nhập sản lượng, dạng modal mở từ bảng tiến độ. Bố cục:
 *   1. Trạng thái ngày + mốc ghi nhận gần nhất
 *   2. Ba ô tổng quan: kế hoạch ngày · đã nhập · còn được nhập
 *   3. Hai cột: form ghi nhận nhanh | lịch sử các lần nhập trong ngày
 *
 * Xuất hàng KHÔNG nằm trong modal này: nó là một nút riêng cạnh "Nhập sản lượng" trên bảng tiến độ,
 * nên Enter trong form ghi nhận không bao giờ chạm tới được thao tác chốt sổ.
 *
 * `readOnly` dùng cho ngày đã Xuất hàng: modal chỉ hiển thị thông tin, không có form và không có
 * lối vào Xử lý thiếu.
 */
export function ProductionDayDialog({
  open,
  orderId,
  productionDate,
  readOnly = false,
  onClose,
}: {
  open: boolean
  orderId: string
  productionDate: IsoDate
  readOnly?: boolean
  onClose: () => void
}) {
  const query = useProductionDay(orderId, productionDate)
  const settingsQuery = useSettings()

  const day = query.data
  const reminder = useRecordingReminder(
    day?.lastRecordedAt ?? null,
    settingsQuery.data?.recordingIntervalMinutes,
    settingsQuery.data?.remindBeforeDue ?? false,
    open && day?.dayStatus === 'InProduction',
  )

  const isPastDay = productionDate < today()
  const isClosed = day?.dayStatus === 'Closed'
  const canRecord = !readOnly && !isClosed && day?.dayStatus === 'InProduction' && !day.isOrderReadOnly

  return (
    <Modal
      open={open}
      title={isClosed ? 'Chi tiết ngày sản xuất' : 'Nhập sản lượng'}
      description={
        day
          ? `${day.orderCode} · ${formatDate(productionDate)} · ${formatWeekday(productionDate)}`
          : undefined
      }
      onClose={onClose}
      width={1040}
      footer={
        <Button variant="primary" onClick={onClose}>
          Đóng
        </Button>
      }
    >
      {query.isPending && <LoadingState />}

      {query.isError && (
        <ErrorState
          error={query.error}
          onRetry={() => void query.refetch()}
          title="Không tải được ngày sản xuất"
        />
      )}

      {day && (
        <>
          <p className="modal__status">
            <DayStatusBadge status={day.dayStatus} isPastDay={isPastDay} />
            {day.lastRecordedAt && (
              <span className="modal__status-time">
                <span aria-hidden="true">🕐</span> Nhập gần nhất: {formatTimestamp(day.lastRecordedAt)}
              </span>
            )}
          </p>

          {day.isOrderReadOnly && (
            <p className="notice notice--danger">
              🔒 Đơn hàng đã quá hạn hoàn thành nên chỉ được xem lại.
            </p>
          )}

          {/* Ngày đã qua mà chưa Xuất hàng: vẫn thao tác đầy đủ, chỉ cảnh báo (CR-01 N-09). */}
          {canRecord && isPastDay && (
            <p className="notice notice--warning">
              ⚠ Ngày này đã qua nhưng chưa xuất hàng. Bạn vẫn có thể nhập bù — thời điểm xuất hàng
              sẽ được ghi nhận là hôm nay.
            </p>
          )}

          {day.dayStatus === 'NoPlan' && (
            <p className="notice notice--warning">
              Ngày này không có kế hoạch sản xuất nên không thể ghi nhận sản lượng.
            </p>
          )}

          {day.dayStatus === 'NotStarted' && (
            <p className="notice">Ngày này chưa tới nên chưa thể ghi nhận sản lượng.</p>
          )}

          {/* Chu kỳ ghi nhận chỉ để nhắc, không bao giờ chặn form (CR-01 N-10). */}
          {reminder.message && <p className="notice notice--warning">⏰ {reminder.message}</p>}

          <RemainingAllowance day={day} />

          {/* Hai cột khi còn ghi nhận được; ngày đã chốt sổ thì lịch sử chiếm trọn bề ngang. */}
          <div className={`day-grid ${canRecord ? '' : 'day-grid--single'}`}>
            {canRecord && (
              <section className="day-panel">
                <header className="day-panel__header">
                  <h3 className="day-panel__title">Ghi nhận thêm</h3>
                </header>
                <div className="day-panel__body">
                  <EntryQuickForm day={day} />
                </div>
              </section>
            )}

            <section className="day-panel">
              <header className="day-panel__header">
                <h3 className="day-panel__title">Các lần đã ghi nhận trong ngày</h3>
                {day.entries.length > 0 && (
                  <Badge tone="info">
                    {day.entries.length} lần · tổng {formatNumber(day.dayActualQuantity)} đôi
                  </Badge>
                )}
              </header>
              <div className="day-panel__body day-panel__body--flush">
                <EntryHistoryTable day={day} readOnly={readOnly} />
              </div>
            </section>
          </div>

          {isClosed && (
            <dl className="summary-list summary-list--compact">
              <div>
                <dt>Sản lượng chốt sổ</dt>
                <dd className="strong">{formatNumber(day.dayActualQuantity)} đôi</dd>
              </div>
              <div>
                <dt>Thiếu so với kế hoạch</dt>
                <dd className={(day.shortageQuantity ?? 0) > 0 ? 'danger' : ''}>
                  {(day.shortageQuantity ?? 0) > 0
                    ? `${formatNumber(day.shortageQuantity ?? 0)} đôi`
                    : 'Không thiếu'}
                </dd>
              </div>
              <div>
                <dt>Thời điểm xuất hàng</dt>
                <dd>{day.closedAt ? formatTimestamp(day.closedAt) : '—'}</dd>
              </div>
              {day.closedBy && (
                <div>
                  <dt>Người xuất hàng</dt>
                  <dd>{day.closedBy}</dd>
                </div>
              )}
            </dl>
          )}

          {isClosed && (
            <p className="muted">Ngày sản xuất này đã chốt sổ và không thể thay đổi.</p>
          )}
        </>
      )}
    </Modal>
  )
}
