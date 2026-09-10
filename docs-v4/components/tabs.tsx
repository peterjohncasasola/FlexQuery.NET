'use client'

import { Children, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'

export function Tabs({ children }: { children: ReactNode }) {
  const items = Children.toArray(children)
  const labels = items.map((item: any) => item?.props?.label ?? 'Tab')
  const [active, setActive] = useState(0)
  const id = useId()
  const tabRefs = useRef<Array<HTMLButtonElement | null>>([])

  const selectFromKeyboard = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    let next = index
    if (event.key === 'ArrowRight') next = (index + 1) % items.length
    else if (event.key === 'ArrowLeft') next = (index - 1 + items.length) % items.length
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = items.length - 1
    else return

    event.preventDefault()
    setActive(next)
    tabRefs.current[next]?.focus()
  }

  return (
    <div className="my-6">
      <div
        role="tablist"
        aria-label="Code examples"
        className="flex gap-1 border-b border-zinc-200 dark:border-zinc-800"
      >
        {labels.map((label, i) => (
          <button
            key={label}
            ref={(element) => {
              tabRefs.current[i] = element
            }}
            id={`${id}-tab-${i}`}
            type="button"
            role="tab"
            aria-selected={active === i}
            aria-controls={`${id}-panel-${i}`}
            tabIndex={active === i ? 0 : -1}
            onClick={() => setActive(i)}
            onKeyDown={(event) => selectFromKeyboard(event, i)}
            className={`rounded-t-md px-3.5 py-2 text-sm font-medium transition-colors ${
              active === i
                ? 'border-b-2 border-brand-500 text-brand-700 dark:text-brand-300'
                : 'text-zinc-500 hover:text-zinc-800 dark:text-zinc-400 dark:hover:text-zinc-200'
            }`}
          >
            {label}
          </button>
        ))}
      </div>
      {items.map((item, i) => (
        <div
          key={i}
          id={`${id}-panel-${i}`}
          role="tabpanel"
          aria-labelledby={`${id}-tab-${i}`}
          tabIndex={0}
          hidden={active !== i}
        >
          {item}
        </div>
      ))}
    </div>
  )
}

export function Tab({ label, children }: { label: string; children: ReactNode }) {
  return <div data-label={label}>{children}</div>
}
