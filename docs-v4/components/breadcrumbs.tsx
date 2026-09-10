import Link from 'next/link'
import { ChevronRight } from 'lucide-react'

export function Breadcrumbs({ group, title }: { group: string | null; title: string }) {
  return (
    <nav
      aria-label="Breadcrumb"
      className="mb-5 flex items-center gap-2 text-[13px] leading-5 text-zinc-500 dark:text-zinc-400"
    >
      <Link
        href="/docs"
        className="rounded-sm transition-colors hover:text-zinc-900 dark:hover:text-white"
      >
        Docs
      </Link>
      {group && (
        <>
          <ChevronRight className="h-3.5 w-3.5 shrink-0 text-zinc-300 dark:text-zinc-600" aria-hidden="true" />
          <span className="truncate">{group}</span>
        </>
      )}
      <ChevronRight className="h-3.5 w-3.5 shrink-0 text-zinc-300 dark:text-zinc-600" aria-hidden="true" />
      <span aria-current="page" className="min-w-0 truncate font-medium text-zinc-900 dark:text-white">
        {title}
      </span>
    </nav>
  )
}
