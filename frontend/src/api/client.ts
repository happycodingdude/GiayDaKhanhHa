import { ApiError, NetworkError, type ApiErrorBody } from './errors'

const BASE_URL = '/api/v1'

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

interface RequestOptions {
  signal?: AbortSignal
  query?: Record<string, string | number | undefined | null>
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const url = `${BASE_URL}${path}`
  if (!query) return url

  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params.append(key, String(value))
    }
  }

  const queryString = params.toString()
  return queryString ? `${url}?${queryString}` : url
}

async function request<T>(
  method: Method,
  path: string,
  body?: unknown,
  options?: RequestOptions,
): Promise<T> {
  let response: Response

  try {
    response = await fetch(buildUrl(path, options?.query), {
      method,
      // Cookie xác thực là HttpOnly và first-party; không đọc gì từ storage.
      credentials: 'same-origin',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: options?.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new NetworkError()
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  const payload = text ? (JSON.parse(text) as unknown) : null

  if (!response.ok) {
    const fallback: ApiErrorBody = {
      code: 'INTERNAL_ERROR',
      message: 'Đã xảy ra lỗi không mong muốn.',
      details: null,
    }
    throw new ApiError(response.status, (payload as ApiErrorBody | null) ?? fallback)
  }

  return payload as T
}

/**
 * Điểm vào HTTP duy nhất. Component không bao giờ gọi fetch trực tiếp; luôn đi qua hook của
 * feature và module API của feature (Step 5 §11).
 */
export const apiClient = {
  /**
   * Gửi multipart/form-data. Chủ đích không đi qua `request`: body ở đây là FormData, và tuyệt đối
   * không được đặt Content-Type thủ công — trình duyệt phải tự sinh boundary, đặt tay vào là
   * server không parse được form.
   */
  upload: async <T>(method: 'POST' | 'PUT', path: string, form: FormData): Promise<T> => {
    let response: Response

    try {
      response = await fetch(`${BASE_URL}${path}`, {
        method,
        credentials: 'same-origin',
        body: form,
      })
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') throw error
      throw new NetworkError()
    }

    const text = await response.text()
    const payload = text ? (JSON.parse(text) as unknown) : null

    if (!response.ok) {
      const fallback: ApiErrorBody = {
        code: 'INTERNAL_ERROR',
        message: 'Đã xảy ra lỗi không mong muốn.',
        details: null,
      }
      throw new ApiError(response.status, (payload as ApiErrorBody | null) ?? fallback)
    }

    return payload as T
  },

  get: <T>(path: string, options?: RequestOptions) => request<T>('GET', path, undefined, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('POST', path, body, options),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('PUT', path, body, options),
  patch: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('PATCH', path, body, options),
  delete: <T>(path: string, options?: RequestOptions) =>
    request<T>('DELETE', path, undefined, options),
}
