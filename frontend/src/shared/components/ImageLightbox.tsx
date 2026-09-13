import { useEffect } from 'react'

/**
 * Xem ảnh mẫu ở kích thước lớn. Nhẹ hơn <see cref="Modal"/> một cách chủ đích: không header, không
 * footer, bấm đâu cũng đóng — nó chỉ để nhìn cho rõ, không có thao tác nào bên trong.
 */
export function ImageLightbox({
  open,
  src,
  alt,
  onClose,
}: {
  open: boolean
  src: string
  alt: string
  onClose: () => void
}) {
  useEffect(() => {
    if (!open) return

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }

    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="lightbox" role="dialog" aria-modal="true" aria-label={alt} onClick={onClose}>
      <img className="lightbox__image" src={src} alt={alt} />
      <button type="button" className="lightbox__close" aria-label="Đóng" onClick={onClose}>
        ×
      </button>
    </div>
  )
}
