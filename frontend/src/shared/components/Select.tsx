import { useEffect, useId, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react'

export interface SelectOption<T extends string | number> {
  value: T
  label: string
}

/**
 * Dropdown chọn một giá trị, thay cho `<select>` gốc: popup của `<select>` do hệ điều hành vẽ nên
 * không style được và lệch hẳn với phần còn lại của giao diện.
 *
 * Theo mẫu "select-only combobox" của WAI-ARIA: focus luôn nằm trên ô chọn, option đang trỏ tới
 * được báo qua `aria-activedescendant`, nên không phải chuyển focus vào trong danh sách. Ô chọn là
 * `div` có tabIndex chứ không phải `button`: Safari không focus `button` khi click chuột, và nút gốc
 * tự bắn click khi nhả phím Space — cả hai đều làm danh sách đóng/mở sai.
 */
export function Select<T extends string | number>({
  value,
  options,
  onChange,
  'aria-label': ariaLabel,
}: {
  value: T
  options: SelectOption<T>[]
  onChange: (value: T) => void
  'aria-label': string
}) {
  const listId = useId()
  const rootRef = useRef<HTMLDivElement>(null)
  const listRef = useRef<HTMLUListElement>(null)
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(0)
  const [dropUp, setDropUp] = useState(false)

  const selectedIndex = options.findIndex((option) => option.value === value)
  const lastIndex = options.length - 1

  const openAt = (index: number) => {
    setActiveIndex(index)
    setOpen(true)
  }

  const choose = (index: number) => {
    const option = options[index]
    if (option && option.value !== value) onChange(option.value)
    setOpen(false)
  }

  // Mở ngược lên khi phía dưới không đủ chỗ — vd. ô "Số dòng" ở thanh phân trang cuối trang.
  useLayoutEffect(() => {
    const root = rootRef.current
    const list = listRef.current
    if (!open || !root || !list) return

    const rect = root.getBoundingClientRect()
    const spaceBelow = window.innerHeight - rect.bottom
    setDropUp(spaceBelow < list.offsetHeight + 8 && rect.top > spaceBelow)
  }, [open])

  // Di chuyển bằng phím tới option nằm ngoài vùng cuộn thì kéo nó vào tầm nhìn. Tự chỉnh scrollTop
  // của danh sách thay vì scrollIntoView: hàm đó cuộn cả tổ tiên, kể cả `.page--fill` (overflow
  // hidden) — trang bị đẩy lệch mà người dùng không cuộn về được.
  useEffect(() => {
    const list = listRef.current
    const option = list?.children[activeIndex] as HTMLElement | undefined
    if (!open || !list || !option) return

    if (option.offsetTop < list.scrollTop) {
      list.scrollTop = option.offsetTop
    } else if (option.offsetTop + option.offsetHeight > list.scrollTop + list.clientHeight) {
      list.scrollTop = option.offsetTop + option.offsetHeight - list.clientHeight
    }
  }, [open, activeIndex])

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (!open) {
      if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) {
        event.preventDefault()
        openAt(Math.max(selectedIndex, 0))
      } else if (event.key === 'Home' || event.key === 'End') {
        event.preventDefault()
        openAt(event.key === 'Home' ? 0 : lastIndex)
      }
      return
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault()
        setActiveIndex((index) => Math.min(index + 1, lastIndex))
        break
      case 'ArrowUp':
        event.preventDefault()
        setActiveIndex((index) => Math.max(index - 1, 0))
        break
      case 'Home':
        event.preventDefault()
        setActiveIndex(0)
        break
      case 'End':
        event.preventDefault()
        setActiveIndex(lastIndex)
        break
      case 'Enter':
      case ' ':
        event.preventDefault()
        choose(activeIndex)
        break
      case 'Escape':
        event.preventDefault()
        setOpen(false)
        break
    }
  }

  return (
    <div className="select" ref={rootRef}>
      <div
        role="combobox"
        tabIndex={0}
        className={`select__trigger ${open ? 'select__trigger--open' : ''}`}
        aria-label={ariaLabel}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-activedescendant={open ? `${listId}-${activeIndex}` : undefined}
        onClick={() => (open ? setOpen(false) : openAt(Math.max(selectedIndex, 0)))}
        onKeyDown={onKeyDown}
        // Bấm ra ngoài hoặc Tab đi chỗ khác đều làm mất focus: đóng danh sách, giữ nguyên giá trị.
        onBlur={() => setOpen(false)}
      >
        {/* Mọi nhãn chồng lên cùng một ô lưới, chỉ nhãn đang chọn hiện ra: ô chọn luôn rộng bằng
            nhãn dài nhất như `<select>` gốc, không co giãn mỗi lần đổi lựa chọn. */}
        <span className="select__value">
          {options.map((option, index) => (
            <span
              key={String(option.value)}
              className={index === selectedIndex ? '' : 'select__value-ghost'}
            >
              {option.label}
            </span>
          ))}
        </span>
        <svg className="select__chevron" viewBox="0 0 16 16" aria-hidden="true">
          <path d="M4 6l4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>

      {open && (
        <ul
          id={listId}
          ref={listRef}
          role="listbox"
          aria-label={ariaLabel}
          className={`select__menu ${dropUp ? 'select__menu--up' : ''}`}
          // Giữ focus trên ô chọn khi bấm vào option; mất focus là danh sách đóng trước khi kịp chọn.
          onMouseDown={(event) => event.preventDefault()}
        >
          {options.map((option, index) => (
            <li
              key={String(option.value)}
              id={`${listId}-${index}`}
              role="option"
              aria-selected={index === selectedIndex}
              className={`select__option ${index === activeIndex ? 'select__option--active' : ''} ${
                index === selectedIndex ? 'select__option--selected' : ''
              }`}
              // mousemove chứ không phải mouseenter: danh sách cuộn bằng phím dưới con trỏ đứng yên
              // cũng bắn mouseenter, kéo option đang trỏ tới nhảy theo con trỏ.
              onMouseMove={() => setActiveIndex(index)}
              onClick={() => choose(index)}
            >
              {option.label}
              {/* Luôn giữ chỗ cho dấu tích để danh sách không đổi bề rộng theo option đang chọn. */}
              <svg className="select__check" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M3.5 8.5l3 3 6-7" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
