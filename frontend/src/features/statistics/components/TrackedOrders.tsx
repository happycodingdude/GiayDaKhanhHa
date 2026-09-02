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
 * Tình trạng sản xuất của một ngày, đọc bằng đúng một chấm trên timeline. Ngày không có gì để
 * đánh giá (ngày nghỉ, ngày chưa tới) không có chấm — timeline chỉ nói về quá khứ.
 */
type DayMark = 'met' | 'short' | 'missing'

function dayMark(day: DashboardOrderDayDto, isPast: boolean): DayMark | null {
  if (day.plannedQuantity === 0) return null

  if (day.actualQuantity === null) {
    // Sản lượng thực tế nhập vào cuối ngày, nên hôm nay chưa có số là bình thường; ngày đã trôi
    // qua mà vẫn trống thì mới là thứ quản lý cần thấy.
    return isPast ? 'missing' : null
  }

  return day.actualQuantity >= day.plannedQuantity ? 'met' : 'short'
}

function dayTitle(day: DashboardOrderDayDto, mark: DayMark): string {
  const head = `${formatDate(day.productionDate)} · KH ${formatNumber(day.plannedQuantity)}`
  if (mark === 'missing') return `${head} · chưa nhập thực tế`

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
              <Button aria-label="Tháng trước" onClick={() => setMonth(addMonths(month, -1))}>
                ‹
              </Button>
              <Button aria-label="Tháng sau" onClick={() => setMonth(addMonths(month, 1))}>
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
      <div className="table-wrapper gantt-scroll" ref={scrollRef}>
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
                          <span className={`gantt__dot gantt__dot--${mark}`} title={dayTitle(day, mark)}>
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
              <td className={`num ${(order.todayDifference ?? 0) < 0 ? 'danger' : ''}`}>
                {order.todayHasPlan ? (
                  formatDifference(order.todayDifference)
                ) : (
                  <Badge tone="neutral">Không SX</Badge>
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
