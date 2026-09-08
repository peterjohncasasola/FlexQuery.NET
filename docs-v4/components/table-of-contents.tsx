'use client'

import { useEffect, useState } from 'react'
import { List } from 'lucide-react'

export interface Heading {
  text: string
  id: string
  level: number
}

export function TableOfContents({ headings }: { headings: Heading[] }) {
  const [activeId, setActiveId] = useState<string>('')

  useEffect(() => {
    if (headings.length === 0) return
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries.filter((e) => e.isIntersecting)
        if (visible.length > 0) {
          setActiveId(visible[0].target.id)
        }
      },
      { rootMargin: '-80px 0px -70% 0px' },
    )
    for (const h of headings) {
      const el = document.getElementById(h.id)
      if (el) observer.observe(el)
    }
    return () => observer.disconnect()
  }, [headings])

  if (headings.length < 2) return null

  return (
    <aside className="sticky top-16 hidden h-[calc(100vh-4rem)] w-56 shrink-0 overflow-y-auto py-8 pl-10 xl:block">
      <div className="flex items-center gap-1.5 text-[13px] font-semibold tracking-wide text-zinc-900 uppercase dark:text-white">
        <List className="h-3.5 w-3.5" />
        On this page
      </div>
      <ul className="mt-3 space-y-1 border-l border-zinc-200 pl-3 dark:border-zinc-800">
        {headings
          .filter((h) => h.level <= 3)
          .map((h) => (
            <li key={h.id}>
              <a
                href={`#${h.id}`}
                aria-current={activeId === h.id ? 'true' : undefined}
                className={`block border-l-2 py-1 text-[13px] leading-5 transition-colors ${
                  h.level === 3 ? 'pl-5' : 'pl-3'
                } ${
                  activeId === h.id
                    ? '-ml-[13px] border-brand-500 font-medium text-brand-700 dark:text-brand-300'
                    : '-ml-[13px] border-transparent text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white'
                }`}
              >
                {h.text}
              </a>
            </li>
          ))}
      </ul>
    </aside>
  )
}
