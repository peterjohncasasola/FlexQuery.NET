'use client'

import { useEffect } from 'react'
import { Search } from 'lucide-react'
import { useSearch } from '@/components/search-provider'

export function SearchButton() {
  const { setOpen } = useSearch()

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault()
        setOpen(true)
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [setOpen])

  return (
    <button
      type="button"
      onClick={() => setOpen(true)}
      className="flex h-9 items-center gap-2 rounded-md border border-zinc-200 bg-zinc-50 px-2.5 text-sm text-zinc-500 transition-colors hover:border-zinc-300 hover:text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400 dark:hover:border-zinc-700 dark:hover:text-zinc-200"
      aria-label="Search documentation"
    >
      <Search className="h-4 w-4" />
      <span className="hidden sm:inline">Search</span>
      <kbd className="hidden rounded border border-zinc-300 bg-white px-1 font-mono text-[10px] text-zinc-500 sm:inline dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-400">
        ⌘K
      </kbd>
    </button>
  )
}
