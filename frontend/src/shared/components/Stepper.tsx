export interface Step {
  id: string
  label: string
}

/**
 * Luồng mũi tên trái sang phải cho biết quản lý đang ở bước nào của biểu mẫu nhiều bước. Các
 * bước đã qua được đánh dấu hoàn thành, nên thanh này cũng đọc ra được tiến độ.
 *
 * Có `onStepClick` thì mỗi bước đã qua trở thành nút quay lại bước đó. Bước chưa tới vẫn không bấm
 * được: đi tới phải qua nút của từng bước, vì mỗi bước còn kiểm tra dữ liệu trước khi cho đi tiếp.
 */
export function Stepper({
  steps,
  current,
  compact = false,
  onStepClick,
}: {
  steps: Step[]
  current: string
  compact?: boolean
  onStepClick?: (stepId: string) => void
}) {
  const currentIndex = steps.findIndex((step) => step.id === current)

  return (
    <ol className={`steps ${compact ? 'steps--compact' : ''}`} aria-label="Các bước">
      {steps.map((step, index) => {
        const state = index < currentIndex ? 'done' : index === currentIndex ? 'active' : 'todo'
        const canGoBack = state === 'done' && onStepClick !== undefined

        const content = (
          <>
            <span className="steps__index" aria-hidden="true">
              {state === 'done' ? '✓' : index + 1}
            </span>
            <span className="steps__label">{step.label}</span>
          </>
        )

        return (
          <li
            key={step.id}
            className={`steps__item steps__item--${state}`}
            aria-current={state === 'active' ? 'step' : undefined}
          >
            {canGoBack ? (
              <button
                type="button"
                className="steps__control"
                onClick={() => onStepClick(step.id)}
                title={`Quay lại bước ${step.label}`}
              >
                {content}
              </button>
            ) : (
              <span className="steps__control">{content}</span>
            )}
          </li>
        )
      })}
    </ol>
  )
}
