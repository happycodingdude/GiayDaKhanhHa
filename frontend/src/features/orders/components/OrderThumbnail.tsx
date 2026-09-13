import { useState } from 'react'
import { ImageLightbox } from '../../../shared/components/ImageLightbox'

/**
 * Ảnh mẫu thu nhỏ. Bấm vào mở lightbox — cách duy nhất để nhìn rõ mẫu giày mà không phải rời màn
 * hình đang làm việc (CR-001 §7.8).
 *
 * Đơn chưa có ảnh hiển thị placeholder thay vì ô trống: một ô trống trong bảng đọc ra như dữ liệu
 * bị lỗi, còn placeholder nói rõ "đơn này chưa có ảnh".
 */
export function OrderThumbnail({
  imageUrl,
  shoeCode,
  size = 44,
}: {
  imageUrl: string | null
  shoeCode: string
  size?: number
}) {
  const [open, setOpen] = useState(false)

  if (!imageUrl) {
    return (
      <span className="thumb thumb--empty" style={{ width: size, height: size }} aria-label="Chưa có ảnh">
        <span aria-hidden="true">👟</span>
      </span>
    )
  }

  return (
    <>
      <button
        type="button"
        className="thumb"
        style={{ width: size, height: size }}
        title={`Xem ảnh mẫu ${shoeCode}`}
        onClick={(event) => {
          // Thumbnail hay nằm trong một dòng bảng bấm được; mở ảnh không được kéo theo mở đơn.
          event.stopPropagation()
          setOpen(true)
        }}
      >
        <img src={imageUrl} alt={`Ảnh mẫu ${shoeCode}`} />
      </button>

      <ImageLightbox
        open={open}
        src={imageUrl}
        alt={`Ảnh mẫu ${shoeCode}`}
        onClose={() => setOpen(false)}
      />
    </>
  )
}
