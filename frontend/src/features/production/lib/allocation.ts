/**
 * Quy tắc chia đều của hệ thống, bản frontend (CR-001 §6.6b, BR-N17).
 *
 * Phải cho ra ĐÚNG dãy số mà backend cho ra — cùng công thức, cùng cách dồn dư vào các phần tử đầu.
 * Lệch một đơn vị ở đây nghĩa là quản lý bấm "Chia đều" xong lại thấy tổng không khớp, và không có
 * cách nào hiểu vì sao.
 */
export function splitEvenly(total: number, count: number): number[] {
  if (count <= 0) return []

  const base = Math.floor(total / count)
  const remainder = total % count

  return Array.from({ length: count }, (_, index) => base + (index < remainder ? 1 : 0))
}

/** Khoá của một ô trong state của ma trận nhập liệu. */
export function cellKey(lineId: string, date: string): string {
  return `${lineId}|${date}`
}

/** Chuỗi rỗng đọc là 0 để tổng luôn tính được trong lúc người dùng còn đang gõ. */
export function toQuantity(value: string | undefined): number {
  if (value === undefined || value.trim() === '') return 0
  return /^\d+$/.test(value.trim()) ? Number(value) : 0
}
