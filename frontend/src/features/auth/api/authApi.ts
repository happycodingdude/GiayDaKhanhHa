import { apiClient } from '../../../api/client'
import type { CurrentUserDto, LoginRequest } from '../types'

export const authApi = {
  /** The server sets the HttpOnly cookie; no token is returned to JavaScript. */
  login: (request: LoginRequest) => apiClient.post<CurrentUserDto>('/auth/login', request),

  logout: () => apiClient.post<void>('/auth/logout'),

  me: (signal?: AbortSignal) => apiClient.get<CurrentUserDto>('/auth/me', { signal }),
}
