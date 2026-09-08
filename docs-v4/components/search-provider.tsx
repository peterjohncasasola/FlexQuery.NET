'use client'

import { createContext, useContext, useState, type ReactNode } from 'react'

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
  return <SearchContext.Provider value={{ open, setOpen }}>{children}</SearchContext.Provider>
}
