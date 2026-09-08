'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { navigation } from '@/lib/navigation'

export function Sidebar() {
  const pathname = usePathname()
  const activeSlug = pathname.startsWith('/docs/') ? pathname.slice('/docs/'.length) : pathname.replace('/docs', '')
  const [openGroups, setOpenGroups] = useState<string[]>([])

  useEffect(() => {
    const group = navigation.find((g) => g.items.some((i) => i.slug === activeSlug))
    if (group) setOpenGroups((prev) => (prev.includes(group.title) ? prev : [...prev, group.title]))
  }, [activeSlug])

  return (
    <aside className="sticky top-16 hidden h-[calc(100vh-4rem)] w-60 shrink-0 overflow-y-auto border-r border-zinc-200 py-8 pr-6 dark:border-zinc-800 lg:block">
      <nav aria-label="Documentation" className="space-y-6">
        {navigation.map((group) => {
          const isOpen = openGroups.includes(group.title)
          return (
            <div key={group.title}>
              <button
                type="button"
                onClick={() =>
                  setOpenGroups((prev) =>
                    prev.includes(group.title) ? prev.filter((t) => t !== group.title) : [...prev, group.title],
                  )
                }
                aria-expanded={isOpen}
                className="mb-2 flex w-full items-center justify-between text-[13px] font-semibold tracking-wide text-zinc-900 uppercase dark:text-white"
              >
                {group.title}
              </button>
              {isOpen && (
                <ul className="space-y-0.5 border-l border-zinc-200 pl-3 dark:border-zinc-800">
                  {group.items.map((item) => {
                    const active = item.slug === activeSlug
                    return (
                      <li key={item.slug}>
                        <Link
                          href={`/docs/${item.slug}`}
                          aria-current={active ? 'page' : undefined}
                          className={`-ml-3.5 block rounded-md border-l-2 py-1.5 pl-3 text-sm transition-colors ${
                            active
                              ? 'border-brand-500 font-medium text-brand-700 dark:text-brand-300'
                              : 'border-transparent text-zinc-600 hover:border-zinc-300 hover:text-zinc-900 dark:text-zinc-400 dark:hover:border-zinc-700 dark:hover:text-white'
                          }`}
                        >
                          {item.title}
                        </Link>
                      </li>
                    )
                  })}
                </ul>
              )}
            </div>
          )
        })}
      </nav>
    </aside>
  )
}
