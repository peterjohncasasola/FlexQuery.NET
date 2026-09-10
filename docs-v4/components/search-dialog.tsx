'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import Fuse from 'fuse.js'
import { ArrowRight, FileText, LoaderCircle, Search, Text } from 'lucide-react'
import { useSearch } from '@/components/search-provider'

interface SearchEntry {
  title: string
  description: string
  section: string
  slug: string
  headings: { text: string; id: string; level?: number }[]
  body: string
}

interface SearchResult {
  key: string
  title: string
  pageTitle: string
  description: string
  section: string
  href: string
  kind: 'page' | 'heading'
  searchText: string
}

type LoadState = 'idle' | 'loading' | 'ready' | 'error'

function createSearchResults(entries: SearchEntry[]): SearchResult[] {
  return entries.flatMap((entry) => {
    const page: SearchResult = {
      key: entry.slug,
      title: entry.title,
      pageTitle: entry.title,
      description: entry.description,
      section: entry.section || 'Documentation',
      href: `/docs/${entry.slug}`,
      kind: 'page',
      searchText: `${entry.title} ${entry.description} ${entry.body}`,
    }

    const headings = entry.headings
      .filter((heading) => heading.level !== 1 && heading.text !== entry.title)
      .map<SearchResult>((heading) => ({
        key: `${entry.slug}#${heading.id}`,
        title: heading.text,
        pageTitle: entry.title,
        description: entry.description,
        section: entry.section || 'Documentation',
        href: `/docs/${entry.slug}#${heading.id}`,
        kind: 'heading',
        searchText: heading.text,
      }))

    return [page, ...headings]
  })
}

export function SearchDialog() {
  const { open, setOpen } = useSearch()
  const [entries, setEntries] = useState<SearchEntry[]>([])
  const [loadState, setLoadState] = useState<LoadState>('idle')
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const dialogRef = useRef<HTMLDivElement>(null)
  const restoreFocusRef = useRef<HTMLElement | null>(null)
  const router = useRouter()

  useEffect(() => {
    if (!open || entries.length > 0) return

    const controller = new AbortController()
    setLoadState('loading')
    fetch('/search-index.json', { signal: controller.signal })
      .then((response) => {
        if (!response.ok) throw new Error(`Search index returned ${response.status}`)
        return response.json() as Promise<SearchEntry[]>
      })
      .then((nextEntries) => {
        setEntries(nextEntries)
        setLoadState('ready')
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === 'AbortError') {
          setLoadState('idle')
          return
        }
        setLoadState('error')
      })

    return () => controller.abort()
  }, [open, entries.length])

  useEffect(() => {
    if (!open) return
    restoreFocusRef.current = document.activeElement as HTMLElement | null
    setQuery('')
    setSelected(0)
    document.body.style.overflow = 'hidden'
    requestAnimationFrame(() => inputRef.current?.focus())

    return () => {
      document.body.style.overflow = ''
      restoreFocusRef.current?.focus()
    }
  }, [open])

  const searchable = useMemo(() => createSearchResults(entries), [entries])
  const fuse = useMemo(
    () =>
      new Fuse(searchable, {
        keys: ['searchText'],
        threshold: 0.3,
        ignoreLocation: true,
      }),
    [searchable],
  )

  const results = useMemo(() => {
    if (query.trim().length === 0) {
      return searchable.filter((result) => result.kind === 'page').slice(0, 8)
    }
    return fuse.search(query, { limit: 12 }).map((result) => result.item)
  }, [fuse, query, searchable])

  useEffect(() => {
    if (selected >= results.length) setSelected(Math.max(0, results.length - 1))
  }, [results.length, selected])

  if (!open) return null

  const close = () => setOpen(false)
  const go = (href: string) => {
    close()
    router.push(href)
  }

  return (
    <div
      className="fixed inset-0 z-100 flex items-start justify-center bg-zinc-950/45 p-3 pt-[8vh] backdrop-blur-[3px] sm:p-6 sm:pt-[12vh] dark:bg-black/70"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) close()
      }}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="search-dialog-title"
        className="w-full max-w-2xl overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-[0_24px_80px_rgba(15,23,42,0.22)] dark:border-zinc-800 dark:bg-zinc-950"
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            event.preventDefault()
            close()
          } else if (event.key === 'Tab') {
            const focusable = dialogRef.current?.querySelectorAll<HTMLElement>(
              'input, button:not([tabindex="-1"]), a[href], [tabindex]:not([tabindex="-1"])',
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
          }
        }}
      >
        <h2 id="search-dialog-title" className="sr-only">
          Search documentation
        </h2>
        <div className="flex items-center gap-3 border-b border-zinc-200 px-4 dark:border-zinc-800">
          <Search className="h-4 w-4 shrink-0 text-zinc-400" aria-hidden="true" />
          <input
            ref={inputRef}
            type="search"
            role="combobox"
            aria-controls="search-results"
            aria-expanded="true"
            aria-autocomplete="list"
            aria-activedescendant={results[selected] ? `search-result-${selected}` : undefined}
            value={query}
            onChange={(event) => {
              setQuery(event.target.value)
              setSelected(0)
            }}
            onKeyDown={(event) => {
              if (event.key === 'ArrowDown') {
                event.preventDefault()
                setSelected((current) => Math.min(current + 1, results.length - 1))
              } else if (event.key === 'ArrowUp') {
                event.preventDefault()
                setSelected((current) => Math.max(current - 1, 0))
              } else if (event.key === 'Home') {
                event.preventDefault()
                setSelected(0)
              } else if (event.key === 'End') {
                event.preventDefault()
                setSelected(Math.max(0, results.length - 1))
              } else if (event.key === 'Enter' && results[selected]) {
                event.preventDefault()
                go(results[selected].href)
              }
            }}
            placeholder="Search guides, APIs, and concepts…"
            className="h-14 w-full bg-transparent text-[15px] text-zinc-900 outline-none placeholder:text-zinc-400 dark:text-white dark:placeholder:text-zinc-500"
          />
          <button
            type="button"
            onClick={close}
            className="rounded-md border border-zinc-200 px-2 py-1 font-mono text-[10px] text-zinc-500 dark:border-zinc-700 dark:text-zinc-400"
            aria-label="Close search"
          >
            ESC
          </button>
        </div>

        <div className="min-h-64">
          {loadState === 'loading' && (
            <div className="flex min-h-64 items-center justify-center gap-2 text-sm text-zinc-500" role="status">
              <LoaderCircle className="h-4 w-4 animate-spin" aria-hidden="true" />
              Loading documentation…
            </div>
          )}
          {loadState === 'error' && (
            <div className="flex min-h-64 flex-col items-center justify-center px-6 text-center" role="alert">
              <p className="font-medium text-zinc-900 dark:text-white">Search is temporarily unavailable</p>
              <p className="mt-1 max-w-sm text-sm text-zinc-500 dark:text-zinc-400">
                Browse the documentation navigation, or close this window and try again.
              </p>
            </div>
          )}
          {loadState === 'ready' && results.length === 0 && (
            <div className="flex min-h-64 flex-col items-center justify-center px-6 text-center" role="status">
              <p className="font-medium text-zinc-900 dark:text-white">No results for “{query}”</p>
              <p className="mt-1 max-w-sm text-zinc-500 dark:text-zinc-400">
                Try a feature or API name such as “filter”, “Dapper”, or “QueryOptions”.
              </p>
            </div>
          )}
          {loadState === 'ready' && results.length > 0 && (
            <>
              <div className="flex items-center justify-between px-4 pt-3 pb-1 text-[11px] font-medium tracking-wide text-zinc-400 uppercase dark:text-zinc-500">
                <span>{query ? `${results.length} results` : 'Suggested pages'}</span>
                <span className="hidden sm:inline">↑↓ to navigate · Enter to open</span>
              </div>
              <ul id="search-results" className="max-h-[min(55vh,28rem)] overflow-y-auto p-2" role="listbox">
                {results.map((result, index) => {
                  const Icon = result.kind === 'page' ? FileText : Text
                  return (
                    <li key={result.key} role="presentation">
                      <button
                        id={`search-result-${index}`}
                        type="button"
                        role="option"
                        tabIndex={-1}
                        aria-selected={index === selected}
                        onMouseEnter={() => setSelected(index)}
                        onClick={() => go(result.href)}
                        className={`group flex w-full items-start gap-3 rounded-lg px-3 py-2.5 text-left transition-colors ${
                          index === selected
                            ? 'bg-brand-50 text-zinc-950 dark:bg-brand-950/70 dark:text-white'
                            : 'text-zinc-700 dark:text-zinc-300'
                        }`}
                      >
                        <Icon
                          className={`mt-0.5 h-4 w-4 shrink-0 ${
                            index === selected ? 'text-brand-600 dark:text-brand-400' : 'text-zinc-400 dark:text-zinc-600'
                          }`}
                          aria-hidden="true"
                        />
                        <span className="min-w-0 flex-1">
                          <span className="flex items-center gap-2">
                            <span className="truncate text-sm font-medium">{result.title}</span>
                            <span className="shrink-0 rounded bg-zinc-100 px-1.5 py-0.5 text-[10px] font-medium text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
                              {result.section}
                            </span>
                          </span>
                          <span className="mt-0.5 block truncate text-xs text-zinc-500 dark:text-zinc-400">
                            {result.kind === 'heading' ? `In ${result.pageTitle}` : result.description}
                          </span>
                        </span>
                        <ArrowRight
                          className={`mt-1 h-3.5 w-3.5 shrink-0 transition-transform ${
                            index === selected ? 'translate-x-0 text-brand-500' : '-translate-x-1 text-transparent'
                          }`}
                          aria-hidden="true"
                        />
                      </button>
                    </li>
                  )
                })}
              </ul>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
