import { Link, Outlet, useNavigate } from '@tanstack/react-router'
import { formatDate, formatWeekday, today } from '../../shared/lib/date'
import { useCurrentUser, useLogout } from '../../features/auth/hooks/useAuth'
import { Button } from '../../shared/components/ui'

/**
 * Khung ứng dụng sau khi đăng nhập. Điều hướng Phase 1 gồm Dashboard + Đơn hàng; kế hoạch sản
 * xuất, sản lượng thực tế và điều chỉnh đều đi vào từ luồng Đơn hàng (Step 5 §6).
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
          <Link to="/orders" className="shell__link" activeProps={{ className: 'shell__link shell__link--active' }}>
            <span aria-hidden="true">📦</span> Đơn hàng
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
          <Button variant="ghost" onClick={onLogout} loading={logout.isPending} className="shell__logout">
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
