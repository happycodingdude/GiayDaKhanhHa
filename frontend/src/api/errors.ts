/** Chi tiết theo từng field trong response VALIDATION_ERROR. */
export interface ApiValidationDetail {
  field: string
  code: string
  message: string
}

/** Hợp đồng lỗi của backend (Step 4 §4). */
export interface ApiErrorBody {
  code: string
  message: string
  details: ApiValidationDetail[] | null
}

export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly details: ApiValidationDetail[] | null

  constructor(status: number, body: ApiErrorBody) {
    super(body.message)
    this.name = 'ApiError'
    this.status = status
    this.code = body.code
    this.details = body.details ?? null
  }
}

/** Ném ra khi request không tới được server. */
export class NetworkError extends Error {
  constructor() {
    super('Không thể kết nối tới máy chủ. Vui lòng kiểm tra kết nối mạng và thử lại.')
    this.name = 'NetworkError'
  }
}

/**
 * Ánh xạ mã lỗi nghiệp vụ sang câu chữ mà quản lý cần đọc. Chi tiết exception kỹ thuật không
 * bao giờ bị lộ ra ngoài (Step 5 §12).
 */
const MESSAGES: Record<string, string> = {
  INVALID_CREDENTIALS: 'Tên đăng nhập hoặc mật khẩu không đúng.',
  USER_INACTIVE: 'Tài khoản đã bị vô hiệu hoá.',
  NOT_AUTHENTICATED: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',

  ORDER_NOT_FOUND: 'Không tìm thấy đơn hàng.',
  ORDER_CODE_ALREADY_EXISTS: 'Mã đơn hàng đã tồn tại. Vui lòng chọn mã khác.',
  ORDER_OVERDUE: 'Đơn hàng đã quá hạn hoàn thành nên chỉ được xem, không thể thay đổi dữ liệu.',
  INITIAL_PLAN_TOTAL_MISMATCH: 'Tổng kế hoạch phải bằng tổng số lượng đơn hàng.',

  PRODUCTION_PLAN_NOT_FOUND: 'Không tìm thấy kế hoạch sản xuất.',
  PRODUCTION_ENTRY_NOT_FOUND: 'Không tìm thấy lần ghi nhận sản lượng này.',
  DAY_HAS_NO_PLAN:
    'Ngày này không có kế hoạch sản xuất nên không thể ghi nhận sản lượng hay xuất hàng.',
  DAY_ALREADY_CLOSED:
    'Ngày này đã xuất hàng nên số liệu đã được chốt sổ và không thể thay đổi.',
  FUTURE_DATE_NOT_ALLOWED: 'Ngày này chưa tới nên chưa thể ghi nhận sản lượng.',
  ENTRY_EXCEEDS_DAILY_PLAN:
    'Số lượng vượt quá phần còn được nhập của ngày. Vui lòng kiểm tra lại.',
  ACTUAL_EXCEEDS_ORDER_QUANTITY:
    'Số lượng hoàn thành không thể vượt quá số lượng của đơn hàng.',
  ORDER_ALREADY_COMPLETED:
    'Đơn hàng đã hoàn thành nên không thể ghi nhận thêm sản lượng.',

  ADJUSTMENT_NOT_FOUND: 'Không tìm thấy lần bù sản lượng.',
  ADJUSTMENT_OUTDATED:
    'Dữ liệu sản xuất đã thay đổi. Vui lòng xem lại đề xuất bù sản lượng mới.',
  ACTIVE_ADJUSTMENT_EXISTS:
    'Ngày này đã có một lần bù đang áp dụng. Hãy hoàn tác lần bù đó trước.',
  ADJUSTMENT_NOT_APPLIED: 'Lần bù này không còn ở trạng thái đang áp dụng.',
  NO_SHORTAGE: 'Ngày này không có sản lượng thiếu cần xử lý.',
  INVALID_ADJUSTMENT_TARGET: 'Ngày nhận bù không hợp lệ.',
  DUPLICATE_ADJUSTMENT_TARGET: 'Một ngày chỉ được chọn một lần trong cùng một lần bù.',
  ADJUSTMENT_TOTAL_MISMATCH: 'Tổng số lượng bù phải bằng đúng số lượng thiếu.',
  NO_ELIGIBLE_TARGET_DAY: 'Không còn ngày sản xuất nào có thể nhận phần bù.',
  SOURCE_DAY_NOT_CLOSED:
    'Ngày này chưa xuất hàng nên chưa có số lượng thiếu chính thức để xử lý.',
  TARGET_DAY_CLOSED: 'Ngày nhận bù đã xuất hàng nên không thể nhận thêm kế hoạch.',
  TARGET_DATE_IN_PAST: 'Không thể bù vào một ngày đã qua.',

  VALIDATION_ERROR: 'Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra lại.',
  INTERNAL_ERROR: 'Đã xảy ra lỗi không mong muốn. Vui lòng thử lại.',
}

/**
 * Các lỗi vượt trần kèm theo con số mà server áp đặt, để thông báo nói đúng "còn được nhập bao
 * nhiêu" thay vì một câu chung chung (CR-01 §6.4).
 */
const MAX_ALLOWED_MESSAGES: Record<string, (maximum: string) => string> = {
  ENTRY_EXCEEDS_DAILY_PLAN: (maximum) =>
    `Vượt quá phần còn được nhập của ngày. Tối đa còn ${maximum} đôi.`,
  ACTUAL_EXCEEDS_ORDER_QUANTITY: (maximum) =>
    `Vượt quá số lượng còn lại của đơn hàng. Tối đa còn ${maximum} đôi.`,
}

/** Chuyển mọi giá trị được ném ra thành thông báo an toàn và hữu ích cho quản lý. */
export function toUserMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const withMaximum = MAX_ALLOWED_MESSAGES[error.code]
    const maximum = error.details?.find((detail) => detail.code === 'MAX_ALLOWED')?.message

    if (withMaximum && maximum !== undefined) {
      return withMaximum(maximum)
    }

    return MESSAGES[error.code] ?? error.message
  }

  if (error instanceof NetworkError) {
    return error.message
  }

  return MESSAGES.INTERNAL_ERROR
}

export function isApiError(error: unknown, code: string): boolean {
  return error instanceof ApiError && error.code === code
}
