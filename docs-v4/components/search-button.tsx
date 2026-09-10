'use client'

import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import { useSearch } from '@/components/search-provider'

export function SearchButton({
  fullWidth = false,
  onOpen,
}: {
  fullWidth?: boolean
  onOpen?: () => void
}) {
  const { setOpen } = useSearch()
  const [shortcut, setShortcut] = useState('Ctrl K')

  useEffect(() => {
    if (/Mac|iPhone|iPad/.test(navigator.platform)) setShortcut('⌘ K')
  }, [])

  return (
    <button
      type="button"
      onClick={() => {
        onOpen?.()
        setOpen(true)
      }}
      className={`flex h-9 items-center gap-2 rounded-md border border-zinc-200 bg-zinc-50 px-2.5 text-sm text-zinc-500 transition-colors hover:border-zinc-300 hover:bg-white hover:text-zinc-800 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400 dark:hover:border-zinc-700 dark:hover:bg-zinc-900 dark:hover:text-zinc-100 ${
        fullWidth ? 'w-full justify-start' : ''
      }`}
      aria-label="Search documentation"
    >
      <Search className="h-4 w-4" />
      <span className={fullWidth ? '' : 'hidden sm:inline'}>Search docs</span>
      <kbd className={`${fullWidth ? 'ml-auto' : 'hidden sm:inline'} rounded border border-zinc-300 bg-white px-1.5 py-0.5 font-mono text-[10px] text-zinc-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-400`}>
        {shortcut}
      </kbd>
    </button>
  )
}
