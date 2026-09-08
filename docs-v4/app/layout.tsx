import type { Metadata } from 'next'
import { ThemeProvider } from 'next-themes'
import { SiteHeader } from '@/components/site-header'
import { SiteFooter } from '@/components/site-footer'
import { SearchProvider } from '@/components/search-provider'
import './globals.css'

export const metadata: Metadata = {
  metadataBase: new URL('https://flexquery.net'),
  title: {
    default: 'FlexQuery.NET — Dynamic querying for .NET APIs',
    template: '%s · FlexQuery.NET',
  },
  description:
    'Dynamic filtering, sorting, paging, projection, and aggregates for IQueryable in .NET. Secure, server-side, and translated to SQL via expression trees.',
  openGraph: {
    title: 'FlexQuery.NET — Dynamic querying for .NET APIs',
    description:
      'Dynamic filtering, sorting, paging, projection, and aggregates for IQueryable in .NET.',
    type: 'website',
  },
}

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
          <SearchProvider>
            <a
              href="#main"
              className="sr-only focus:not-sr-only focus:absolute focus:top-3 focus:left-3 focus:z-100 focus:rounded-md focus:bg-brand-600 focus:px-4 focus:py-2 focus:text-sm focus:font-medium focus:text-white"
            >
              Skip to content
            </a>
            <SiteHeader />
            <main id="main">{children}</main>
            <SiteFooter />
          </SearchProvider>
        </ThemeProvider>
      </body>
    </html>
  )
}
