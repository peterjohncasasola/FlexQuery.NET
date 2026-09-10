'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { ThemeToggle } from '@/components/theme-toggle'
import { SearchButton } from '@/components/search-button'
import { MobileNav } from '@/components/mobile-nav'
import { BrandMark } from '@/components/brand-mark'

const links = [
  { href: '/docs', label: 'Docs' },
  { href: '/docs/guides/filtering', label: 'Guides' },
  { href: '/docs/api-reference', label: 'API Reference' },
  { href: '/docs/migration/v3-to-v4', label: 'Migration' },
]

export function SiteHeader() {
  const pathname = usePathname()
  const isDocs = pathname === '/docs' || pathname.startsWith('/docs/')

  return (
    <header className="sticky top-0 z-50 border-b border-zinc-200/80 bg-white/90 backdrop-blur-md dark:border-zinc-800 dark:bg-zinc-950/90">
      <div
        className={`mx-auto flex h-14 items-center gap-4 px-4 sm:px-6 lg:px-8 ${
          isDocs ? 'max-w-[112rem]' : 'max-w-7xl'
        }`}
      >
        <Link href="/" className="flex items-center gap-2.5 font-semibold tracking-tight" aria-label="FlexQuery.NET home">
          <BrandMark />
          <span className="text-[15px] text-zinc-900 dark:text-white">
            FlexQuery<span className="text-brand-600 dark:text-brand-400">.NET</span>
          </span>
          <span className="hidden border-l border-zinc-200 pl-2.5 text-xs font-medium text-zinc-500 sm:inline dark:border-zinc-800 dark:text-zinc-400">
            v4
          </span>
        </Link>
        <nav aria-label="Main" className="hidden items-center gap-1 text-sm lg:flex">
          {links.map((link) => {
            const isSpecializedDocsRoute =
              pathname.startsWith('/docs/guides/') ||
              pathname === '/docs/api-reference' ||
              pathname.startsWith('/docs/migration/')
            const active =
              pathname === link.href ||
              (link.href === '/docs/guides/filtering' && pathname.startsWith('/docs/guides/')) ||
              (link.href === '/docs/migration/v3-to-v4' && pathname.startsWith('/docs/migration/')) ||
              (link.href === '/docs' && pathname.startsWith('/docs/') && !isSpecializedDocsRoute)
            return (
              <Link
                key={link.href}
                href={link.href}
                aria-current={active ? 'page' : undefined}
                className={`rounded-md px-3 py-1.5 transition-colors ${
                  active
                    ? 'bg-zinc-100 font-medium text-zinc-950 dark:bg-zinc-900 dark:text-white'
                    : 'text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-900 dark:hover:text-white'
                }`}
              >
                {link.label}
              </Link>
            )
          })}
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
          <a
            href="https://github.com/peterjohncasasola/FlexQuery.NET"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="FlexQuery.NET on GitHub"
            title="GitHub"
            className="hidden h-9 w-9 items-center justify-center rounded-md text-zinc-600 transition-colors hover:bg-zinc-100 hover:text-zinc-950 sm:flex dark:text-zinc-400 dark:hover:bg-zinc-900 dark:hover:text-white"
          >
            <svg viewBox="0 0 24 24" className="h-4 w-4 fill-current" aria-hidden="true">
              <path d="M12 .7a11.5 11.5 0 0 0-3.64 22.4c.58.1.79-.25.79-.56v-2.23c-3.22.7-3.9-1.37-3.9-1.37-.53-1.34-1.29-1.7-1.29-1.7-1.05-.72.08-.71.08-.71 1.17.08 1.78 1.2 1.78 1.2 1.04 1.77 2.72 1.26 3.38.96.1-.75.4-1.26.74-1.55-2.57-.3-5.27-1.29-5.27-5.69 0-1.26.45-2.28 1.19-3.09-.12-.29-.52-1.46.11-3.05 0 0 .97-.31 3.16 1.18a10.9 10.9 0 0 1 5.76 0c2.19-1.49 3.16-1.18 3.16-1.18.63 1.59.23 2.76.11 3.05.74.81 1.19 1.83 1.19 3.09 0 4.41-2.7 5.39-5.28 5.68.42.36.78 1.06.78 2.14v3.26c0 .31.21.67.8.56A11.5 11.5 0 0 0 12 .7Z" />
            </svg>
          </a>
          <ThemeToggle />
          <MobileNav links={links} />
        </div>
      </div>
    </header>
  )
}
