import Link from 'next/link'
import { BookOpen, ArrowRight } from 'lucide-react'

export interface RelatedLink {
  title: string
  slug: string
  description: string
}

export function RelatedLinks({ links }: { links: RelatedLink[] }) {
  return (
    <section className="mt-14">
      <h2 className="flex items-center gap-2 text-sm font-semibold tracking-wide text-zinc-900 uppercase dark:text-white">
        <BookOpen className="h-4 w-4 text-brand-500" />
        Related documentation
      </h2>
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        {links.map((l) => (
          <Link
            key={l.slug}
            href={`/docs/${l.slug}`}
            className="group rounded-xl border border-zinc-200 p-4 transition-colors hover:border-brand-400 dark:border-zinc-800 dark:hover:border-brand-600"
          >
            <span className="flex items-center justify-between font-medium text-zinc-900 dark:text-white">
              {l.title}
              <ArrowRight className="h-4 w-4 text-zinc-400 transition-transform group-hover:translate-x-0.5 group-hover:text-brand-500" />
            </span>
            <span className="mt-1 block text-sm text-zinc-500 dark:text-zinc-400">{l.description}</span>
          </Link>
        ))}
      </div>
    </section>
  )
}
