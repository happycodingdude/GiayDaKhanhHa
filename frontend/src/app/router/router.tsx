import {
  createRootRouteWithContext,
  createRoute,
  createRouter,
  Outlet,
  redirect,
} from '@tanstack/react-router'
import type { QueryClient } from '@tanstack/react-query'
import { ApiError } from '../../api/errors'
import { authApi } from '../../features/auth/api/authApi'
import { LoginPage } from '../../features/auth/pages/LoginPage'
import { CreateOrderPage } from '../../features/orders/pages/CreateOrderPage'
import { OrderDetailPage } from '../../features/orders/pages/OrderDetailPage'
import { OrderListPage } from '../../features/orders/pages/OrderListPage'
import { SettingsPage } from '../../features/settings/pages/SettingsPage'
import { DashboardPage } from '../../features/statistics/pages/DashboardPage'
import { AppLayout } from '../layouts/AppLayout'
import { queryKeys } from '../config/queryKeys'

interface RouterContext {
  queryClient: QueryClient
}

const rootRoute = createRootRouteWithContext<RouterContext>()({
  component: Outlet,
})

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/login',
  component: LoginPage,
})

/**
 * Vùng cần đăng nhập. Người dùng hiện tại được resolve một lần trước khi render khung, nên
 * phiên hết hạn sẽ về thẳng /login thay vì chớp qua một màn hình trống.
 */
const authenticatedRoute = createRoute({
  getParentRoute: () => rootRoute,
  id: 'authenticated',
  beforeLoad: async ({ context }) => {
    try {
      await context.queryClient.ensureQueryData({
        queryKey: queryKeys.currentUser,
        queryFn: () => authApi.me(),
      })
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        throw redirect({ to: '/login' })
      }
      throw error
    }
  },
  component: AppLayout,
})

const indexRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/',
  beforeLoad: () => {
    throw redirect({ to: '/dashboard' })
  },
})

const dashboardRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/dashboard',
  component: DashboardPage,
})

const ordersRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/orders',
  component: OrderListPage,
})

const createOrderRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/orders/new',
  component: CreateOrderPage,
})

const orderDetailRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/orders/$orderId',
  component: OrderDetailPage,
})

const settingsRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/settings',
  component: SettingsPage,
})

const routeTree = rootRoute.addChildren([
  loginRoute,
  authenticatedRoute.addChildren([
    indexRoute,
    dashboardRoute,
    ordersRoute,
    createOrderRoute,
    orderDetailRoute,
    settingsRoute,
  ]),
])

export function createAppRouter(queryClient: QueryClient) {
  return createRouter({
    routeTree,
    context: { queryClient },
    defaultPreload: 'intent',
  })
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof createAppRouter>
  }
}
