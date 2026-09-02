import { apiClient } from '../../../api/client'
import type { SystemSettingsDto, UpdateSystemSettingsRequest } from '../types'

export const settingsApi = {
  get: (signal?: AbortSignal) => apiClient.get<SystemSettingsDto>('/settings', { signal }),

  update: (request: UpdateSystemSettingsRequest) =>
    apiClient.put<SystemSettingsDto>('/settings', request),
}
