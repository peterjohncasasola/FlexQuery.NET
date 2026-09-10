'use client'

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

interface SearchContextValue {
  open: boolean
  setOpen: (open: boolean) => void
}

const SearchContext = createContext<SearchContextValue>({ open: false, setOpen: () => {} })

export function useSearch() {
  return useContext(SearchContext)
}

export function SearchProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false)

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault()
        setOpen(true)
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])

  const updateOpen = useCallback((nextOpen: boolean) => setOpen(nextOpen), [])
  const value = useMemo(() => ({ open, setOpen: updateOpen }), [open, updateOpen])

  return <SearchContext.Provider value={value}>{children}</SearchContext.Provider>
}
