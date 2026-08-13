const numberFormat = new Intl.NumberFormat('vi-VN')

/** 1000 -> "1.000" */
export function formatNumber(value: number): string {
  return numberFormat.format(value)
}

/** A quantity that has not been entered renders as an em dash, never as 0. */
export function formatQuantity(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : numberFormat.format(value)
}

/** Signed difference: +10 / -10 / 0. */
export function formatDifference(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  if (value > 0) return `+${numberFormat.format(value)}`
  return numberFormat.format(value)
}

export function formatPercent(value: number): string {
  return `${Number.isInteger(value) ? value : value.toFixed(1)}%`
}
