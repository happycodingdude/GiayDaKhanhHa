import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { settingsApi } from '../api/settingsApi'
import type { UpdateSystemSettingsRequest } from '../types'

export function useSettings() {
  return useQuery({
    queryKey: queryKeys.settings,
    queryFn: ({ signal }) => settingsApi.get(signal),
    // Cấu hình gần như không đổi, nhưng màn hình nhập sản lượng đọc nó mỗi lần mở.
    staleTime: 5 * 60 * 1000,
  })
}

export function useUpdateSettings() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpdateSystemSettingsRequest) => settingsApi.update(request),
    onSuccess: (settings) => queryClient.setQueryData(queryKeys.settings, settings),
  })
}
