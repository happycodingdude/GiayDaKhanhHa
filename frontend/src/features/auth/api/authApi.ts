import { apiClient } from '../../../api/client'
import type { CurrentUserDto, LoginRequest } from '../types'

export const authApi = {
  /** Server set cookie HttpOnly; không trả token nào về cho JavaScript. */
  login: (request: LoginRequest) => apiClient.post<CurrentUserDto>('/auth/login', request),

  logout: () => apiClient.post<void>('/auth/logout'),

  me: (signal?: AbortSignal) => apiClient.get<CurrentUserDto>('/auth/me', { signal }),
}
