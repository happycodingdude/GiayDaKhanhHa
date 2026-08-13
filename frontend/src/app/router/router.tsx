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
 * Protected area. The current user is resolved once before the shell renders, so an expired
 * session lands on /login instead of flashing an empty screen.
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

const routeTree = rootRoute.addChildren([
  loginRoute,
  authenticatedRoute.addChildren([
    indexRoute,
    dashboardRoute,
    ordersRoute,
    createOrderRoute,
    orderDetailRoute,
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
