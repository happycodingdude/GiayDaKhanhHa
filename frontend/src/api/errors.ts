/** Field-level detail from a VALIDATION_ERROR response. */
export interface ApiValidationDetail {
  field: string
  code: string
  message: string
}

/** The backend error contract (Step 4 §4). */
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

/** Raised when the request never reached the server. */
export class NetworkError extends Error {
  constructor() {
    super('Không thể kết nối tới máy chủ. Vui lòng kiểm tra kết nối mạng và thử lại.')
    this.name = 'NetworkError'
  }
}

/**
 * Business error codes mapped to the wording the manager should see. Technical exception detail is
 * never surfaced (Step 5 §12).
 */
const MESSAGES: Record<string, string> = {
  INVALID_CREDENTIALS: 'Tên đăng nhập hoặc mật khẩu không đúng.',
  USER_INACTIVE: 'Tài khoản đã bị vô hiệu hoá.',
  NOT_AUTHENTICATED: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',

  ORDER_NOT_FOUND: 'Không tìm thấy đơn hàng.',
  ORDER_CODE_ALREADY_EXISTS: 'Mã đơn hàng đã tồn tại. Vui lòng chọn mã khác.',
  INITIAL_PLAN_TOTAL_MISMATCH: 'Tổng kế hoạch phải bằng tổng số lượng đơn hàng.',

  PRODUCTION_PLAN_NOT_FOUND: 'Không tìm thấy kế hoạch sản xuất.',
  PRODUCTION_RECORD_NOT_FOUND: 'Không tìm thấy bản ghi sản lượng.',
  PRODUCTION_RECORD_ALREADY_EXISTS:
    'Ngày này đã có sản lượng. Vui lòng sửa sản lượng đã nhập thay vì tạo mới.',
  NO_PRODUCTION_PLAN_FOR_DATE: 'Ngày này không có kế hoạch sản xuất.',
  PLAN_QUANTITY_IS_ZERO:
    'Ngày này không có kế hoạch sản xuất. Không thể nhập sản lượng thực tế.',
  ACTUAL_EXCEEDS_ORDER_QUANTITY:
    'Số lượng hoàn thành không thể vượt quá số lượng của đơn hàng.',

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
  NO_ELIGIBLE_TARGET_PLANS: 'Không còn ngày sản xuất nào có thể nhận phần bù.',

  VALIDATION_ERROR: 'Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra lại.',
  INTERNAL_ERROR: 'Đã xảy ra lỗi không mong muốn. Vui lòng thử lại.',
}

/** Turns any thrown value into a message that is safe and useful to show the manager. */
export function toUserMessage(error: unknown): string {
  if (error instanceof ApiError) {
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
