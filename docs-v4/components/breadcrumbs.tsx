import Link from 'next/link'
import { ChevronRight } from 'lucide-react'

export function Breadcrumbs({ group, title }: { group: string | null; title: string }) {
  return (
    <nav aria-label="Breadcrumb" className="mb-4 flex items-center gap-1.5 text-sm text-zinc-500 dark:text-zinc-400">
      <Link href="/docs" className="hover:text-zinc-900 dark:hover:text-white">
        Docs
      </Link>
      {group && (
        <>
          <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
          <span>{group}</span>
        </>
      )}
      <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
      <span aria-current="page" className="truncate font-medium text-zinc-900 dark:text-white">{title}</span>
    </nav>
  )
}
