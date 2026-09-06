import { useEffect, useRef, type ReactNode } from 'react'

/** Một phần tử bị khoá cuộn, kèm giá trị inline cũ để trả lại nguyên trạng. */
interface LockedScroller {
  el: HTMLElement
  overflow: string
  paddingRight: string
}

/**
 * Mọi tổ tiên đang cuộn được của modal.
 *
 * Không hardcode một selector cố định: bánh xe chuột chỉ cuộn được tổ tiên của phần tử nhận sự
 * kiện, nên khoá đúng chuỗi tổ tiên là đủ và không phụ thuộc vào việc đoán trúng phần tử nào của
 * layout đang giữ thanh cuộn.
 */
function scrollableAncestors(node: HTMLElement): HTMLElement[] {
  const found: HTMLElement[] = []

  for (let el = node.parentElement; el; el = el.parentElement) {
    const style = getComputedStyle(el)
    const canScrollY = /^(auto|scroll|overlay)$/.test(style.overflowY)
    const canScrollX = /^(auto|scroll|overlay)$/.test(style.overflowX)
    const overflowsY = canScrollY && el.scrollHeight > el.clientHeight
    const overflowsX = canScrollX && el.scrollWidth > el.clientWidth

    if (overflowsY || overflowsX) found.push(el)
  }

  // documentElement không nằm trong chuỗi parentElement theo cách trên nếu chính nó cuộn.
  const root = document.documentElement
  if (root.scrollHeight > root.clientHeight && !found.includes(root)) found.push(root)

  return found
}

function lockScrollers(panel: HTMLElement): LockedScroller[] {
  return scrollableAncestors(panel).map((el) => {
    const previous: LockedScroller = {
      el,
      overflow: el.style.overflow,
      paddingRight: el.style.paddingRight,
    }

    // Ẩn thanh cuộn làm vùng nội dung rộng thêm đúng bề ngang của nó, nền sẽ giật ngang một nhịp
    // khi modal mở. Bù lại bằng padding; hệ dùng thanh cuộn overlay thì phần bù bằng 0.
    const scrollbar = el.offsetWidth - el.clientWidth
    el.style.overflow = 'hidden'
    if (scrollbar > 0) {
      const current = Number.parseFloat(getComputedStyle(el).paddingRight) || 0
      el.style.paddingRight = `${current + scrollbar}px`
    }

    return previous
  })
}

function restoreScrollers(locked: LockedScroller[]) {
  for (const { el, overflow, paddingRight } of locked) {
    if (overflow) el.style.overflow = overflow
    else el.style.removeProperty('overflow')

    if (paddingRight) el.style.paddingRight = paddingRight
    else el.style.removeProperty('padding-right')
  }
}

export function Modal({
  open,
  title,
  description,
  onClose,
  footer,
  children,
  width = 560,
}: {
  open: boolean
  title: string
  description?: ReactNode
  onClose: () => void
  footer?: ReactNode
  children: ReactNode
  width?: number
}) {
  const panelRef = useRef<HTMLDivElement>(null)

  /**
   * Bên gọi luôn truyền một arrow mới ở mỗi lần render (`onClose={() => setEditing(null)}`). Để
   * `onClose` trong deps nghĩa là effect chạy lại sau MỌI lần render của bên gọi — và mỗi lần chạy
   * lại nó gọi panelRef.focus(), giật focus khỏi ô người dùng đang gõ. Giữ hàm trong ref để effect
   * chỉ phụ thuộc vào `open`.
   */
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  })

  useEffect(() => {
    if (!open) return

    const panel = panelRef.current
    // Khoá cuộn tính theo chính chuỗi tổ tiên của modal này, nên modal lồng nhau tự đúng: modal con
    // chỉ khoá phần thân của modal cha, khoá của modal cha ở ngoài không bị đụng tới.
    const locked = panel ? lockScrollers(panel) : []

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      // Chỉ modal trên cùng phản hồi Escape: mọi modal đều nghe trên document, nếu không một lần
      // Escape sẽ đóng cả chồng thay vì chỉ cái người dùng đang nhìn. Modal đứng sau cùng trong
      // DOM chính là cái vẽ trên cùng — modal lồng nhau nằm bên trong thân của modal cha.
      const panels = document.querySelectorAll('.modal')
      if (panels[panels.length - 1] !== panelRef.current) return
      onCloseRef.current()
    }

    document.addEventListener('keydown', onKeyDown)

    // Chỉ kéo focus vào panel khi bên trong chưa có gì nhận focus: nội dung modal tự chọn điểm
    // focus của nó (ô nhập autoFocus, nút "Quay lại" của dialog xuất hàng), ghi đè là sai.
    if (panel && !panel.contains(document.activeElement)) {
      panel.focus()
    }

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      restoreScrollers(locked)
    }
  }, [open])

  if (!open) return null

  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <div
        className="modal"
        style={{ maxWidth: width }}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        ref={panelRef}
      >
        <header className="modal__header">
          <div>
            <h2 className="modal__title">{title}</h2>
            {description && <p className="modal__description">{description}</p>}
          </div>
          <button className="modal__close" onClick={onClose} aria-label="Đóng" type="button">
            ×
          </button>
        </header>
        <div className="modal__body">{children}</div>
        {footer && <footer className="modal__footer">{footer}</footer>}
      </div>
    </div>
  )
}
