import Link from 'next/link'
import { ArrowLeft, ArrowRight } from 'lucide-react'
import type { NavItem } from '@/lib/navigation'

export function PagerNav({ prev, next }: { prev: NavItem | null; next: NavItem | null }) {
  if (!prev && !next) return null
  return (
    <nav aria-label="Pagination" className="mt-14 grid grid-cols-1 gap-4 border-t border-zinc-200 pt-8 sm:grid-cols-2 dark:border-zinc-800">
      {prev ? (
        <Link
          href={`/docs/${prev.slug}`}
          className="group rounded-xl border border-zinc-200 p-4 transition-colors hover:border-brand-400 dark:border-zinc-800 dark:hover:border-brand-600"
        >
          <span className="flex items-center gap-1.5 text-xs text-zinc-500 dark:text-zinc-400">
            <ArrowLeft className="h-3.5 w-3.5" /> Previous
          </span>
          <span className="mt-1 block text-sm font-medium text-zinc-900 group-hover:text-brand-700 dark:text-white dark:group-hover:text-brand-300">
            {prev.title}
          </span>
        </Link>
      ) : (
        <span />
      )}
      {next && (
        <Link
          href={`/docs/${next.slug}`}
          className="group rounded-xl border border-zinc-200 p-4 text-right transition-colors hover:border-brand-400 sm:col-start-2 dark:border-zinc-800 dark:hover:border-brand-600"
        >
          <span className="flex items-center justify-end gap-1.5 text-xs text-zinc-500 dark:text-zinc-400">
            Next <ArrowRight className="h-3.5 w-3.5" />
          </span>
          <span className="mt-1 block text-sm font-medium text-zinc-900 group-hover:text-brand-700 dark:text-white dark:group-hover:text-brand-300">
            {next.title}
          </span>
        </Link>
      )}
    </nav>
  )
}
