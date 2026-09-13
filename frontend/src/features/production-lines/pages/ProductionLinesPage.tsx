import { useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Badge, Button, Card } from '../../../shared/components/ui'
import { ConfirmDialog } from '../../../shared/dialogs/ConfirmDialog'
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { ProductionLineDialog } from '../components/ProductionLineDialog'
import { useChangeProductionLineStatus, useProductionLines } from '../hooks/useProductionLines'
import type { ProductionLineDto } from '../types'

/**
 * Cấu hình dây chuyền sản xuất (CR-001 §7.9). Không có nút xoá ở Phase này: dây chuyền đã dùng thì
 * không xoá được, và dây chuyền chưa dùng cũng chỉ cần tắt (QĐ-9, BR-N15).
 */
export function ProductionLinesPage() {
  const { showToast } = useToast()
  const query = useProductionLines()
  const changeStatus = useChangeProductionLineStatus()

  const [editing, setEditing] = useState<ProductionLineDto | null>(null)
  const [creating, setCreating] = useState(false)
  const [confirming, setConfirming] = useState<ProductionLineDto | null>(null)

  const toggle = async (line: ProductionLineDto) => {
    const status = line.status === 'Active' ? 'Inactive' : 'Active'

    try {
      await changeStatus.mutateAsync({ id: line.id, status })
      showToast(
        status === 'Active'
          ? `Đã bật lại dây chuyền ${line.code}.`
          : `Đã ngừng hoạt động dây chuyền ${line.code}.`,
      )
    } catch (error) {
      showToast(toUserMessage(error), 'error')
    } finally {
      setConfirming(null)
    }
  }

  const lines = query.data?.items ?? []

  return (
    <div className="page page--fill">
      <header className="page__header">
        <div>
          <h1 className="page__title">Dây chuyền sản xuất</h1>
          <p className="page__subtitle">
            Danh mục dây chuyền dùng khi lập tiến độ. Dây chuyền ngừng hoạt động vẫn giữ nguyên dữ
            liệu lịch sử, chỉ không xuất hiện trong lựa chọn mới.
          </p>
        </div>
        <Button variant="primary" onClick={() => setCreating(true)}>
          + Thêm dây chuyền
        </Button>
      </header>

      <Card>
        {query.isPending && <LoadingState />}

        {query.isError && (
          <ErrorState
            error={query.error}
            onRetry={() => void query.refetch()}
            title="Không tải được danh sách dây chuyền"
          />
        )}

        {query.isSuccess && lines.length === 0 && (
          <EmptyState
            icon="🏭"
            title="Chưa cấu hình dây chuyền"
            description="Thêm ít nhất một dây chuyền trước khi lập tiến độ cho đơn hàng."
            action={
              <Button variant="primary" onClick={() => setCreating(true)}>
                + Thêm dây chuyền
              </Button>
            }
          />
        )}

        {lines.length > 0 && (
          <div className="table-wrapper table-wrapper--fill">
            <table className="table">
              <thead>
                <tr>
                  <th>Mã</th>
                  <th>Tên</th>
                  <th className="num">Thứ tự</th>
                  <th>Ghi chú</th>
                  <th>Trạng thái</th>
                  <th>Đang dùng</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {lines.map((line) => (
                  <tr key={line.id}>
                    <td className="table__strong">{line.code}</td>
                    <td>{line.name}</td>
                    <td className="num">{line.sortOrder}</td>
                    <td className="muted">{line.note ?? '—'}</td>
                    <td>
                      {line.status === 'Active' ? (
                        <Badge tone="success">● Đang hoạt động</Badge>
                      ) : (
                        <Badge tone="neutral">○ Ngừng hoạt động</Badge>
                      )}
                    </td>
                    <td>{line.inUse ? <Badge tone="info">Có</Badge> : <span className="muted">Không</span>}</td>
                    <td className="table__actions">
                      <div>
                        <Button onClick={() => setEditing(line)}>Sửa</Button>
                        <Button
                          variant={line.status === 'Active' ? 'danger' : 'primary'}
                          onClick={() => setConfirming(line)}
                        >
                          {line.status === 'Active' ? 'Ngừng' : 'Bật lại'}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <ProductionLineDialog open={creating} line={null} onClose={() => setCreating(false)} />
      <ProductionLineDialog open={editing !== null} line={editing} onClose={() => setEditing(null)} />

      <ConfirmDialog
        open={confirming !== null}
        title={confirming?.status === 'Active' ? 'Ngừng hoạt động dây chuyền?' : 'Bật lại dây chuyền?'}
        confirmLabel={confirming?.status === 'Active' ? 'Ngừng hoạt động' : 'Bật lại'}
        tone={confirming?.status === 'Active' ? 'danger' : 'primary'}
        loading={changeStatus.isPending}
        onCancel={() => setConfirming(null)}
        onConfirm={() => confirming && void toggle(confirming)}
      >
        {confirming?.status === 'Active' ? (
          <p>
            Dây chuyền <strong>{confirming.code}</strong> sẽ không còn xuất hiện khi lập tiến độ mới.
            {confirming.inUse && ' Các đơn hàng đang chạy trên dây chuyền này không bị ảnh hưởng.'}
          </p>
        ) : (
          <p>
            Dây chuyền <strong>{confirming?.code}</strong> sẽ xuất hiện trở lại trong danh sách chọn
            khi lập tiến độ.
          </p>
        )}
      </ConfirmDialog>
    </div>
  )
}
