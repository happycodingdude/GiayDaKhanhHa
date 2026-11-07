#!/usr/bin/env node
/**
 * Nạp dữ liệu mẫu bằng cách ghi THẲNG vào PostgreSQL — không đi qua REST API.
 *
 * Hệ thống đang ở giai đoạn dev: dữ liệu nghiệp vụ không cần giữ, nên mỗi lần chạy script xoá sạch
 * đơn hàng cũ (cùng kế hoạch, sản lượng, điều chỉnh) rồi nạp lại từ scenarios.json. Tài khoản, cấu
 * hình chung và danh mục dây chuyền được giữ nguyên.
 *
 * Không có API kiểm tra hộ nên script tự giữ các bất biến mà API vẫn đảm bảo: tổng phân bổ = số
 * lượng đơn, ô kế hoạch 0 không có dòng plan, chỉ tiến độ đã chốt mới có sản lượng, Xuất hàng chốt
 * sổ cả ngày (ô chưa ghi nhận gì chốt với 0), trạng thái đơn tính lại khi Xuất hàng, bù sản lượng
 * chỉ vào ngày còn mở phía sau trên cùng dây chuyền, chia đều dồn phần dư lên ngày sớm.
 *
 * Cách dùng (app phải đã chạy ít nhất một lần để có schema, tài khoản quản lý và dây chuyền mẫu):
 *   node mock-data/seed.mjs                            # nạp mọi kịch bản
 *   node mock-data/seed.mjs ORD-2026-009 ORD-2026-011  # chỉ nạp các kịch bản được liệt kê
 *
 * Biến môi trường tuỳ chọn: PM_DB_CONTAINER (mặc định giayda-postgres), POSTGRES_DB
 * (production_management), POSTGRES_USER (postgres), PM_USERNAME (manager — người ghi nhận),
 * PM_TIMEZONE (Asia/Ho_Chi_Minh — phải trùng Business__TimeZone của backend).
 */

import { spawnSync } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const CONTAINER = process.env.PM_DB_CONTAINER ?? 'giayda-postgres'
const DATABASE = process.env.POSTGRES_DB ?? 'production_management'
const DB_USER = process.env.POSTGRES_USER ?? 'postgres'
const USERNAME = process.env.PM_USERNAME ?? 'manager'
const TIME_ZONE = process.env.PM_TIMEZONE ?? 'Asia/Ho_Chi_Minh'

const RUN_APP =
  'docker compose -f docker-compose.yml -f docker-compose.local.yml --env-file .env.local up -d --build'

const here = dirname(fileURLToPath(import.meta.url))

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
  fail: (msg) => console.log(`${colors.red}✗${colors.reset} ${msg}`),
}

// --- Thời gian ----------------------------------------------------------------------------------

const now = new Date()

/** Hôm nay theo múi giờ nghiệp vụ, không theo máy chạy script. */
const today = new Intl.DateTimeFormat('en-CA', { timeZone: TIME_ZONE }).format(now)

function addDays(date, days) {
  const value = new Date(`${date}T00:00:00Z`)
  value.setUTCDate(value.getUTCDate() + days)
  return value.toISOString().slice(0, 10)
}

const dateFromOffset = (offset) => addDays(today, offset)

/** Độ lệch múi giờ nghiệp vụ của một ngày, dạng "+07:00". */
function zoneOffset(date) {
  const name = new Intl.DateTimeFormat('en-US', { timeZone: TIME_ZONE, timeZoneName: 'longOffset' })
    .formatToParts(new Date(`${date}T12:00:00Z`))
    .find((part) => part.type === 'timeZoneName').value
  return name === 'GMT' ? '+00:00' : name.slice(3)
}

/** Một giờ cụ thể của một ngày nghiệp vụ, ví dụ at('2026-10-01', '08:00'). */
const at = (date, time) => new Date(`${date}T${time}:00${zoneOffset(date)}`)
const addMinutes = (moment, minutes) => new Date(moment.getTime() + minutes * 60_000)
const earliest = (...moments) => new Date(Math.min(...moments.map((moment) => moment.getTime())))
const latest = (...moments) => new Date(Math.max(...moments.map((moment) => moment.getTime())))

// --- SQL ----------------------------------------------------------------------------------------

/** UUIDv7 như backend dùng (Guid.CreateVersion7): 48 bit thời gian rồi tới phần ngẫu nhiên. */
function uuidv7(moment = now) {
  const bytes = randomBytes(16)
  bytes.writeUIntBE(moment.getTime(), 0, 6)
  bytes[6] = (bytes[6] & 0x0f) | 0x70
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = bytes.toString('hex')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

const raw = (sql) => ({ raw: sql })

function literal(value) {
  if (value === null || value === undefined) return 'NULL'
  if (typeof value === 'object' && 'raw' in value) return value.raw
  if (value instanceof Date) return `'${value.toISOString()}'`
  if (typeof value === 'number') return String(value)
  return `'${String(value).replaceAll("'", "''")}'`
}

function insert(table, row) {
  const columns = Object.keys(row)
  return `INSERT INTO ${table} (${columns.join(', ')}) VALUES (${columns.map((c) => literal(row[c])).join(', ')});`
}

/** Người thao tác của mọi bản ghi — tài khoản quản lý mà backend tạo lúc khởi động lần đầu. */
const USER = raw('(SELECT id FROM seed_user)')
const lineRef = (code) => raw(`(SELECT id FROM production_lines WHERE code = ${literal(code)})`)

// --- Kịch bản -----------------------------------------------------------------------------------

const sum = (values) => values.reduce((total, value) => total + value, 0)

/** Cùng quy tắc với EvenDistribution của backend: phần dư dồn lên các phần tử đầu. */
function splitEvenly(total, count) {
  const base = Math.floor(total / count)
  const remainder = total % count
  return Array.from({ length: count }, (_, index) => base + (index < remainder ? 1 : 0))
}

/**
 * Dựng toàn bộ dữ liệu của một kịch bản trong bộ nhớ rồi trả về các câu INSERT. Mọi kiểm tra đều
 * nằm ở đây, trước khi chạm database.
 *
 * @param stamp mốc "lúc này" của kịch bản: kịch bản sau mới hơn kịch bản trước, và mọi thời điểm
 *              của nó không vượt quá mốc này.
 */
function buildScenario(scenario, stamp, knownLines) {
  const fail = (message) => {
    throw new Error(message)
  }

  const scheduled = scenario.lines.length > 0
  const confirmed = scheduled && scenario.confirmed !== false

  for (const line of scenario.lines) {
    if (!knownLines.has(line.code)) fail(`Dây chuyền ${line.code} không có trong danh mục mẫu.`)
    if (sum(line.plan) <= 0) fail(`Dây chuyền ${line.code} phải được phân bổ số lượng lớn hơn 0.`)
  }

  const quantity = scheduled ? sum(scenario.lines.map((line) => sum(line.plan))) : scenario.quantity
  if (!(quantity > 0)) fail('Đơn chưa lập tiến độ phải khai báo quantity > 0.')

  const order = {
    id: uuidv7(),
    quantity,
    status: scheduled ? 'Incomplete' : 'Pending',
    startDate: null,
    dueDate: null,
    receivedAt: stamp,
    scheduledAt: null,
    confirmedAt: null,
    updatedAt: stamp,
  }

  const plans = []
  if (scheduled) {
    const dayCount = Math.max(...scenario.lines.map((line) => line.plan.length))
    order.startDate = dateFromOffset(scenario.startOffset)
    order.dueDate = dateFromOffset(scenario.startOffset + dayCount - 1)

    // Nhập hàng 2 ngày trước ngày bắt đầu, lập tiến độ hôm trước, chốt ngay sau đó — nhưng không
    // bao giờ muộn hơn mốc của kịch bản (đơn bắt đầu hôm nay/ngày mai).
    order.receivedAt = earliest(at(addDays(order.startDate, -2), '08:00'), addMinutes(stamp, -3))
    order.scheduledAt = earliest(at(addDays(order.startDate, -1), '09:00'), addMinutes(stamp, -2))
    order.confirmedAt = confirmed
      ? earliest(at(addDays(order.startDate, -1), '09:30'), addMinutes(stamp, -1))
      : null
    order.updatedAt = order.confirmedAt ?? order.scheduledAt

    for (const line of scenario.lines) {
      line.plan.forEach((planned, index) => {
        // Ô bằng 0 là dây chuyền nghỉ ngày đó: không có dòng plan (CR-001 §6.6b).
        if (planned <= 0) return
        plans.push({
          id: uuidv7(order.scheduledAt),
          line: line.code,
          date: dateFromOffset(scenario.startOffset + index),
          initial: planned,
          planned,
          updatedAt: order.scheduledAt,
        })
      })
    }
  }

  const planAt = (lineCode, date) => plans.find((plan) => plan.line === lineCode && plan.date === date)

  // --- Ghi nhận sản lượng ---
  const days = [] // { id, line, date, entries: [{ id, quantity, recordedAt }], closedAt, createdAt, updatedAt }
  const dayAt = (lineCode, date) => days.find((day) => day.line === lineCode && day.date === date)

  for (const cell of scenario.cells) {
    const date = dateFromOffset(cell.dayOffset)
    if (!confirmed) fail(`Tiến độ chưa chốt nên không có sản lượng (ô ${cell.line} ngày ${date}).`)
    if (date > today) fail(`Ngày ${date} chưa tới nên chưa ghi nhận được.`)
    const plan = planAt(cell.line, date)
    if (!plan) fail(`Ô ${cell.line} ngày ${date} không có kế hoạch nên không ghi nhận được.`)

    // Ô chỉ khai báo Xuất hàng thì chưa có dòng ngày nào; lúc chốt sổ mới sinh ra (với 0).
    const entries = cell.entries ?? []
    if (entries.length === 0) continue

    const day = dayAt(cell.line, date) ?? { id: uuidv7(), line: cell.line, date, entries: [], closedAt: null }
    if (!days.includes(day)) days.push(day)

    // Trần ghi nhận của ô và của cả đơn, như API (ENTRY_EXCEEDS_DAILY_PLAN, ACTUAL_EXCEEDS_ORDER_QUANTITY).
    if (sum(day.entries.map((entry) => entry.quantity)) + sum(entries) > plan.planned) {
      fail(`Ô ${cell.line} ngày ${date} ghi nhận vượt kế hoạch ${plan.planned}.`)
    }

    entries.forEach((entryQuantity, index) => {
      if (!(entryQuantity > 0)) fail(`Lần ghi nhận ở ô ${cell.line} ngày ${date} phải lớn hơn 0.`)
      // Ngày đã qua: rải trong giờ làm việc của ngày đó. Hôm nay: cách nhau 10 phút, kết thúc trước mốc.
      const recordedAt =
        date < today
          ? at(date, `${String(8 + index * 2).padStart(2, '0')}:00`)
          : earliest(
              at(date, `${String(8 + index * 2).padStart(2, '0')}:00`),
              addMinutes(stamp, -10 * (entries.length - index) - 1),
            )
      day.entries.push({ id: uuidv7(recordedAt), quantity: entryQuantity, recordedAt })
    })

    if (sum(days.flatMap((d) => d.entries.map((entry) => entry.quantity))) > order.quantity) {
      fail(`Tổng sản lượng vượt số lượng đơn ${order.quantity}.`)
    }
  }

  // --- Xuất hàng: chốt sổ cả NGÀY, mọi ô có kế hoạch của ngày đó (CR-01 §6.6) ---
  const closedDates = [...new Set(scenario.cells.filter((cell) => cell.close).map((cell) => dateFromOffset(cell.dayOffset)))]
  for (const date of closedDates.sort()) {
    const closedAt = date < today ? at(date, '17:00') : addMinutes(stamp, -1)

    for (const plan of plans.filter((candidate) => candidate.date === date)) {
      let day = dayAt(plan.line, date)
      if (!day) {
        // Ô chưa ghi nhận lần nào chốt với sản lượng 0 (CR-01 §14.4).
        day = { id: uuidv7(closedAt), line: plan.line, date, entries: [], closedAt: null }
        days.push(day)
      }
      day.closedAt = closedAt
    }

    // Trạng thái đơn chỉ được đánh giá lúc Xuất hàng (CR-01 OV-4).
    const totalActual = sum(days.flatMap((day) => day.entries.map((entry) => entry.quantity)))
    order.status = totalActual >= order.quantity ? 'Completed' : 'Incomplete'
    order.updatedAt = latest(order.updatedAt, closedAt)
  }

  const actualOf = (day) => sum(day.entries.map((entry) => entry.quantity))
  const closedDatesOn = (lineCode) => days.filter((day) => day.line === lineCode && day.closedAt).map((day) => day.date)

  // --- Bù sản lượng thiếu ---
  const adjustments = [] // { id, source, shortage, type, status, at, reversedAt, items: [{ id, plan, addOn }] }

  scenario.adjustments.forEach((adjustment, index) => {
    const sourceDate = dateFromOffset(adjustment.sourceDayOffset)
    const source = planAt(adjustment.line, sourceDate)
    const sourceDay = dayAt(adjustment.line, sourceDate)

    if (!source) fail(`Không có ô nguồn ${adjustment.line} ngày ${sourceDate}.`)
    if (!sourceDay?.closedAt) fail(`Ô ${adjustment.line} ngày ${sourceDate} chưa Xuất hàng nên chưa có thiếu để bù.`)
    if (order.status === 'Completed') fail('Đơn đã hoàn thành nên không bù thêm.')
    if (adjustments.some((other) => other.source === source && other.status === 'Applied')) {
      fail(`Ô ${adjustment.line} ngày ${sourceDate} đã có một lần bù đang áp dụng.`)
    }

    const shortage = source.planned - actualOf(sourceDay)
    if (shortage <= 0) fail(`Ô ${adjustment.line} ngày ${sourceDate} không thiếu.`)

    // Ngày nhận bù: cùng dây chuyền, sau ngày thiếu, chưa qua, chưa Xuất hàng (AdjustmentRules).
    const closed = closedDatesOn(adjustment.line)
    const candidates = plans
      .filter(
        (plan) =>
          plan.line === adjustment.line &&
          plan !== source &&
          plan.date > sourceDate &&
          plan.date >= today &&
          !closed.includes(plan.date),
      )
      .sort((a, b) => a.date.localeCompare(b.date))

    let items
    if (adjustment.type === 'Automatic') {
      if (candidates.length === 0) fail(`Ô ${adjustment.line} ngày ${sourceDate} không còn ngày nào nhận bù.`)
      const shares = splitEvenly(shortage, candidates.length)
      items = candidates.map((plan, i) => ({ plan, addOn: shares[i] })).filter((item) => item.addOn > 0)
    } else {
      // Option 1 luôn bù trọn phần thiếu vào ngày được chọn.
      const targetDate = dateFromOffset(adjustment.targetDayOffset)
      const target = candidates.find((plan) => plan.date === targetDate)
      if (!target) fail(`Ngày ${targetDate} không nhận bù được cho ô ${adjustment.line} ngày ${sourceDate}.`)
      items = [{ plan: target, addOn: shortage }]
    }

    const appliedAt = earliest(addMinutes(sourceDay.closedAt, 30 + index * 10), addMinutes(stamp, -1))
    for (const item of items) {
      item.plan.planned += item.addOn
      item.plan.updatedAt = latest(item.plan.updatedAt, appliedAt)
    }

    const record = {
      id: uuidv7(appliedAt),
      source,
      shortage,
      type: adjustment.type,
      status: 'Applied',
      at: appliedAt,
      reversedAt: null,
      items: items.map((item) => ({ id: uuidv7(appliedAt), ...item })),
    }

    if (adjustment.thenReverse) {
      record.status = 'Reversed'
      record.reversedAt = addMinutes(appliedAt, 5)
      for (const item of items) {
        item.plan.planned -= item.addOn
        item.plan.updatedAt = latest(item.plan.updatedAt, record.reversedAt)
      }
    }

    adjustments.push(record)
  })

  // --- Câu lệnh SQL, theo thứ tự khoá ngoại ---
  const statements = [
    insert('orders', {
      id: order.id,
      shoe_code: scenario.shoeCode,
      quantity: order.quantity,
      start_date: order.startDate,
      due_date: order.dueDate,
      status: order.status,
      schedule_confirmed_at: order.confirmedAt,
      created_at: order.receivedAt,
      updated_at: order.updatedAt,
    }),
  ]

  for (const line of scenario.lines) {
    statements.push(
      insert('order_production_lines', {
        order_id: order.id,
        production_line_id: lineRef(line.code),
        allocated_quantity: sum(line.plan),
        created_at: order.scheduledAt,
      }),
    )
  }

  for (const plan of plans) {
    statements.push(
      insert('production_plans', {
        id: plan.id,
        order_id: order.id,
        production_line_id: lineRef(plan.line),
        production_date: plan.date,
        initial_planned_quantity: plan.initial,
        planned_quantity: plan.planned,
        created_at: order.scheduledAt,
        updated_at: plan.updatedAt,
      }),
    )
  }

  for (const day of days) {
    const firstEntry = day.entries[0]?.recordedAt ?? day.closedAt
    const lastChange = day.closedAt ?? day.entries.at(-1).recordedAt
    statements.push(
      insert('production_days', {
        id: day.id,
        order_id: order.id,
        production_line_id: lineRef(day.line),
        production_date: day.date,
        status: day.closedAt ? 'Closed' : 'Open',
        actual_quantity: day.closedAt ? actualOf(day) : null,
        closed_at: day.closedAt,
        closed_by: day.closedAt ? USER : null,
        created_by: USER,
        updated_by: USER,
        created_at: firstEntry,
        updated_at: lastChange,
      }),
    )

    for (const entry of day.entries) {
      statements.push(
        insert('production_entries', {
          id: entry.id,
          production_day_id: day.id,
          quantity: entry.quantity,
          recorded_at: entry.recordedAt,
          created_by: USER,
          updated_by: USER,
          created_at: entry.recordedAt,
          updated_at: entry.recordedAt,
        }),
        insert('production_entry_logs', {
          id: uuidv7(entry.recordedAt),
          production_entry_id: entry.id,
          action: 'Create',
          new_quantity: entry.quantity,
          changed_by: USER,
          changed_at: entry.recordedAt,
        }),
      )
    }
  }

  for (const adjustment of adjustments) {
    statements.push(
      insert('plan_adjustments', {
        id: adjustment.id,
        source_production_plan_id: adjustment.source.id,
        shortage_quantity: adjustment.shortage,
        adjustment_type: adjustment.type,
        status: adjustment.status,
        created_by: USER,
        applied_by: USER,
        reversed_by: adjustment.reversedAt ? USER : null,
        created_at: adjustment.at,
        applied_at: adjustment.at,
        reversed_at: adjustment.reversedAt,
      }),
      ...adjustment.items.map((item) =>
        insert('plan_adjustment_items', {
          id: item.id,
          plan_adjustment_id: adjustment.id,
          production_plan_id: item.plan.id,
          add_on_quantity: item.addOn,
        }),
      ),
    )
  }

  const totalActual = sum(days.flatMap((day) => day.entries.map((entry) => entry.quantity)))
  const summary =
    `${order.status === 'Pending' ? 'chưa lập tiến độ' : confirmed ? 'đã chốt' : 'chờ chốt'}` +
    ` · ${order.status}` +
    (scheduled ? ` · ${order.startDate} → ${order.dueDate}` : '') +
    ` · thực tế ${totalActual}/${order.quantity}` +
    (adjustments.length > 0
      ? ` · bù ${adjustments
          .map((a) => `${a.type === 'Automatic' ? 'chia đều' : 'dồn 1 ngày'} ${a.items.map((i) => `+${i.addOn}`).join('/')}${a.status === 'Reversed' ? ' (đã hoàn tác)' : ''}`)
          .join(', ')}`
      : '')

  return { statements, summary, status: order.status, confirmed }
}

// --- Chạy ---------------------------------------------------------------------------------------

function runSql(sql) {
  const result = spawnSync(
    'docker',
    ['exec', '-i', CONTAINER, 'psql', '-X', '-q', '-v', 'ON_ERROR_STOP=1', '-U', DB_USER, '-d', DATABASE],
    { input: sql, encoding: 'utf8' },
  )

  if (result.error) throw new Error(`Không chạy được docker: ${result.error.message}`)
  if (result.status !== 0) {
    throw new Error(
      `PostgreSQL báo lỗi (container ${CONTAINER}, database ${DATABASE}):\n${result.stderr.trim()}\n` +
        `  App phải đang chạy và đã khởi động ít nhất một lần:\n    ${RUN_APP}`,
    )
  }
}

async function main() {
  console.log(`\n${colors.cyan}Nạp dữ liệu mẫu — Quản lý sản xuất${colors.reset}`)
  console.log(`${colors.dim}Ghi thẳng vào ${CONTAINER}/${DATABASE} · hôm nay: ${today} (${TIME_ZONE})${colors.reset}\n`)

  const { orders: allScenarios, productionLines } = JSON.parse(
    await readFile(join(here, 'scenarios.json'), 'utf8'),
  )

  const requested = process.argv.slice(2)
  const unknown = requested.filter((code) => !allScenarios.some((scenario) => scenario.shoeCode === code))
  if (unknown.length > 0) {
    log.fail(`Không có kịch bản: ${unknown.join(', ')}`)
    log.info(`Các kịch bản: ${allScenarios.map((scenario) => scenario.shoeCode).join(', ')}`)
    process.exitCode = 1
    return
  }

  const scenarios = requested.length > 0
    ? allScenarios.filter((scenario) => requested.includes(scenario.shoeCode))
    : allScenarios

  const knownLines = new Set(productionLines.map((line) => line.code))

  // Dựng hết trước khi ghi: một kịch bản sai thì không ghi gì cả, database giữ nguyên.
  const built = []
  for (const [index, scenario] of scenarios.entries()) {
    log.step(`${scenario.shoeCode} — ${scenario.label}`)
    try {
      const stamp = addMinutes(now, -5 * (scenarios.length - index))
      const result = buildScenario(scenario, stamp, knownLines)
      built.push(result)
      log.ok(result.summary)
    } catch (error) {
      log.fail(error.message)
      process.exitCode = 1
      return
    }
  }

  const sql = [
    'BEGIN;',
    `CREATE TEMP TABLE seed_user ON COMMIT DROP AS SELECT id FROM users WHERE username = ${literal(USERNAME)};`,
    `DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM seed_user) THEN ` +
      `RAISE EXCEPTION 'Chưa có tài khoản ${USERNAME.replaceAll("'", "''")} — khởi động api một lần để backend tạo tài khoản.'; ` +
      'END IF; END $$;',

    // Xoá dữ liệu nghiệp vụ cũ theo thứ tự khoá ngoại.
    'DELETE FROM plan_adjustment_items;',
    'DELETE FROM plan_adjustments;',
    'DELETE FROM production_entry_logs;',
    'DELETE FROM production_entries;',
    'DELETE FROM production_days;',
    'DELETE FROM production_plans;',
    'DELETE FROM order_production_lines;',
    'DELETE FROM orders;',

    // Dây chuyền mẫu: tạo nếu chưa có, bật lại nếu đang ngừng — kịch bản cần chúng hoạt động.
    ...productionLines.map(
      (line, index) =>
        `INSERT INTO production_lines (id, code, name, status, sort_order, created_at, updated_at) ` +
        `VALUES (${literal(uuidv7())}, ${literal(line.code)}, ${literal(line.name)}, 'Active', ${index + 1}, ` +
        `${literal(now)}, ${literal(now)}) ` +
        `ON CONFLICT (code) DO UPDATE SET status = 'Active', updated_at = EXCLUDED.updated_at ` +
        `WHERE production_lines.status <> 'Active';`,
    ),

    ...built.flatMap((result) => result.statements),
    'COMMIT;',
  ].join('\n')

  try {
    runSql(sql)
  } catch (error) {
    console.log()
    log.fail(error.message)
    process.exitCode = 1
    return
  }

  const count = (predicate) => built.filter(predicate).length
  console.log(`\n${colors.green}Xong.${colors.reset}`)
  console.log(
    `${colors.dim}${built.length} đơn · ${count((b) => b.status === 'Pending')} chưa lập tiến độ · ` +
      `${count((b) => b.status !== 'Pending' && !b.confirmed)} chờ chốt · ` +
      `${count((b) => b.status === 'Incomplete' && b.confirmed)} đã chốt chưa xong · ` +
      `${count((b) => b.status === 'Completed')} hoàn thành${colors.reset}`,
  )
  console.log('\nẢnh mẫu của các đơn cũ (nếu có) vẫn còn trong volume order-images, không ảnh hưởng gì.')
  console.log(`Mở http://localhost:5173 để kiểm tra.\n`)
}

await main()
