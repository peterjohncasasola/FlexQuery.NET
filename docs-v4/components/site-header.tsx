import Link from 'next/link'
import { ThemeToggle } from '@/components/theme-toggle'
import { SearchButton } from '@/components/search-button'
import { MobileNav } from '@/components/mobile-nav'

const links = [
  { href: '/docs', label: 'Docs' },
  { href: '/docs/guides/filtering', label: 'Guides' },
  { href: '/docs/api-reference', label: 'API Reference' },
  { href: '/docs/migration/v3-to-v4', label: 'v3 → v4' },
]

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-50 border-b border-zinc-200 bg-white/80 backdrop-blur-md dark:border-zinc-800 dark:bg-zinc-950/80">
      <div className="mx-auto flex h-16 max-w-7xl items-center gap-4 px-4 sm:px-6">
        <Link href="/" className="flex items-center gap-2.5 font-semibold tracking-tight">
          <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-gradient-to-br from-brand-500 to-accent-500 text-sm font-bold text-white">
            F
          </span>
          <span className="text-[15px] text-zinc-900 dark:text-white">
            FlexQuery<span className="text-brand-600 dark:text-brand-400">.NET</span>
          </span>
        </Link>
        <nav aria-label="Main" className="hidden items-center gap-1 text-sm md:flex">
          {links.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className="rounded-md px-3 py-1.5 text-zinc-600 transition-colors hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800/60 dark:hover:text-white"
            >
              {l.label}
            </Link>
          ))}
        </nav>
        <div className="ml-auto flex items-center gap-1.5">
          <SearchButton />
          <a
            href="https://www.nuget.org/packages/FlexQuery.NET"
            target="_blank"
            rel="noopener noreferrer"
            className="hidden rounded-md px-3 py-1.5 text-sm text-zinc-600 transition-colors hover:bg-zinc-100 hover:text-zinc-900 sm:block dark:text-zinc-400 dark:hover:bg-zinc-800/60 dark:hover:text-white"
          >
            NuGet
          </a>
          <ThemeToggle />
          <MobileNav links={links} />
        </div>
      </div>
    </header>
  )
}
