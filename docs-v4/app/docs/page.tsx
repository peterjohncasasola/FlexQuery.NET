import Link from 'next/link'
import { ArrowRight, BookOpen, Braces, RotateCcw } from 'lucide-react'
import { SearchButton } from '@/components/search-button'
import { navigation } from '@/lib/navigation'

export const metadata = {
  title: 'Documentation',
  description: 'Learn how to build secure, composable query APIs with FlexQuery.NET.',
}

const startingPoints = [
  {
    icon: BookOpen,
    title: 'Build your first query',
    description: 'Install the EF Core provider and ship a working query endpoint.',
    href: '/docs/getting-started/first-query',
    meta: '10 min',
  },
  {
    icon: Braces,
    title: 'Understand the query model',
    description: 'See how filters, sorting, projection, paging, and includes fit together.',
    href: '/docs/concepts/query-options',
    meta: 'Core concept',
  },
  {
    icon: RotateCcw,
    title: 'Migrate from v3',
    description: 'Review breaking changes and move an existing integration to v4.',
    href: '/docs/migration/v3-to-v4',
    meta: 'Migration guide',
  },
]

export default function DocsIndexPage() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-14 sm:px-6 sm:py-18">
      <div className="grid gap-10 border-b border-zinc-200 pb-12 lg:grid-cols-[minmax(0,1fr)_22rem] lg:items-end dark:border-zinc-800">
        <div>
          <p className="mb-3 text-xs font-semibold tracking-[0.12em] text-brand-700 uppercase dark:text-brand-400">
            FlexQuery.NET v4
          </p>
          <h1 className="max-w-3xl text-4xl leading-tight font-bold tracking-[-0.035em] text-zinc-950 sm:text-5xl dark:text-white">
            Documentation
          </h1>
          <p className="mt-4 max-w-2xl text-lg leading-8 text-zinc-600 dark:text-zinc-400">
            Build secure, composable query endpoints for EF Core and Dapper—from the first
            filter to production governance and diagnostics.
          </p>
        </div>
        <div>
          <p className="mb-2 text-xs font-medium text-zinc-500 dark:text-zinc-400">
            Find an API, feature, or error message
          </p>
          <SearchButton fullWidth />
        </div>
      </div>

      <section aria-labelledby="start-heading" className="py-12">
        <div className="grid gap-8 lg:grid-cols-[13rem_minmax(0,1fr)]">
          <div>
            <h2 id="start-heading" className="text-sm font-semibold text-zinc-950 dark:text-white">
              Start here
            </h2>
            <p className="mt-2 text-sm leading-6 text-zinc-500 dark:text-zinc-400">
              Choose the shortest path for what you need to do today.
            </p>
          </div>
          <div className="divide-y divide-zinc-200 border-y border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {startingPoints.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                className="group grid gap-3 py-5 sm:grid-cols-[2rem_minmax(0,1fr)_auto] sm:items-start"
              >
                <item.icon className="mt-0.5 h-5 w-5 text-brand-600 dark:text-brand-400" aria-hidden="true" />
                <span>
                  <span className="block font-semibold text-zinc-950 group-hover:text-brand-700 dark:text-white dark:group-hover:text-brand-300">
                    {item.title}
                  </span>
                  <span className="mt-1 block text-sm leading-6 text-zinc-500 dark:text-zinc-400">
                    {item.description}
                  </span>
                </span>
                <span className="hidden items-center gap-3 text-xs text-zinc-400 sm:flex dark:text-zinc-500">
                  {item.meta}
                  <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5 group-hover:text-brand-500" aria-hidden="true" />
                </span>
              </Link>
            ))}
          </div>
        </div>
      </section>

      <section aria-labelledby="browse-heading" className="border-t border-zinc-200 pt-12 dark:border-zinc-800">
        <div className="grid gap-8 lg:grid-cols-[13rem_minmax(0,1fr)]">
          <div>
            <h2 id="browse-heading" className="text-sm font-semibold text-zinc-950 dark:text-white">
              Browse the docs
            </h2>
            <p className="mt-2 text-sm leading-6 text-zinc-500 dark:text-zinc-400">
              The complete v4 documentation, organized by task and system area.
            </p>
          </div>
          <div className="grid gap-x-10 gap-y-10 sm:grid-cols-2">
            {navigation.map((group) => (
              <section key={group.title} aria-labelledby={`docs-group-${group.title.replaceAll(' ', '-').toLowerCase()}`}>
                <h3
                  id={`docs-group-${group.title.replaceAll(' ', '-').toLowerCase()}`}
                  className="border-b border-zinc-200 pb-2 text-xs font-semibold tracking-[0.08em] text-zinc-500 uppercase dark:border-zinc-800 dark:text-zinc-500"
                >
                  {group.title}
                </h3>
                <ul className="mt-2">
                  {group.items.map((item) => (
                    <li key={item.slug}>
                      <Link
                        href={`/docs/${item.slug}`}
                        className="group flex items-center justify-between gap-3 rounded-md px-2 py-2 text-sm text-zinc-700 transition-colors hover:bg-zinc-50 hover:text-brand-700 dark:text-zinc-300 dark:hover:bg-zinc-900/70 dark:hover:text-brand-300"
                      >
                        {item.title}
                        <ArrowRight className="h-3.5 w-3.5 shrink-0 -translate-x-1 text-transparent transition-all group-hover:translate-x-0 group-hover:text-brand-500" aria-hidden="true" />
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
        </div>
      </section>
    </div>
  )
}
