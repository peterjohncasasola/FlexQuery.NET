'use client'

import { useState, type ReactNode } from 'react'

export function Tabs({ children }: { children: ReactNode }) {
  const tabs = Array.isArray(children) ? children : [children]
  const items = tabs.filter(Boolean) as ReactNode[]
  const labels = items.map((item: any) => item?.props?.label ?? 'Tab')
  const [active, setActive] = useState(0)

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
            type="button"
            role="tab"
            aria-selected={active === i}
            onClick={() => setActive(i)}
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
        <div key={i} role="tabpanel" hidden={active !== i}>
          {item}
        </div>
      ))}
    </div>
  )
}

export function Tab({ label, children }: { label: string; children: ReactNode }) {
  return <div data-label={label}>{children}</div>
}
