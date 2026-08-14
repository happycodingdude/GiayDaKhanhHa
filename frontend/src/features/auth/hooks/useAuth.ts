import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../../api/errors'
import { queryKeys } from '../../../app/config/queryKeys'
import { authApi } from '../api/authApi'
import type { LoginRequest } from '../types'

/** Người dùng hiện tại là server state do TanStack Query quản lý (Step 5 §10). */
export function useCurrentUser() {
  return useQuery({
    queryKey: queryKeys.currentUser,
    queryFn: ({ signal }) => authApi.me(signal),
    retry: (failureCount, error) => {
      // Chưa đăng nhập là một câu trả lời hợp lệ, không phải lỗi đáng retry.
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) return false
      return failureCount < 2
    },
    staleTime: 60_000,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: LoginRequest) => authApi.login(request),
    onSuccess: (user) => {
      queryClient.setQueryData(queryKeys.currentUser, user)
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: () => {
      // Mọi thứ đang nằm trong cache đều thuộc về phiên vừa kết thúc.
      queryClient.clear()
    },
  })
}
