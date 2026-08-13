import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../../api/errors'
import { queryKeys } from '../../../app/config/queryKeys'
import { authApi } from '../api/authApi'
import type { LoginRequest } from '../types'

/** The current user is server state owned by TanStack Query (Step 5 §10). */
export function useCurrentUser() {
  return useQuery({
    queryKey: queryKeys.currentUser,
    queryFn: ({ signal }) => authApi.me(signal),
    retry: (failureCount, error) => {
      // Not being logged in is an expected answer, not a failure worth retrying.
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
      // Everything cached belongs to the session that just ended.
      queryClient.clear()
    },
  })
}
