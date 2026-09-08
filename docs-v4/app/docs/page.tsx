import Link from 'next/link'
import { notFound } from 'next/navigation'
import { navigation } from '@/lib/navigation'

export const metadata = {
  title: 'Documentation',
  description: 'FlexQuery.NET documentation overview.',
}

export default function DocsIndexPage() {
  const first = navigation[0]?.items[0]
  if (!first) notFound()
  return (
    <div className="mx-auto max-w-3xl px-4 py-24 text-center sm:px-6">
      <h1 className="text-3xl font-bold tracking-tight">Documentation</h1>
      <p className="mt-3 text-zinc-500 dark:text-zinc-400">Start with the overview.</p>
      <Link
        href={`/docs/${first.slug}`}
        className="mt-6 inline-block rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-brand-500"
      >
        Open documentation
      </Link>
    </div>
  )
}
