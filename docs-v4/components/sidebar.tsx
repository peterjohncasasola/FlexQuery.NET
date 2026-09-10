'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { ChevronDown } from 'lucide-react'
import { navigation } from '@/lib/navigation'

export function Sidebar() {
  const pathname = usePathname()
  const activeSlug = pathname.startsWith('/docs/') ? pathname.slice('/docs/'.length) : ''
  const activeGroup = navigation.find((group) => group.items.some((item) => item.slug === activeSlug))
  const [openGroups, setOpenGroups] = useState<string[]>(() => (activeGroup ? [activeGroup.title] : []))

  useEffect(() => {
    if (activeGroup) {
      setOpenGroups((current) =>
        current.includes(activeGroup.title) ? current : [...current, activeGroup.title],
      )
    }
  }, [activeSlug])

  return (
    <aside className="sticky top-14 hidden h-[calc(100vh-3.5rem)] w-60 shrink-0 overflow-y-auto border-r border-zinc-200 py-7 pr-6 dark:border-zinc-800 lg:block">
      <nav aria-label="Documentation" className="space-y-1">
        {navigation.map((group, groupIndex) => {
          const isOpen = openGroups.includes(group.title)
          const groupId = `docs-nav-group-${groupIndex}`
          return (
            <div key={group.title} className="pb-1">
              <button
                type="button"
                onClick={() =>
                  setOpenGroups((prev) =>
                    prev.includes(group.title) ? prev.filter((t) => t !== group.title) : [...prev, group.title],
                  )
                }
                aria-expanded={isOpen}
                aria-controls={groupId}
                className="group flex min-h-8 w-full items-center justify-between rounded-md px-2 text-left text-[11px] font-semibold tracking-[0.08em] text-zinc-500 uppercase transition-colors hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-500 dark:hover:bg-zinc-900 dark:hover:text-white"
              >
                {group.title}
                <ChevronDown
                  className={`h-3.5 w-3.5 transition-transform ${isOpen ? '' : '-rotate-90'}`}
                  aria-hidden="true"
                />
              </button>
              {isOpen && (
                <ul id={groupId} className="mt-1 space-y-0.5 border-l border-zinc-200 pl-3 dark:border-zinc-800">
                  {group.items.map((item) => {
                    const active = item.slug === activeSlug
                    return (
                      <li key={item.slug}>
                        <Link
                          href={`/docs/${item.slug}`}
                          aria-current={active ? 'page' : undefined}
                          className={`-ml-3.5 block rounded-r-md border-l-2 py-1.5 pr-2 pl-3 text-[13px] leading-5 transition-colors ${
                            active
                              ? 'border-brand-500 bg-brand-50/70 font-medium text-brand-700 dark:bg-brand-950/40 dark:text-brand-300'
                              : 'border-transparent text-zinc-600 hover:border-zinc-300 hover:bg-zinc-50 hover:text-zinc-950 dark:text-zinc-400 dark:hover:border-zinc-700 dark:hover:bg-zinc-900/70 dark:hover:text-white'
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
