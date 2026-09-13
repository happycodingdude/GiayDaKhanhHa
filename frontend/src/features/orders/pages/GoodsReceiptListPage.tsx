import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Select } from '../../../shared/components/Select'
import { Badge, Button, Card } from '../../../shared/components/ui'
import { ConfirmDialog } from '../../../shared/dialogs/ConfirmDialog'
import {
  EmptyState,
  ErrorState,
  InlineError,
  LoadingState,
} from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { OrderStatusBadge } from '../components/OrderStatusBadge'
import { OrderThumbnail } from '../components/OrderThumbnail'
import { ProductionLineTags } from '../components/ProductionLineTags'
import { useDeleteOrder, useOrders } from '../hooks/useOrders'
import type { OrderListItemDto } from '../types'

const STATUS_FILTERS = [
  { value: 'All', label: 'Tất cả' },
  { value: 'Pending', label: 'Chưa lập tiến độ' },
  { value: 'Incomplete', label: 'Đang sản xuất' },
  { value: 'Completed', label: 'Hoàn thành' },
]

const PAGE_SIZES = [10, 20, 50]

/**
 * Nhập hàng — danh sách (CR-001 §7.4). Đây là màn hình của bước "hàng về": mọi đơn đều xuất hiện ở
 * đây, kể cả đơn đã lập tiến độ, vì mã giày và ảnh mẫu vẫn thuộc về bước này.
 *
 * Dòng không bấm được: mọi điều hướng đi qua nút ở cột Thao tác — Lập tiến độ / Xem tiến độ và Sửa.
 * Đơn chưa lập tiến độ có thêm nút Xoá; đơn đã lập tiến độ thì không xoá được nên không hiện nút.
 */
export function GoodsReceiptListPage() {
  const { showToast } = useToast()
  const deleteOrder = useDeleteOrder()
  const [deleting, setDeleting] = useState<OrderListItemDto | null>(null)

  const [status, setStatus] = useState('All')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)

  const query = useOrders({ status, search, page, pageSize })

  const applySearch = (value: string) => {
    setSearch(value.trim())
    setPage(1)
  }

  const result = query.data
  const hasFilters = search !== '' || status !== 'All'
  const totalPages = Math.max(result?.totalPages ?? 1, 1)

  const confirmDelete = async () => {
    if (!deleting) return

    await deleteOrder.mutateAsync(deleting.id)

    // Xoá dòng cuối cùng của một trang thì lùi về trang trước, không để người dùng đứng ở trang trống.
    if (result && result.items.length === 1 && page > 1) setPage(page - 1)

    showToast(`Đã xoá đơn ${deleting.shoeCode}.`)
    setDeleting(null)
  }

  return (
    <div className="page page--fill">
      <header className="page__header">
        <div>
          <h1 className="page__title">Nhập hàng</h1>
          <p className="page__subtitle">Quản lý hàng nhận về theo mã giày</p>
        </div>
        <Link to="/goods-receipt/new">
          <Button variant="primary">+ Nhập hàng</Button>
        </Link>
      </header>

      <Card>
        <div className="toolbar">
          <div className="segmented" role="group" aria-label="Lọc theo trạng thái">
            {STATUS_FILTERS.map((filter) => (
              <button
                key={filter.value}
                type="button"
                className={`segmented__item ${status === filter.value ? 'segmented__item--active' : ''}`}
                onClick={() => {
                  setStatus(filter.value)
                  setPage(1)
                }}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <form
            className="search"
            onSubmit={(event) => {
              event.preventDefault()
              applySearch(searchInput)
            }}
          >
            <input
              className="input search__input"
              placeholder="🔍 Tìm mã giày…"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              aria-label="Tìm mã giày"
            />
            <Button type="submit">Tìm</Button>
            {search && (
              <Button
                type="button"
                onClick={() => {
                  setSearchInput('')
                  applySearch('')
                }}
              >
                Xoá
              </Button>
            )}
          </form>
        </div>

        {query.isPending && <LoadingState />}
        {query.isError && (
          <ErrorState
            error={query.error}
            onRetry={() => void query.refetch()}
            title="Không tải được danh sách nhập hàng"
          />
        )}

        {result && result.items.length === 0 &&
          (hasFilters ? (
            <EmptyState
              icon="🔍"
              title="Không tìm thấy đơn hàng phù hợp"
              description="Thử đổi bộ lọc trạng thái hoặc từ khoá tìm kiếm."
            />
          ) : (
            <EmptyState
              title="Chưa có đơn hàng nào"
              description="Nhập lô hàng đầu tiên để bắt đầu theo dõi sản xuất."
              action={
                <Link to="/goods-receipt/new">
                  <Button variant="primary">+ Nhập hàng</Button>
                </Link>
              }
            />
          ))}

        {result && result.items.length > 0 && (
          <div className="table-wrapper table-wrapper--fill">
            <table className="table receipt-table">
              {/* Mã giày và dây chuyền không khai báo độ rộng: hai cột này chia đều phần còn lại. */}
              <colgroup>
                <col className="receipt-table__col--image" />
                <col />
                <col className="receipt-table__col--quantity" />
                <col />
                <col className="receipt-table__col--period" />
                <col className="receipt-table__col--status" />
                <col className="receipt-table__col--actions" />
              </colgroup>
              <thead>
                <tr>
                  <th>Ảnh</th>
                  <th>Mã giày</th>
                  <th className="num">Số lượng</th>
                  <th>Dây chuyền</th>
                  <th>Thời gian</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((order) => (
                  <tr key={order.id} className="table__row--hover">
                    <td>
                      <OrderThumbnail imageUrl={order.imageUrl} shoeCode={order.shoeCode} />
                    </td>
                    <td className="table__strong receipt-table__wrap">{order.shoeCode}</td>
                    <td className="num">{formatNumber(order.quantity)}</td>
                    <td>
                      <ProductionLineTags lines={order.productionLines} />
                    </td>
                    <td>
                      {order.startDate && order.dueDate ? (
                        <>
                          <span>{formatDate(order.startDate)}</span>
                          <span className="table__sub">→ {formatDate(order.dueDate)}</span>
                        </>
                      ) : (
                        <span className="muted">Chưa lên lịch</span>
                      )}
                    </td>
                    <td className="receipt-table__wrap">
                      <OrderStatusBadge status={order.status} />
                      {order.isOverdue && (
                        <>
                          {' '}
                          <Badge tone="danger">Quá hạn</Badge>
                        </>
                      )}
                    </td>
                    <td className="table__actions">
                      <div>
                        {order.status === 'Pending' ? (
                          <Link to="/progress/new" search={{ orderId: order.id }}>
                            <Button variant="primary">Lập tiến độ</Button>
                          </Link>
                        ) : (
                          <Link to="/progress/$orderId" params={{ orderId: order.id }}>
                            <Button>Xem tiến độ</Button>
                          </Link>
                        )}
                        <Link to="/goods-receipt/$orderId" params={{ orderId: order.id }}>
                          <Button variant="ghost">Sửa</Button>
                        </Link>
                        {order.status === 'Pending' && (
                          <Button
                            variant="ghost"
                            onClick={() => {
                              deleteOrder.reset()
                              setDeleting(order)
                            }}
                          >
                            Xoá
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {result && result.totalCount > 0 && (
          <div className="pagination">
            <span className="pagination__info">
              {formatNumber(result.totalCount)} đơn hàng · hiển thị{' '}
              {formatNumber((result.page - 1) * result.pageSize + 1)}–
              {formatNumber(Math.min(result.page * result.pageSize, result.totalCount))}
            </span>

            <div className="pagination__controls">
              <div className="pagination__size">
                Số dòng
                <Select
                  value={pageSize}
                  options={PAGE_SIZES.map((size) => ({ value: size, label: String(size) }))}
                  onChange={(next) => {
                    setPageSize(next)
                    setPage(1)
                  }}
                  aria-label="Số dòng mỗi trang"
                />
              </div>

              <Button disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
                ← Trước
              </Button>
              <span className="pagination__page">
                Trang {result.page} / {totalPages}
              </span>
              <Button disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
                Sau →
              </Button>
            </div>
          </div>
        )}
      </Card>

      <ConfirmDialog
        open={deleting !== null}
        title="Xoá đơn nhập hàng?"
        confirmLabel="Xoá đơn"
        tone="danger"
        loading={deleteOrder.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => void confirmDelete()}
      >
        {deleting && (
          <>
            <p>
              Đơn <strong>{deleting.shoeCode}</strong> ({formatNumber(deleting.quantity)} đôi)
              {deleting.hasImage && ' cùng ảnh mẫu'} sẽ bị xoá vĩnh viễn, không khôi phục được.
            </p>
            {deleteOrder.isError && <InlineError message={toUserMessage(deleteOrder.error)} />}
          </>
        )}
      </ConfirmDialog>
    </div>
  )
}
