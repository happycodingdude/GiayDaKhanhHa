import { Link, Outlet, useNavigate } from '@tanstack/react-router'
import { formatDate, today } from '../../shared/lib/date'
import { useCurrentUser, useLogout } from '../../features/auth/hooks/useAuth'
import { Button } from '../../shared/components/ui'

/**
 * The authenticated shell. Phase 1 navigation is Dashboard + Đơn hàng; production plans, actuals
 * and adjustments are reached through the Order workflow (Step 5 §6).
 *
 * The sidebar is a fixed full-height column that carries the brand, the navigation and the
 * signed-in account. Only the content column scrolls.
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
          <p className="shell__date">{formatDate(today())}</p>
          <p className="shell__user-name">
            <span aria-hidden="true">👤</span> {user?.displayName ?? '…'}
          </p>
          <Button variant="ghost" onClick={onLogout} loading={logout.isPending} className="shell__logout">
            Đăng xuất
          </Button>
        </div>
      </aside>

      <main className="shell__content">
        <Outlet />
      </main>
    </div>
  )
}
