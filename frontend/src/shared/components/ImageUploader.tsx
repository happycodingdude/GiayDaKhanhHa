import { useEffect, useRef, useState } from 'react'
import { ImageLightbox } from './ImageLightbox'

/** Đúng những gì backend chấp nhận (CR-001 BR-N03). Client chặn sớm, server vẫn là phán quyết cuối. */
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const MAX_SIZE_BYTES = 5 * 1024 * 1024

const TYPE_ERROR = 'Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.'
const SIZE_ERROR = 'Ảnh vượt quá 5MB. Vui lòng chọn ảnh nhỏ hơn.'

/**
 * Nhận diện định dạng bằng CHỮ KÝ BYTE, giống hệt server.
 *
 * `file.type` không đủ: trình duyệt suy nó ra từ phần mở rộng, nên một file PDF đổi tên thành .jpg
 * sẽ khai báo là `image/jpeg` và lọt qua mọi phép kiểm dựa trên MIME. Người dùng chỉ phát hiện ra
 * khi khung xem trước hiện ảnh vỡ — quá muộn và không giải thích được gì (test checklist §9,
 * Slice 2 #5).
 */
function detectImageFormat(header: Uint8Array): boolean {
  const startsWith = (...signature: number[]) =>
    signature.every((byte, index) => header[index] === byte)

  // JPEG: FF D8 FF
  if (startsWith(0xff, 0xd8, 0xff)) return true

  // PNG: 89 50 4E 47 0D 0A 1A 0A
  if (startsWith(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a)) return true

  // WEBP là container RIFF: "RIFF" <4 byte độ dài> "WEBP"
  const ascii = (index: number, char: string) => header[index] === char.charCodeAt(0)
  if (
    header.length >= 12 &&
    ascii(0, 'R') && ascii(1, 'I') && ascii(2, 'F') && ascii(3, 'F') &&
    ascii(8, 'W') && ascii(9, 'E') && ascii(10, 'B') && ascii(11, 'P')
  ) {
    return true
  }

  return false
}

export async function validateImageFile(file: File): Promise<string | null> {
  // Kiểm dung lượng và MIME trước: cả hai đều rẻ, và chặn được đa số trường hợp mà không phải đọc
  // byte nào của một file có thể rất lớn.
  if (!ACCEPTED_TYPES.includes(file.type)) return TYPE_ERROR
  if (file.size > MAX_SIZE_BYTES) return SIZE_ERROR

  const header = new Uint8Array(await file.slice(0, 12).arrayBuffer())
  return detectImageFormat(header) ? null : TYPE_ERROR
}

const CloudUploadIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
    <path d="M7.5 18.5H7a4.5 4.5 0 0 1-.9-8.91A6 6 0 0 1 17.7 8.6 4.5 4.5 0 0 1 17 18.5h-.5" />
    <path d="M12 20v-8M8.75 15.25 12 12l3.25 3.25" />
  </svg>
)

const ExpandIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7" />
  </svg>
)

const TrashIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6M10 11v5M14 11v5" />
  </svg>
)

/**
 * Chọn ảnh bằng kéo-thả hoặc bấm chọn, kèm xem trước và nút xoá (CR-001 §7.3, §7.5).
 *
 * Component này chỉ quản lý MỘT file — đơn hàng có tối đa một ảnh (BR-N02). Nó không tự upload:
 * bên gọi quyết định gửi kèm lúc tạo đơn hay gọi endpoint thay ảnh.
 *
 * `existingUrl` là ảnh đang lưu trên server. Chọn file mới sẽ đè lên phần xem trước, nhưng ảnh cũ
 * chỉ thực sự mất khi bên gọi lưu thành công.
 *
 * Khung xem trước luôn hiện, kể cả khi chưa có ảnh: thẻ ảnh không đổi chiều cao khi thêm/xoá ảnh.
 * Vùng kéo-thả luôn nằm dưới khung — có ảnh rồi thì nó là cách đổi ảnh.
 */
export function ImageUploader({
  file,
  existingUrl,
  disabled = false,
  onChange,
  onRemoveExisting,
}: {
  file: File | null
  existingUrl?: string | null
  disabled?: boolean
  onChange: (file: File | null) => void
  onRemoveExisting?: () => void
}) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [zoomed, setZoomed] = useState(false)

  // Object URL giữ file sống trong bộ nhớ cho tới khi được revoke; quên revoke là rò bộ nhớ mỗi
  // lần người dùng đổi ảnh.
  useEffect(() => {
    if (!file) {
      setPreviewUrl(null)
      return
    }

    const url = URL.createObjectURL(file)
    setPreviewUrl(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  const accept = async (selected: File | null) => {
    if (!selected || disabled) return

    const message = await validateImageFile(selected)
    setError(message)
    onChange(message ? null : selected)
  }

  const shownUrl = previewUrl ?? existingUrl ?? null
  // Bỏ file vừa chọn thì quay về ảnh đang lưu; bỏ chính ảnh đang lưu là việc của bên gọi, vì chỉ
  // nó biết khi nào việc gỡ ảnh được lưu. Bên gọi không cho xoá thì không hiện nút.
  const canRemove = file !== null || (existingUrl != null && onRemoveExisting !== undefined)

  return (
    <div className="uploader">
      <div className="uploader__preview">
        {shownUrl ? (
          <>
            <img src={shownUrl} alt="Ảnh mẫu của đơn hàng" />
            <div className="uploader__tools">
              {canRemove && (
                <button
                  type="button"
                  className="uploader__tool uploader__tool--danger"
                  disabled={disabled}
                  title={file ? 'Bỏ chọn ảnh' : 'Xoá ảnh'}
                  aria-label={file ? 'Bỏ chọn ảnh' : 'Xoá ảnh'}
                  onClick={() => {
                    setError(null)
                    if (file) onChange(null)
                    else onRemoveExisting?.()
                  }}
                >
                  {TrashIcon}
                </button>
              )}
              {/* Xem ảnh lớn không làm thay đổi gì nên luôn bấm được, kể cả khi đơn chỉ đọc. */}
              <button
                type="button"
                className="uploader__tool"
                title="Xem ảnh lớn"
                aria-label="Xem ảnh lớn"
                onClick={() => setZoomed(true)}
              >
                {ExpandIcon}
              </button>
            </div>
          </>
        ) : (
          <span className="uploader__empty">
            <span className="uploader__empty-icon" aria-hidden="true">
              👟
            </span>
            Chưa có ảnh mẫu
          </span>
        )}
      </div>

      <button
        type="button"
        className={`uploader__dropzone ${dragging ? 'uploader__dropzone--active' : ''}`}
        disabled={disabled}
        onClick={() => inputRef.current?.click()}
        onDragOver={(event) => {
          event.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(event) => {
          event.preventDefault()
          setDragging(false)
          void accept(event.dataTransfer.files?.[0] ?? null)
        }}
      >
        <span className="uploader__icon" aria-hidden="true">
          {CloudUploadIcon}
        </span>
        <span className="uploader__text">Kéo thả hoặc bấm chọn</span>
        <span className="uploader__hint">JPG/PNG/WEBP · tối đa 5MB</span>
      </button>

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}

      <input
        ref={inputRef}
        type="file"
        accept={ACCEPTED_TYPES.join(',')}
        hidden
        onChange={(event) => {
          void accept(event.target.files?.[0] ?? null)
          // Ô file sống suốt vòng đời component, nên phải xoá giá trị: không thì bỏ chọn rồi chọn
          // lại đúng file cũ sẽ không phát ra sự kiện change nào.
          event.target.value = ''
        }}
      />

      {shownUrl && (
        <ImageLightbox
          open={zoomed}
          src={shownUrl}
          alt="Ảnh mẫu của đơn hàng"
          onClose={() => setZoomed(false)}
        />
      )}
    </div>
  )
}
