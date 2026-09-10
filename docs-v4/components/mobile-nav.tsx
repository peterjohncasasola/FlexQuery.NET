'use client'

import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { Menu, X, ChevronDown } from 'lucide-react'
import { navigation } from '@/lib/navigation'
import { SearchButton } from '@/components/search-button'

export function MobileNav({ links }: { links: { href: string; label: string }[] }) {
  const [open, setOpen] = useState(false)
  const pathname = usePathname()
  const activeSlug = pathname.startsWith('/docs/') ? pathname.slice('/docs/'.length) : ''
  const activeGroup = navigation.find((group) => group.items.some((item) => item.slug === activeSlug))
  const [expanded, setExpanded] = useState<string | null>(activeGroup?.title ?? null)
  const menuButtonRef = useRef<HTMLButtonElement>(null)
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (activeGroup) setExpanded(activeGroup.title)
  }, [activeSlug])

  useEffect(() => {
    if (!open) return
    document.body.style.overflow = 'hidden'
    closeButtonRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        menuButtonRef.current?.focus()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => {
      document.body.style.overflow = ''
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <>
      <button
        ref={menuButtonRef}
        type="button"
        aria-label={open ? 'Close navigation menu' : 'Open navigation menu'}
        aria-expanded={open}
        onClick={() => setOpen(!open)}
        className="flex h-9 w-9 items-center justify-center rounded-md text-zinc-600 transition-colors hover:bg-zinc-100 dark:text-zinc-400 dark:hover:bg-zinc-900 lg:hidden"
      >
        {open ? <X className="h-5 w-5" aria-hidden="true" /> : <Menu className="h-5 w-5" aria-hidden="true" />}
      </button>
      {open &&
        createPortal(
          <div
            ref={panelRef}
            className="fixed inset-x-0 top-14 bottom-0 z-40 overflow-y-auto border-t border-zinc-200 bg-white px-4 py-4 dark:border-zinc-800 dark:bg-zinc-950 lg:hidden"
            role="dialog"
            aria-modal="true"
            aria-label="Documentation navigation"
            onKeyDown={(event) => {
              if (event.key !== 'Tab') return
              const focusable = panelRef.current?.querySelectorAll<HTMLElement>(
                'button, a[href], [tabindex]:not([tabindex="-1"])',
              )
              if (!focusable?.length) return
              const first = focusable[0]
              const last = focusable[focusable.length - 1]
              if (event.shiftKey && document.activeElement === first) {
                event.preventDefault()
                last.focus()
              } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault()
                first.focus()
              }
            }}
          >
            <button ref={closeButtonRef} type="button" className="sr-only" onClick={() => setOpen(false)}>
              Close navigation
            </button>
            <div className="mb-4">
              <SearchButton fullWidth onOpen={() => setOpen(false)} />
            </div>
            <nav aria-label="Mobile documentation">
              {links.map((l) => (
                <Link
                  key={l.href}
                  href={l.href}
                  onClick={() => setOpen(false)}
                  className={`block rounded-md px-3 py-2 text-sm font-medium ${
                    pathname === l.href
                      ? 'bg-brand-50 text-brand-700 dark:bg-brand-950/60 dark:text-brand-300'
                      : 'text-zinc-700 dark:text-zinc-300'
                  }`}
                >
                  {l.label}
                </Link>
              ))}
              <p className="mt-6 mb-2 px-3 text-xs font-semibold tracking-wide text-zinc-400 uppercase">
                Documentation
              </p>
              {navigation.map((group) => {
                const isExpanded = expanded === group.title
                return (
                  <div key={group.title} className="mt-1">
                    <button
                      type="button"
                      onClick={() => setExpanded(isExpanded ? null : group.title)}
                      className="flex w-full items-center justify-between rounded-md px-3 py-2 text-sm font-semibold text-zinc-900 dark:text-white"
                      aria-expanded={isExpanded}
                    >
                      {group.title}
                      <ChevronDown className={`h-4 w-4 transition-transform ${isExpanded ? 'rotate-180' : ''}`} aria-hidden="true" />
                    </button>
                    {isExpanded && (
                      <div className="ml-3 border-l border-zinc-200 pl-3 dark:border-zinc-800">
                        {group.items.map((item) => (
                          <Link
                            key={item.slug}
                            href={`/docs/${item.slug}`}
                            onClick={() => setOpen(false)}
                            className={`block rounded-md px-3 py-1.5 text-sm ${
                              pathname === `/docs/${item.slug}`
                                ? 'text-brand-600 dark:text-brand-400'
                                : 'text-zinc-600 dark:text-zinc-400'
                            }`}
                          >
                            {item.title}
                          </Link>
                        ))}
                      </div>
                    )}
                  </div>
                )
              })}
            </nav>
          </div>,
          document.body,
        )}
    </>
  )
}
