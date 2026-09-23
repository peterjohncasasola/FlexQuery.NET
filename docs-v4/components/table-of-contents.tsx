'use client'

import { useEffect, useState } from 'react'

export interface Heading {
  text: string
  id: string
  level: number
}

export function TableOfContents({ headings }: { headings: Heading[] }) {
  const [activeId, setActiveId] = useState<string>('')

  useEffect(() => {
    if (headings.length === 0) return

    // The active heading is the last one whose top sits above the activation
    // line (~header height + breathing room). rAF-throttled scroll listener:
    // stable while scrolling and never jumps back on short final sections.
    let frame = 0
    const update = () => {
      frame = 0
      const line = 96
      let current = ''
      for (const h of headings) {
        const el = document.getElementById(h.id)
        if (!el) continue
        if (el.getBoundingClientRect().top <= line) current = h.id
        else break
      }
      // Near the bottom of the page, always activate the last heading so the
      // TOC never leaves the final section unresolved.
      if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2) {
        current = headings[headings.length - 1]?.id ?? current
      }
      setActiveId(current)
    }
    const onScroll = () => {
      if (frame) return
      frame = requestAnimationFrame(update)
    }

    update()
    window.addEventListener('scroll', onScroll, { passive: true })
    window.addEventListener('resize', onScroll, { passive: true })
    return () => {
      if (frame) cancelAnimationFrame(frame)
      window.removeEventListener('scroll', onScroll)
      window.removeEventListener('resize', onScroll)
    }
  }, [headings])

  if (headings.length < 2) return null

  return (
    <aside className="sticky top-14 hidden h-[calc(100vh-3.5rem)] w-44 shrink-0 overflow-y-auto overflow-x-hidden py-3 pl-3 pr-2 xl:block" aria-label="On this page">
      <div className="text-[11px] font-semibold tracking-[0.08em] text-zinc-500 uppercase dark:text-zinc-500">On this page</div>
      <ul className="mt-1.5 space-y-0.5">
        {headings
          .filter((h) => h.level <= 3)
          .map((h) => (
            <li key={h.id}>
              <a
                href={`#${h.id}`}
                aria-current={activeId === h.id ? 'true' : undefined}
                className={`block rounded-md border-l-2 py-1 text-[13px] leading-5 transition-colors duration-150 break-words ${
                  h.level === 3 ? 'pl-3' : 'pl-2'
                } ${
                  activeId === h.id
                    ? 'border-brand-500 bg-brand-50 font-medium text-brand-700 dark:bg-brand-950/40 dark:text-brand-300'
                    : 'border-transparent text-zinc-500 hover:border-zinc-300 hover:text-zinc-900 hover:bg-zinc-50 dark:text-zinc-400 dark:hover:border-zinc-700 dark:hover:text-white dark:hover:bg-zinc-800'
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
