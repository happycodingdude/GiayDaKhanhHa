import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'
import { formatPercent } from '../lib/format'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  loading?: boolean
}

export function Button({
  variant = 'secondary',
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
