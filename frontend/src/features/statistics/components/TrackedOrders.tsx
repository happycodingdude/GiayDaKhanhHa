import { useEffect, useRef, useState } from 'react'
import { Badge, Button, Card, ProgressBar } from '../../../shared/components/ui'
import { ScheduleStatusBadge } from '../../../shared/components/StatusBadges'
import { EmptyState } from '../../../shared/feedback/QueryState'
import {
  addMonths,
  dayOfMonth,
  daysOfMonth,
  formatDate,
  formatMonth,
  isSunday,
  monthOf,
} from '../../../shared/lib/date'
import type { IsoDate } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatPercent } from '../../../shared/lib/format'
import type { DashboardOrderDayDto, DashboardOrderDto } from '../types'

/**
 * Tình trạng sản xuất của một ngày, đọc bằng đúng một chấm trên timeline.
 *
 *   met       đã Xuất hàng và đạt kế hoạch
 *   short     đã Xuất hàng nhưng thiếu
 *   progress  đang sản xuất và đã ghi nhận được sản lượng — số tạm tính, chưa chốt sổ
 *   missing   ngày đã qua mà vẫn chưa Xuất hàng
 *
 * Ngày nghỉ, ngày chưa tới, và ngày hôm nay chưa ghi nhận gì thì không có chấm.
 */
type DayMark = 'met' | 'short' | 'progress' | 'missing'

/** Ô nằm cao hơn mốc này thì tooltip không còn chỗ ở phía trên và phải lật xuống dưới. */
const TOOLTIP_FLIP_THRESHOLD = 90

function dayMark(day: DashboardOrderDayDto, isPast: boolean): DayMark | null {
  if (day.dayStatus === 'NoPlan' || day.dayStatus === 'NotStarted') return null

  if (day.dayStatus === 'Closed') {
    return (day.actualQuantity ?? 0) >= day.plannedQuantity ? 'met' : 'short'
  }

  // Ngày đã trôi qua mà chưa chốt sổ là việc bị treo, cần thấy trước mọi thứ khác.
  if (isPast) return 'missing'

  // Ngày đang sản xuất mà đã có sản lượng phải hiện lên: chỉ chấm điểm ngày đã Xuất hàng thì
  // công của cả ngày hôm nay biến mất khỏi timeline (CR-01 OV-5 nói về phần thiếu, không phải
  // về việc giấu sản lượng).
  return (day.actualQuantity ?? 0) > 0 ? 'progress' : null
}

function dayTitle(day: DashboardOrderDayDto, mark: DayMark): string {
  const head = `${formatDate(day.productionDate)} · KH ${formatNumber(day.plannedQuantity)}`

  if (mark === 'missing') {
    return day.actualQuantity === null || day.actualQuantity === 0
      ? `${head} · chưa xuất hàng, chưa ghi nhận gì`
      : `${head} · chưa xuất hàng, tạm tính ${formatNumber(day.actualQuantity)}`
  }

  if (mark === 'progress') {
    return `${head} · đang sản xuất, tạm tính ${formatNumber(day.actualQuantity ?? 0)}`
  }

  const difference = (day.actualQuantity ?? 0) - day.plannedQuantity
  return `${head} · TT ${formatNumber(day.actualQuantity ?? 0)} (${formatDifference(difference)})`
}

export function TrackedOrders({
  orders,
  today,
  onOpenOrder,
}: {
  orders: DashboardOrderDto[]
  /** Ngày nghiệp vụ do backend chốt, không phải ngày của trình duyệt. */
  today: IsoDate
  onOpenOrder: (orderId: string) => void
}) {
  const [view, setView] = useState<'timeline' | 'list'>('timeline')
  const [month, setMonth] = useState(() => monthOf(today))
  // Nút "Hôm nay" bấm được ở mọi tháng, nên khi đang đứng sẵn ở tháng này thì setMonth không đổi
  // gì cả. Đếm số lần bấm để timeline vẫn nhận được tín hiệu và căn lại về cột hôm nay.
  const [focusToday, setFocusToday] = useState(0)

  if (orders.length === 0) {
    return (
      <Card title="Đơn hàng đang theo dõi">
        <EmptyState icon="✓" title="Tất cả đơn hàng đã hoàn thành" />
      </Card>
    )
  }

  return (
    <Card
      title="Đơn hàng đang theo dõi"
      actions={
        <Button onClick={() => setView(view === 'timeline' ? 'list' : 'timeline')}>
          {view === 'timeline' ? '☰ Xem danh sách' : '▦ Xem tiến độ'}
        </Button>
      }
    >
      {view === 'timeline' ? (
        <>
          <div className="gantt-toolbar">
            <div className="gantt-toolbar__month">
              <span className="gantt-toolbar__month-label">📅 {formatMonth(month)}</span>
              {/* Hai nút điều hướng tháng giữ style trung tính: chúng chỉ đổi khung nhìn, không
                  phải thao tác nghiệp vụ như các nút hành động khác. */}
              <Button variant="secondary" aria-label="Tháng trước" onClick={() => setMonth(addMonths(month, -1))}>
                ‹
              </Button>
              <Button variant="secondary" aria-label="Tháng sau" onClick={() => setMonth(addMonths(month, 1))}>
                ›
              </Button>
            </div>
            <Button
              onClick={() => {
                setMonth(monthOf(today))
                setFocusToday((count) => count + 1)
              }}
            >
              Hôm nay
            </Button>

            <ul className="gantt-legend">
              <li>
                <span className="gantt__dot gantt__dot--met" /> Đạt kế hoạch
              </li>
              <li>
                <span className="gantt__dot gantt__dot--short" /> Thiếu
              </li>
              <li>
                <span className="gantt__dot gantt__dot--progress" /> Đang sản xuất
              </li>
              <li>
                <span className="gantt__dot gantt__dot--missing">!</span> Chưa nhập
              </li>
            </ul>
          </div>

          <TrackedOrdersTimeline
            orders={orders}
            today={today}
            month={month}
            focusToday={focusToday}
            onOpenOrder={onOpenOrder}
          />
        </>
      ) : (
        <TrackedOrdersList orders={orders} onOpenOrder={onOpenOrder} />
      )}
    </Card>
  )
}

function TrackedOrdersTimeline({
  orders,
  today,
  month,
  focusToday,
  onOpenOrder,
}: {
  orders: DashboardOrderDto[]
  today: IsoDate
  month: string
  /** Tăng lên mỗi lần bấm "Hôm nay" — chỉ dùng làm tín hiệu căn lại timeline. */
  focusToday: number
  onOpenOrder: (orderId: string) => void
}) {
  const days = daysOfMonth(month)
  const scrollRef = useRef<HTMLDivElement>(null)

  /**
   * Tooltip tự vẽ thay cho thuộc tính `title` gốc: trình duyệt luôn trễ khoảng một giây trước khi
   * hiện title, và cỡ chữ của nó không chỉnh được. Toạ độ là `position: fixed` vì bảng nằm trong
   * một khung cuộn ngang — tooltip định vị tuyệt đối bên trong sẽ bị khung đó cắt mất.
   */
  const [tooltip, setTooltip] = useState<
    { text: string; x: number; y: number; below: boolean } | null
  >(null)

  // Tháng có tới 31 cột nên gần như luôn phải cuộn ngang; đưa cột hôm nay vào giữa tầm nhìn để
  // không phải tự đi tìm nó sau mỗi lần đổi tháng hay bấm nút "Hôm nay".
  useEffect(() => {
    const scroller = scrollRef.current
    if (!scroller) return

    const centerOnToday = () => {
      const todayColumn = scroller.querySelector<HTMLElement>('[data-today="true"]')
      scroller.scrollLeft = todayColumn
        ? Math.max(0, todayColumn.offsetLeft - scroller.clientWidth / 2)
        : 0
    }

    // Đổi bề rộng cửa sổ làm trình duyệt đặt lại vị trí cuộn, kéo timeline về ngày 1 của tháng.
    // Theo dõi kích thước để căn lại, thay vì để cột hôm nay biến mất khỏi tầm nhìn.
    const observer = new ResizeObserver(centerOnToday)
    observer.observe(scroller)
    return () => observer.disconnect()
  }, [month, focusToday])

  return (
    <>
      <div
        className="table-wrapper gantt-scroll"
        ref={scrollRef}
        // Tooltip dùng toạ độ tuyệt đối của màn hình; cuộn ngang làm nó lệch khỏi ô nên tắt luôn.
        onScroll={() => setTooltip(null)}
      >
        <table className="table gantt">
          <thead>
            <tr>
              <th className="gantt__code">Mã đơn</th>
              <th className="gantt__progress">Tiến độ</th>
              {days.map((date) => {
                const isToday = date === today
                return (
                  <th
                    key={date}
                    data-today={isToday}
                    className={`gantt__day ${isToday ? 'gantt__day--today' : ''} ${
                      isSunday(date) ? 'gantt__day--sunday' : ''
                    }`}
                  >
                    {isToday && <span className="gantt__today-label">Hôm nay</span>}
                    {/* Số ngày luôn nằm trong cùng một khối, kể cả ngày thường: chỉ ngày hôm nay
                        mới có khối thì hàng tiêu đề cao thêm đúng ở tháng chứa hôm nay, và cả
                        bảng nhảy chỗ mỗi lần đổi qua lại giữa các tháng. */}
                    <span className={`gantt__day-number ${isToday ? 'gantt__today-badge' : ''}`}>
                      {dayOfMonth(date)}
                    </span>
                  </th>
                )
              })}
              {/* Cột đệm nuốt toàn bộ phần dư khi bảng hẹp hơn khung. Không có nó, thuật toán
                  table-layout tự động chia phần dư theo bề rộng nội dung, khiến cột ngày một
                  chữ số hẹp hơn cột hai chữ số và cả lưới ngày lệch nhau. */}
              <th className="gantt__spacer" aria-hidden="true" />
              <th className="gantt__status">Tình trạng</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order) => {
              const daysByDate = new Map(order.days.map((day) => [day.productionDate, day]))

              return (
                <tr
                  key={order.orderId}
                  className="table__row--clickable"
                  onClick={() => onOpenOrder(order.orderId)}
                  tabIndex={0}
                  onKeyDown={(event) => event.key === 'Enter' && onOpenOrder(order.orderId)}
                >
                  <td className="gantt__code table__strong">
                    {/* Bề rộng cố định ở lớp trong: cột này sticky và cột "Tiến độ" neo theo đúng
                        bề rộng của nó, nên một mã đơn dài bất thường không được phép nong cột ra. */}
                    <span className="gantt__code-text" title={order.orderCode}>
                      {order.orderCode}
                    </span>
                  </td>
                  <td className="gantt__progress">{formatPercent(order.progressPercentage)}</td>

                  {days.map((date, index) => {
                    const day = daysByDate.get(date)
                    const mark = day ? dayMark(day, date < today) : null

                    return (
                      <td
                        key={date}
                        className={`gantt__day ${date === today ? 'gantt__day--today' : ''}`}
                      >
                        {/* Thanh nền chạy suốt cả tháng để mọi hàng có cùng một đường cơ sở, thay
                            vì chỉ vẽ trong khoảng ngày của đơn — các chấm mới là thứ nói về đơn. */}
                        <span
                          className={`gantt__track ${index === 0 ? 'gantt__track--start' : ''} ${
                            index === days.length - 1 ? 'gantt__track--end' : ''
                          }`}
                        />
                        {day && mark && (
                          // Tooltip chỉ bám vào đúng chấm tròn, không phải cả ô ngày: hover vào
                          // khoảng trống của một ngày không có gì để nói thì không hiện gì cả.
                          <span
                            className={`gantt__dot gantt__dot--${mark}`}
                            onMouseEnter={(event) => {
                              const rect = event.currentTarget.getBoundingClientRect()
                              // Chấm sát mép trên màn hình thì tooltip vẽ phía trên sẽ bị tràn ra
                              // ngoài, nên nó lật xuống dưới.
                              const below = rect.top < TOOLTIP_FLIP_THRESHOLD
                              setTooltip({
                                text: dayTitle(day, mark),
                                x: rect.left + rect.width / 2,
                                y: below ? rect.bottom : rect.top,
                                below,
                              })
                            }}
                            onMouseLeave={() => setTooltip(null)}
                          >
                            {mark === 'missing' ? '!' : ''}
                          </span>
                        )}
                      </td>
                    )
                  })}

                  <td className="gantt__spacer" />
                  <td className="gantt__status">
                    <ScheduleStatusBadge
                      scheduleStatus={order.scheduleStatus}
                      behindQuantity={order.behindQuantity}
                    />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      <p className="gantt-hint">← Kéo ngang để xem thêm →</p>

      {tooltip && (
        <div
          className={`gantt-tooltip ${tooltip.below ? 'gantt-tooltip--below' : ''}`}
          style={{ left: tooltip.x, top: tooltip.y }}
          role="tooltip"
        >
          {tooltip.text}
        </div>
      )}
    </>
  )
}

/** Đúng bảng đang dùng trước khi có timeline: hai cột số mà timeline không diễn đạt được. */
function TrackedOrdersList({
  orders,
  onOpenOrder,
}: {
  orders: DashboardOrderDto[]
  onOpenOrder: (orderId: string) => void
}) {
  return (
    <div className="table-wrapper">
      <table className="table">
        <thead>
          <tr>
            <th>Mã đơn</th>
            <th>Tiến độ</th>
            <th className="num">Hôm nay</th>
            <th className="num">Còn lại</th>
            <th>Tình trạng</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <tr
              key={order.orderId}
              className="table__row--clickable"
              onClick={() => onOpenOrder(order.orderId)}
              tabIndex={0}
              onKeyDown={(event) => event.key === 'Enter' && onOpenOrder(order.orderId)}
            >
              <td className="table__strong">{order.orderCode}</td>
              <td className="table__progress">
                <span>{formatPercent(order.progressPercentage)}</span>
                <ProgressBar
                  value={order.progressPercentage}
                  tone={order.scheduleStatus === 'Behind' ? 'danger' : 'info'}
                />
              </td>
              {/* Ngày đã Xuất hàng hiện chênh lệch; ngày còn mở hiện sản lượng tạm tính, vì
                  chênh lệch của một ngày chưa chốt sổ là con số chưa tồn tại (CR-01 N-07). */}
              <td className={`num ${(order.todayDifference ?? 0) < 0 ? 'danger' : ''}`}>
                {!order.todayHasPlan ? (
                  <Badge tone="neutral">Không SX</Badge>
                ) : order.todayStatus === 'Closed' ? (
                  formatDifference(order.todayDifference)
                ) : (
                  <>
                    {formatNumber(order.todayActualQuantity)} / {formatNumber(order.todayPlannedQuantity)}
                    <span className="table__sub">Tạm tính</span>
                  </>
                )}
              </td>
              <td className="num">{formatNumber(order.remaining)}</td>
              <td>
                <ScheduleStatusBadge
                  scheduleStatus={order.scheduleStatus}
                  behindQuantity={order.behindQuantity}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
