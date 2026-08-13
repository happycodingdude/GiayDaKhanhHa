export interface Step {
  id: string
  label: string
}

/**
 * Left-to-right arrow flow showing which step of a multi-step form the manager is on. Steps that
 * are already behind them are marked done, so the indicator also reads as progress.
 */
export function Stepper({
  steps,
  current,
  compact = false,
}: {
  steps: Step[]
  current: string
  compact?: boolean
}) {
  const currentIndex = steps.findIndex((step) => step.id === current)

  return (
    <ol className={`steps ${compact ? 'steps--compact' : ''}`} aria-label="Các bước">
      {steps.map((step, index) => {
        const state = index < currentIndex ? 'done' : index === currentIndex ? 'active' : 'todo'

        return (
          <li
            key={step.id}
            className={`steps__item steps__item--${state}`}
            aria-current={state === 'active' ? 'step' : undefined}
          >
            <span className="steps__index" aria-hidden="true">
              {state === 'done' ? '✓' : index + 1}
            </span>
            <span className="steps__label">{step.label}</span>
          </li>
        )
      })}
    </ol>
  )
}
