import type { ReactNode } from 'react'
import { Button } from '../components/ui'
import { Modal } from './Modal'

/**
 * Xác nhận một thao tác. Tồn tại vì CR-001 yêu cầu confirm ở khá nhiều chỗ — bật/tắt dây chuyền,
 * ghi đè phân bổ khi đổi mode, chia đều lại ma trận — và ba chỗ đó tự viết ba kiểu thì sớm muộn
 * cũng lệch nhau (§7.6, §7.9).
 */
export function ConfirmDialog({
  open,
  title,
  confirmLabel = 'Xác nhận',
  cancelLabel = 'Huỷ',
  tone = 'primary',
  loading = false,
  children,
  onConfirm,
  onCancel,
}: {
  open: boolean
  title: string
  confirmLabel?: string
  cancelLabel?: string
  tone?: 'primary' | 'danger'
  loading?: boolean
  children: ReactNode
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <Modal
      open={open}
      title={title}
      onClose={onCancel}
      width={480}
      footer={
        <>
          <Button onClick={onCancel} disabled={loading}>
            {cancelLabel}
          </Button>
          <Button variant={tone} loading={loading} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      {children}
    </Modal>
  )
}
