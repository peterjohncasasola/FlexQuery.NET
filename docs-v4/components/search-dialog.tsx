'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import Fuse from 'fuse.js'
import { Search, FileText, CornerDownLeft } from 'lucide-react'
import { useSearch } from '@/components/search-provider'

interface SearchEntry {
  title: string
  description: string
  section: string
  slug: string
  headings: { text: string; id: string }[]
  body: string
}

export function SearchDialog() {
  const { open, setOpen } = useSearch()
  const [entries, setEntries] = useState<SearchEntry[]>([])
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const router = useRouter()

  useEffect(() => {
    if (open && entries.length === 0) {
      fetch('/search-index.json')
        .then((r) => r.json())
        .then(setEntries)
        .catch(() => setEntries([]))
    }
  }, [open, entries.length])

  useEffect(() => {
    if (open) {
      setQuery('')
      setSelected(0)
      requestAnimationFrame(() => inputRef.current?.focus())
    }
  }, [open])

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [setOpen])

  const fuse = useMemo(
    () =>
      new Fuse(entries, {
        keys: [
          { name: 'title', weight: 3 },
          { name: 'headings.text', weight: 2 },
          { name: 'description', weight: 1.5 },
          { name: 'body', weight: 1 },
        ],
        threshold: 0.35,
        ignoreLocation: true,
        includeScore: true,
      }),
    [entries],
  )

  const results = useMemo(() => {
    if (query.trim().length === 0) return entries.slice(0, 8).map((e) => ({ item: e }))
    return fuse.search(query, { limit: 10 })
  }, [fuse, query, entries])

  if (!open) return null

  const go = (slug: string) => {
    setOpen(false)
    router.push(`/docs/${slug}`)
  }

  return (
    <div
      className="fixed inset-0 z-100 flex items-start justify-center bg-zinc-950/40 p-4 pt-[12vh] backdrop-blur-sm dark:bg-black/60"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) setOpen(false)
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Search documentation"
        className="w-full max-w-xl overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-2xl dark:border-zinc-800 dark:bg-zinc-900"
      >
        <div className="flex items-center gap-3 border-b border-zinc-200 px-4 dark:border-zinc-800">
          <Search className="h-4 w-4 shrink-0 text-zinc-400" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value)
              setSelected(0)
            }}
            onKeyDown={(e) => {
              if (e.key === 'ArrowDown') {
                e.preventDefault()
                setSelected((s) => Math.min(s + 1, results.length - 1))
              } else if (e.key === 'ArrowUp') {
                e.preventDefault()
                setSelected((s) => Math.max(s - 1, 0))
              } else if (e.key === 'Enter' && results[selected]) {
                go(results[selected].item.slug)
              }
            }}
            placeholder="Search documentation..."
            className="h-12 w-full bg-transparent text-sm text-zinc-900 outline-none placeholder:text-zinc-400 dark:text-white"
          />
        </div>
        <ul className="max-h-80 overflow-y-auto p-2" role="listbox">
          {results.length === 0 && (
            <li className="px-3 py-8 text-center text-sm text-zinc-400">No results found.</li>
          )}
          {results.map((r, i) => (
            <li key={r.item.slug}>
              <button
                type="button"
                role="option"
                aria-selected={i === selected}
                onMouseEnter={() => setSelected(i)}
                onClick={() => go(r.item.slug)}
                className={`flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm ${
                  i === selected
                    ? 'bg-brand-50 text-brand-900 dark:bg-brand-950/60 dark:text-white'
                    : 'text-zinc-700 dark:text-zinc-300'
                }`}
              >
                <FileText className="h-4 w-4 shrink-0 text-zinc-400" />
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium">{r.item.title}</span>
                  <span className="block truncate text-xs text-zinc-400">
                    {r.item.section} · {r.item.description}
                  </span>
                </span>
                {i === selected && <CornerDownLeft className="h-3.5 w-3.5 shrink-0 text-zinc-400" />}
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
