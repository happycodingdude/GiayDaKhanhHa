import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'
import { useState } from 'react'
import { ApiError } from '../../api/errors'
import { ToastProvider } from '../../shared/feedback/ToastProvider'
import { createAppRouter } from '../router/router'

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // An authentication or business error will not resolve by retrying.
        retry: (failureCount, error) => {
          if (error instanceof ApiError && error.status < 500) return false
          return failureCount < 2
        },
        staleTime: 15_000,
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: false,
      },
    },
  })
}

export function AppProviders() {
  const [queryClient] = useState(createQueryClient)
  const [router] = useState(() => createAppRouter(queryClient))

  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <RouterProvider router={router} />
      </ToastProvider>
    </QueryClientProvider>
  )
}
