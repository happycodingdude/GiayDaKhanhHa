import type { ReactNode } from 'react'
import { toUserMessage } from '../../api/errors'
import { Button } from '../components/ui'

/** Mọi màn hình lấy dữ liệu từ server đều render trạng thái loading, lỗi và rỗng (Step 5 §30). */

export function LoadingState({ label = 'Đang tải dữ liệu…' }: { label?: string }) {
  return (
    <div className="state" role="status">
      <span className="state__spinner" aria-hidden="true" />
      <p className="state__text">{label}</p>
    </div>
  )
}

export function ErrorState({
  error,
  onRetry,
  title = 'Không tải được dữ liệu',
}: {
  error: unknown
  onRetry?: () => void
  title?: string
}) {
  return (
    <div className="state state--error" role="alert">
      <p className="state__title">{title}</p>
      <p className="state__text">{toUserMessage(error)}</p>
      {onRetry && (
        <Button variant="primary" onClick={onRetry}>
          Thử lại
        </Button>
      )}
    </div>
  )
}

export function EmptyState({
  icon = '📦',
  title,
  description,
  action,
}: {
  icon?: ReactNode
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="state state--empty">
      <p className="state__icon" aria-hidden="true">
        {icon}
      </p>
      <p className="state__title">{title}</p>
      {description && <p className="state__text">{description}</p>}
      {action}
    </div>
  )
}

export function InlineError({ message }: { message: string }) {
  return (
    <p className="inline-error" role="alert">
      {message}
    </p>
  )
}
