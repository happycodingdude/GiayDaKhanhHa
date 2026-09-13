import { Link, Outlet, useNavigate } from '@tanstack/react-router'
import { formatDate, formatWeekday, today } from '../../shared/lib/date'
import { useCurrentUser, useLogout } from '../../features/auth/hooks/useAuth'
import { Button } from '../../shared/components/ui'

/**
 * Khung ứng dụng sau khi đăng nhập. Điều hướng sau CR-001: Dashboard · Nhập hàng · Tiến độ · Cấu
 * hình. Nhập hàng là bước "hàng về"; kế hoạch, sản lượng và bù thiếu đều đi vào từ Tiến độ
 * (CR-001 §7.1).
 *
 * Sidebar là cột cố định cao hết màn hình, chứa thương hiệu, điều hướng và tài khoản đang đăng
 * nhập. Chỉ cột nội dung được cuộn.
 */
export function AppLayout() {
  const { data: user } = useCurrentUser()
  const logout = useLogout()
  const navigate = useNavigate()

  const onLogout = async () => {
    await logout.mutateAsync()
    await navigate({ to: '/login' })
  }

  return (
    <div className="shell">
      <aside className="shell__sidebar">
        <div className="shell__brand">
          <span aria-hidden="true">👟</span>
          <span>Quản lý sản xuất</span>
        </div>

        <nav className="shell__nav">
          <Link to="/dashboard" className="shell__link" activeProps={{ className: 'shell__link shell__link--active' }}>
            <span aria-hidden="true">📊</span> Dashboard
          </Link>
          <Link to="/goods-receipt" className="shell__link" activeProps={{ className: 'shell__link shell__link--active' }}>
            <span aria-hidden="true">📦</span> Nhập hàng
          </Link>
          <Link to="/progress" className="shell__link" activeProps={{ className: 'shell__link shell__link--active' }}>
            <span aria-hidden="true">📈</span> Tiến độ
          </Link>

          {/* Cấu hình là một nhóm: dây chuyền sản xuất nằm dưới nó, không phải một mục ngang hàng
              với Nhập hàng và Tiến độ (CR-001 §7.1). */}
          <span className="shell__group-label">Cấu hình</span>
          <Link
            to="/settings/production-lines"
            className="shell__link shell__link--nested"
            activeProps={{ className: 'shell__link shell__link--nested shell__link--active' }}
          >
            <span aria-hidden="true">🏭</span> Dây chuyền sản xuất
          </Link>
          {/* Khớp chính xác: mặc định Link active theo tiền tố nên sẽ sáng cả khi đang ở
              /settings/production-lines, trùng với mục Dây chuyền sản xuất ở trên. */}
          <Link
            to="/settings"
            activeOptions={{ exact: true }}
            className="shell__link shell__link--nested"
            activeProps={{ className: 'shell__link shell__link--nested shell__link--active' }}
          >
            <span aria-hidden="true">⚙️</span> Cấu hình chung
          </Link>
        </nav>

        <div className="shell__account">
          <div className="shell__user">
            <span className="shell__avatar" aria-hidden="true">
              {user?.displayName?.trim().charAt(0).toUpperCase() ?? '·'}
            </span>
            <span className="shell__user-text">
              <span className="shell__user-name">{user?.displayName ?? '…'}</span>
              <span className="shell__date">
                {formatWeekday(today())} · {formatDate(today())}
              </span>
            </span>
          </div>
          <Button variant="primary" onClick={onLogout} loading={logout.isPending} className="shell__logout">
            <span aria-hidden="true">⇥</span> Đăng xuất
          </Button>
        </div>
      </aside>

      <main className="shell__content">
        <Outlet />
      </main>
    </div>
  )
}
