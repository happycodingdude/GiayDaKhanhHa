#!/usr/bin/env node
/**
 * Nạp dữ liệu mẫu qua chính REST API của ứng dụng.
 *
 * Cố ý KHÔNG ghi thẳng vào PostgreSQL: đi qua API nghĩa là mọi business rule
 * (tổng kế hoạch = tổng đơn, tổng thực tế <= tổng đơn, preview trước khi apply,
 * tối đa 1 adjustment đang áp dụng cho mỗi ngày nguồn…) đều được kiểm tra thật,
 * nên dữ liệu mẫu chắc chắn là dữ liệu hợp lệ chứ không phải dữ liệu "vẽ tay".
 *
 * Cách dùng:
 *   node mock-data/seed.mjs
 *   PM_PASSWORD='...' node mock-data/seed.mjs
 *   PM_API=http://localhost:5080 PM_USERNAME=manager node mock-data/seed.mjs
 */

import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const API = process.env.PM_API ?? 'http://localhost:5080'
const USERNAME = process.env.PM_USERNAME ?? 'manager'
const PASSWORD = process.env.PM_PASSWORD ?? process.argv[2]

const here = dirname(fileURLToPath(import.meta.url))

let cookie = ''

const colors = {
  reset: '\x1b[0m',
  dim: '\x1b[2m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  cyan: '\x1b[36m',
}

const log = {
  step: (msg) => console.log(`${colors.cyan}▸${colors.reset} ${msg}`),
  ok: (msg) => console.log(`  ${colors.green}✓${colors.reset} ${msg}`),
  info: (msg) => console.log(`  ${colors.dim}${msg}${colors.reset}`),
  warn: (msg) => console.log(`${colors.yellow}!${colors.reset} ${msg}`),
  fail: (msg) => console.error(`${colors.red}✗${colors.reset} ${msg}`),
}

/** Ngày nghiệp vụ là chuỗi YYYY-MM-DD, không đi qua timezone. */
function dateFromOffset(offset) {
  const now = new Date()
  const base = new Date(now.getFullYear(), now.getMonth(), now.getDate() + offset)
  const month = String(base.getMonth() + 1).padStart(2, '0')
  const day = String(base.getDate()).padStart(2, '0')
  return `${base.getFullYear()}-${month}-${day}`
}

class ApiError extends Error {
  constructor(status, body) {
    super(body?.message ?? `HTTP ${status}`)
    this.status = status
    this.code = body?.code ?? 'UNKNOWN'
    this.details = body?.details ?? null
  }
}

async function call(method, path, body) {
  let response
  try {
    response = await fetch(`${API}/api/v1${path}`, {
      method,
      headers: {
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        ...(cookie ? { Cookie: cookie } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new Error(
      `Không kết nối được tới ${API}.\n` +
        `  Hãy chạy backend trước:\n` +
        `    docker compose up -d\n` +
        `    cd backend/src/ProductionManagement.Api && dotnet run --urls ${API}`,
    )
  }

  // Cookie xác thực là HttpOnly; script giữ lại để dùng cho các request sau.
  const setCookie = response.headers.getSetCookie?.() ?? []
  if (setCookie.length > 0) {
    cookie = setCookie.map((entry) => entry.split(';')[0]).join('; ')
  }

  if (response.status === 204) return null

  const text = await response.text()
  const payload = text ? JSON.parse(text) : null

  if (!response.ok) throw new ApiError(response.status, payload)
  return payload
}

async function login() {
  if (!PASSWORD) {
    throw new Error(
      'Thiếu mật khẩu.\n' +
        "  Dùng:  PM_PASSWORD='<mật-khẩu>' node mock-data/seed.mjs\n" +
        '  Mật khẩu là chuỗi backend in ra log lần đầu khởi động,\n' +
        '  hoặc giá trị bạn đặt ở biến môi trường Bootstrap__Password.',
    )
  }

  try {
    const user = await call('POST', '/auth/login', { username: USERNAME, password: PASSWORD })
    log.ok(`Đăng nhập: ${user.displayName} (${user.username})`)
  } catch (error) {
    if (error.status === 401) {
      throw new Error(`Sai tên đăng nhập hoặc mật khẩu cho tài khoản '${USERNAME}'.`)
    }
    throw error
  }
}

async function createOrder(scenario) {
  const productionPlans = scenario.plan.map((plannedQuantity, index) => ({
    productionDate: dateFromOffset(scenario.startOffset + index),
    plannedQuantity,
  }))

  // Tổng số lượng đơn luôn được tính từ kế hoạch, nên bất biến
  // SUM(initialPlannedQuantity) == quantity không bao giờ bị lệch.
  const quantity = scenario.plan.reduce((sum, value) => sum + value, 0)

  return call('POST', '/orders', {
    orderCode: scenario.orderCode,
    quantity,
    startDate: productionPlans[0].productionDate,
    dueDate: productionPlans[productionPlans.length - 1].productionDate,
    productionPlans,
  })
}

async function seedScenario(scenario) {
  log.step(`${scenario.orderCode} — ${scenario.label}`)

  const order = await createOrder(scenario)
  log.ok(`Tạo đơn ${order.quantity} đôi, ${scenario.plan.length} ngày sản xuất`)

  for (const actual of scenario.actuals) {
    await call('POST', `/orders/${order.id}/production-records`, {
      productionDate: dateFromOffset(actual.dayOffset),
      actualQuantity: actual.quantity,
    })
  }

  if (scenario.actuals.length > 0) {
    log.ok(`Nhập sản lượng cho ${scenario.actuals.length} ngày`)
  }

  for (const adjustment of scenario.adjustments) {
    await seedAdjustment(order, scenario, adjustment)
  }

  const final = await call('GET', `/orders/${order.id}`)
  log.info(
    `trạng thái=${final.status} · thực tế=${final.totalActual}/${final.quantity} · ` +
      `kế hoạch=${final.totalPlan} · tiến độ=${final.progressPercentage}%` +
      (final.behindQuantity > 0 ? ` · chậm ${final.behindQuantity} đôi` : ''),
  )

  return final
}

async function seedAdjustment(order, scenario, adjustment) {
  const days = (await call('GET', `/orders/${order.id}/production-plans`)).items
  const sourceDate = dateFromOffset(adjustment.sourceDayOffset)
  const source = days.find((day) => day.productionDate === sourceDate)

  if (!source) throw new Error(`Không tìm thấy ngày nguồn ${sourceDate}`)
  if (source.shortageQuantity <= 0) {
    throw new Error(`Ngày ${sourceDate} không có sản lượng thiếu để bù.`)
  }

  const request =
    adjustment.type === 'Automatic'
      ? { adjustmentType: 'Automatic' }
      : {
          adjustmentType: 'Manual',
          targets: [
            {
              productionPlanId: days.find(
                (day) => day.productionDate === dateFromOffset(adjustment.targetDayOffset),
              ).id,
              // Option 1 luôn bù trọn vẹn phần thiếu.
              addOnQuantity: source.shortageQuantity,
            },
          ],
        }

  // Luôn preview trước rồi mới apply, đúng luồng nghiệp vụ đã chốt.
  const preview = await call('POST', `/production-plans/${source.id}/adjustments/preview`, request)
  if (!preview.valid) {
    throw new Error(`Đề xuất bù không hợp lệ: ${preview.validationMessage}`)
  }

  const applied = await call('POST', `/production-plans/${source.id}/adjustments`, {
    adjustmentType: preview.adjustmentType,
    shortageQuantity: preview.shortageQuantity,
    targets: preview.items.map((item) => ({
      productionPlanId: item.productionPlanId,
      addOnQuantity: item.addOnQuantity,
    })),
  })

  const label = adjustment.type === 'Automatic' ? 'chia đều' : 'dồn 1 ngày'
  const split = preview.items.map((item) => `+${item.addOnQuantity}`).join(' / ')
  log.ok(`Bù ${preview.shortageQuantity} đôi (${label}): ${split}`)

  if (adjustment.thenReverse) {
    await call('POST', `/plan-adjustments/${applied.id}/reverse`, undefined)
    log.ok('Hoàn tác lần bù trên (giữ lại trong lịch sử)')
  }
}

async function main() {
  console.log(`\n${colors.cyan}Nạp dữ liệu mẫu — Quản lý sản xuất${colors.reset}`)
  console.log(`${colors.dim}API: ${API}${colors.reset}\n`)

  const { orders: scenarios } = JSON.parse(await readFile(join(here, 'scenarios.json'), 'utf8'))

  await login()
  console.log()

  // Đơn hàng không có API xoá (lịch sử sản xuất là bất biến), nên nếu mã đơn đã tồn tại
  // thì cần reset database thay vì ghi đè.
  const existing = await call('GET', '/orders?pageSize=200')
  const clash = scenarios.find((scenario) =>
    existing.items.some((order) => order.orderCode === scenario.orderCode),
  )

  if (clash) {
    log.warn(`Mã đơn ${clash.orderCode} đã tồn tại — dữ liệu mẫu đã được nạp trước đó.`)
    log.info('Muốn nạp lại từ đầu, xoá sạch database rồi khởi động lại backend:')
    log.info('  docker compose down -v && docker compose up -d')
    return
  }

  for (const scenario of scenarios) {
    await seedScenario(scenario)
    console.log()
  }

  const dashboard = await call('GET', '/statistics/dashboard')
  console.log(`${colors.green}Xong.${colors.reset}`)
  console.log(
    `${colors.dim}${dashboard.totalOrders} đơn · ${dashboard.incompleteOrders} đang chạy · ` +
      `${dashboard.behindOrders} đang chậm · ${dashboard.completedOrders} hoàn thành · ` +
      `${dashboard.totalActualQuantity}/${dashboard.totalOrderQuantity} đôi${colors.reset}`,
  )
  console.log(`\nMở http://localhost:5173 để kiểm tra.\n`)
}

main().catch((error) => {
  console.error()
  log.fail(error.message)
  if (error.details) console.error(error.details)
  process.exitCode = 1
})
