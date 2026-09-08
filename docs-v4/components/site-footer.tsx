export function SiteFooter() {
  return (
    <footer className="border-t border-zinc-200 dark:border-zinc-800">
      <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
        <div className="flex flex-col items-start justify-between gap-6 sm:flex-row sm:items-center">
          <div className="flex items-center gap-2.5 text-sm font-semibold">
            <span className="flex h-6 w-6 items-center justify-center rounded-md bg-gradient-to-br from-brand-500 to-accent-500 text-xs font-bold text-white">
              F
            </span>
            FlexQuery<span className="-ml-1.5 text-brand-600 dark:text-brand-400">.NET</span>
          </div>
          <nav aria-label="Footer" className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-zinc-500 dark:text-zinc-400">
            <a href="https://www.nuget.org/packages/FlexQuery.NET" target="_blank" rel="noopener noreferrer" className="hover:text-zinc-900 dark:hover:text-white">
              NuGet
            </a>
            <a href="https://github.com/peterjohncasasola/FlexQuery.NET" target="_blank" rel="noopener noreferrer" className="hover:text-zinc-900 dark:hover:text-white">
              GitHub
            </a>
            <a href="/docs" className="hover:text-zinc-900 dark:hover:text-white">
              Documentation
            </a>
            <a href="/docs/migration/v3-to-v4" className="hover:text-zinc-900 dark:hover:text-white">
              Migration
            </a>
          </nav>
          <p className="text-xs text-zinc-400 dark:text-zinc-500">MIT Licensed · v4.0</p>
        </div>
      </div>
    </footer>
  )
}
