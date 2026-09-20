import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, Ref } from 'react'
import { formatDate } from '../lib/date'
import { formatPercent } from '../lib/format'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /**
   * Mọi nút hành động đều là 'primary' — nền xanh, chữ trắng — nên đó là mặc định. Một nút mới
   * thêm sau này tự đồng bộ theo, thay vì trông chờ mỗi call site nhớ truyền variant.
   */
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  loading?: boolean
  /** React 19 truyền ref như một prop thường; khai báo ở đây để bên gọi lấy được nút thật. */
  ref?: Ref<HTMLButtonElement>
}

export function Button({
  variant = 'primary',
  loading = false,
  disabled,
  children,
  className = '',
  ...rest
}: ButtonProps) {
  return (
    <button
      className={`btn btn--${variant} ${className}`}
      disabled={disabled || loading}
      {...rest}
    >
      {loading && <span className="btn__spinner" aria-hidden="true" />}
      {children}
    </button>
  )
}

export function Card({
  title,
  description,
  actions,
  children,
  className = '',
}: {
  title?: ReactNode
  description?: ReactNode
  actions?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`card ${className}`}>
      {(title || actions) && (
        <header className="card__header">
          <div>
            {title && <h2 className="card__title">{title}</h2>}
            {description && <p className="card__description">{description}</p>}
          </div>
          {actions && <div className="card__actions">{actions}</div>}
        </header>
      )}
      <div className="card__body">{children}</div>
    </section>
  )
}

export type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info'

export function Badge({ tone = 'neutral', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={`badge badge--${tone}`}>{children}</span>
}

export function ProgressBar({ value, tone = 'info' }: { value: number; tone?: BadgeTone }) {
  const clamped = Math.max(0, Math.min(100, value))
  return (
    <div
      className="progress"
      role="progressbar"
      aria-valuenow={clamped}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={`Tiến độ ${formatPercent(value)}`}
    >
      <div className={`progress__fill progress__fill--${tone}`} style={{ width: `${clamped}%` }} />
    </div>
  )
}

export function Field({
  label,
  htmlFor,
  error,
  hint,
  required,
  children,
}: {
  label: string
  htmlFor?: string
  error?: string
  hint?: ReactNode
  required?: boolean
  children: ReactNode
}) {
  return (
    <div className={`field ${error ? 'field--invalid' : ''}`}>
      <label className="field__label" htmlFor={htmlFor}>
        {label}
        {required && <span className="field__required"> *</span>}
      </label>
      {children}
      {hint && !error && <p className="field__hint">{hint}</p>}
      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input className="input" {...props} />
}

/**
 * Ô chọn ngày. Vẫn là <input type="date"> để dùng lịch sẵn có của trình duyệt, nhưng phần chữ do ta
 * vẽ đè lên: Chrome hiển thị ngày theo locale của trình duyệt (mm/dd/yyyy trên máy en-US) chứ không
 * theo app, còn ở đây ngày luôn phải đọc là dd/mm/yyyy. Bấm vào bất kỳ đâu trong ô cũng mở lịch,
 * không phải nhắm đúng icon.
 */
export function DateInput({
  value,
  className = '',
  ...rest
}: Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'value'> & { value: string }) {
  return (
    <span className="date-input">
      <input
        className={`input date-input__control ${className}`}
        type="date"
        value={value}
        onClick={(event) => {
          // Trình duyệt cũ không có showPicker; khi đó vẫn còn icon lịch mặc định để bấm.
          try {
            event.currentTarget.showPicker()
          } catch {
            /* không mở được thì để trình duyệt xử lý như thường */
          }
        }}
        {...rest}
      />
      {/* Trình đọc màn hình đọc giá trị của chính input, nên bản vẽ đè này ẩn với nó. */}
      <span className="date-input__text" aria-hidden="true">
        {value ? formatDate(value) : <span className="date-input__placeholder">dd/mm/yyyy</span>}
      </span>
    </span>
  )
}

export function StatTile({
  label,
  value,
  hint,
  tone = 'neutral',
}: {
  label: string
  value: ReactNode
  hint?: ReactNode
  tone?: BadgeTone
}) {
  return (
    <div className={`stat stat--${tone}`}>
      <p className="stat__label">{label}</p>
      <p className="stat__value">{value}</p>
      {hint && <p className="stat__hint">{hint}</p>}
    </div>
  )
}
