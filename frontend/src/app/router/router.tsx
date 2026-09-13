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
import {
  CreateGoodsReceiptPage,
  EditGoodsReceiptPage,
} from '../../features/orders/pages/GoodsReceiptFormPage'
import { GoodsReceiptListPage } from '../../features/orders/pages/GoodsReceiptListPage'
import { ProductionLinesPage } from '../../features/production-lines/pages/ProductionLinesPage'
import { CreateSchedulePage } from '../../features/production/pages/CreateSchedulePage'
import { ProgressDetailPage } from '../../features/production/pages/ProgressDetailPage'
import { ProgressListPage } from '../../features/production/pages/ProgressListPage'
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

// --- Nhập hàng (CR-001 §7.2) -----------------------------------------------------------------

const goodsReceiptRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/goods-receipt',
  component: GoodsReceiptListPage,
})

const createGoodsReceiptRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/goods-receipt/new',
  component: CreateGoodsReceiptPage,
})

const goodsReceiptDetailRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/goods-receipt/$orderId',
  component: EditGoodsReceiptPage,
})

// --- Tiến độ ----------------------------------------------------------------------------------

const progressRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/progress',
  component: ProgressListPage,
})

const createScheduleRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/progress/new',
  // Vào từ nút "Lập tiến độ" của màn Nhập hàng thì đơn được chọn sẵn qua query string.
  validateSearch: (search: Record<string, unknown>): { orderId?: string } => ({
    orderId: typeof search.orderId === 'string' ? search.orderId : undefined,
  }),
  component: CreateSchedulePage,
})

const progressDetailRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/progress/$orderId',
  component: ProgressDetailPage,
})

// --- Cấu hình ---------------------------------------------------------------------------------

const settingsRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/settings',
  component: SettingsPage,
})

const productionLinesRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/settings/production-lines',
  component: ProductionLinesPage,
})

/**
 * `/orders*` là đường dẫn của baseline trước CR-001. Giữ redirect để bookmark cũ không vỡ
 * (CR-001 §7.2): danh sách đơn hàng cũ chính là màn Tiến độ, và chi tiết đơn cũ là chi tiết tiến độ.
 */
const legacyOrdersRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/orders',
  beforeLoad: () => {
    throw redirect({ to: '/progress' })
  },
})

const legacyCreateOrderRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/orders/new',
  beforeLoad: () => {
    // Tạo đơn hàng cũ = nhập hàng: đó là bước đầu tiên của luồng mới.
    throw redirect({ to: '/goods-receipt/new' })
  },
})

const legacyOrderDetailRoute = createRoute({
  getParentRoute: () => authenticatedRoute,
  path: '/orders/$orderId',
  beforeLoad: ({ params }) => {
    throw redirect({ to: '/progress/$orderId', params: { orderId: params.orderId } })
  },
})

const routeTree = rootRoute.addChildren([
  loginRoute,
  authenticatedRoute.addChildren([
    indexRoute,
    dashboardRoute,
    goodsReceiptRoute,
    createGoodsReceiptRoute,
    goodsReceiptDetailRoute,
    progressRoute,
    createScheduleRoute,
    progressDetailRoute,
    settingsRoute,
    productionLinesRoute,
    legacyOrdersRoute,
    legacyCreateOrderRoute,
    legacyOrderDetailRoute,
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
