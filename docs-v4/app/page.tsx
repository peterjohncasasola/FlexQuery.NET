import Link from 'next/link'
import {
  ArrowRight,
  Filter,
  ArrowDownWideNarrow,
  Layers,
  GitBranch,
  BarChart3,
  ShieldCheck,
  KeyRound,
  Stethoscope,
  Boxes,
  Plug,
  FileCode2,
  Database,
  Zap,
  MousePointerClick,
} from 'lucide-react'

const capabilities = [
  {
    icon: Filter,
    title: 'Dynamic Filtering',
    description: '18 operators, nested groups, collection filters — translated to SQL expression trees, never client-evaluated.',
    href: '/docs/guides/filtering',
  },
  {
    icon: ArrowDownWideNarrow,
    title: 'Multi-column Sorting',
    description: 'Colon and space form directions, navigation paths, default sorts, and aggregate sorts.',
    href: '/docs/guides/sorting',
  },
  {
    icon: Layers,
    title: 'Server-side Projection',
    description: 'Select only what you need — flat, nested, aliased, and DTO-shaped output with enforced result surfaces.',
    href: '/docs/guides/projection',
  },
  {
    icon: GitBranch,
    title: 'Deep Expand Trees',
    description: 'Load navigations with per-branch filter, sort, and take — hydrated via split queries.',
    href: '/docs/guides/expand',
  },
  {
    icon: BarChart3,
    title: 'Grouping & Aggregates',
    description: 'GROUP BY with sum, count, avg, min, max — and HAVING with a full expression tree.',
    href: '/docs/guides/grouping',
  },
  {
    icon: KeyRound,
    title: 'Keyset Pagination',
    description: 'Seek predicates instead of OFFSET for large datasets, with opaque, versioned cursors.',
    href: '/docs/guides/keyset-pagination',
  },
  {
    icon: ShieldCheck,
    title: 'Security & Governance',
    description: 'Allowed/blocked fields, per-operation sets, operator allow-lists, role-based access.',
    href: '/docs/security',
  },
  {
    icon: Stethoscope,
    title: 'Diagnostics',
    description: 'Pipeline events, timing reports, SQL previews, and copy-paste-ready Dapper SQL logs.',
    href: '/docs/diagnostics',
  },
]

const providers = [
  { icon: Database, name: 'Entity Framework Core', href: '/docs/providers/ef-core' },
  { icon: Database, name: 'Dapper', href: '/docs/providers/dapper' },
  { icon: MousePointerClick, name: 'AG Grid', href: '/docs/integrations/ag-grid' },
  { icon: MousePointerClick, name: 'Kendo UI', href: '/docs/integrations/kendo' },
  { icon: FileCode2, name: 'OpenAPI / Swagger', href: '/docs/integrations/openapi' },
  { icon: Plug, name: 'ASP.NET Core', href: '/docs/integrations/aspnetcore' },
]

const steps = [
  {
    title: 'Install',
    body: 'Add the core package and your provider package from NuGet.',
    code: 'dotnet add package FlexQuery.NET.EntityFrameworkCore',
  },
  {
    title: 'Configure',
    body: 'Set global defaults once at startup — then never think about them again.',
    code: 'FlexQueryCore.Configure(options =>\n{\n    options.DefaultPageSize = 20;\n    options.MaxPageSize = 1000;\n});',
  },
  {
    title: 'Query',
    body: 'Bind FlexQueryParameters and execute. That is the whole endpoint.',
    code: 'var result = await db.Customers\n    .AsNoTracking()\n    .FlexQueryAsync(parameters,\n        cancellationToken: ct);',
  },
]

export default function Home() {
  return (
    <div>
      {/* Hero */}
      <section className="relative overflow-hidden border-b border-zinc-200 dark:border-zinc-800">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 bg-[radial-gradient(60%_50%_at_50%_0%,rgba(99,102,241,0.12),transparent)] dark:bg-[radial-gradient(60%_50%_at_50%_0%,rgba(99,102,241,0.22),transparent)]"
        />
        <div className="relative mx-auto max-w-7xl px-4 py-24 text-center sm:px-6 lg:py-32">
          <div className="mx-auto mb-6 inline-flex items-center gap-2 rounded-full border border-brand-200 bg-brand-50 px-3.5 py-1 text-xs font-medium text-brand-700 dark:border-brand-800 dark:bg-brand-950/50 dark:text-brand-300">
            <Zap className="h-3.5 w-3.5" />
            v4 — typed DTOs, expand trees, keyset pagination
          </div>
          <h1 className="mx-auto max-w-3xl text-4xl font-bold tracking-tight text-balance sm:text-6xl">
            Dynamic querying for{' '}
            <span className="bg-gradient-to-r from-brand-500 to-accent-500 bg-clip-text text-transparent">
              .NET APIs
            </span>
          </h1>
          <p className="mx-auto mt-5 max-w-2xl text-lg leading-8 text-zinc-600 text-pretty dark:text-zinc-400">
            FlexQuery.NET turns query parameters into secure, server-side expression trees —
            filtering, sorting, paging, projection, and aggregates in a single line, with
            EF Core or Dapper.
          </p>
          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Link
              href="/docs/getting-started/first-query"
              className="inline-flex items-center gap-2 rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-brand-500"
            >
              Get started <ArrowRight className="h-4 w-4" />
            </Link>
            <Link
              href="/docs"
              className="inline-flex items-center gap-2 rounded-lg border border-zinc-300 px-5 py-2.5 text-sm font-semibold text-zinc-700 transition-colors hover:border-zinc-400 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-900"
            >
              Read the docs
            </Link>
          </div>
          <div className="mx-auto mt-10 max-w-2xl overflow-hidden rounded-xl border border-zinc-200 text-left shadow-sm dark:border-zinc-800">
            <div className="flex items-center gap-1.5 border-b border-zinc-200 bg-zinc-50 px-4 py-2 dark:border-zinc-800 dark:bg-zinc-900">
              <span className="h-2.5 w-2.5 rounded-full bg-red-400" />
              <span className="h-2.5 w-2.5 rounded-full bg-amber-400" />
              <span className="h-2.5 w-2.5 rounded-full bg-emerald-400" />
              <span className="ml-2 font-mono text-xs text-zinc-500">HTTP</span>
            </div>
            <pre className="overflow-x-auto bg-white p-4 font-mono text-[13px] leading-6 dark:bg-zinc-950">
              <code>{`GET /api/customers?filter=Status:eq:Active&sort=LastName:asc&select=Id,FirstName,Email&page=1&pageSize=20`}</code>
            </pre>
          </div>
        </div>
      </section>

      {/* Capabilities */}
      <section className="mx-auto max-w-7xl px-4 py-20 sm:px-6">
        <div className="mb-12 text-center">
          <h2 className="text-3xl font-bold tracking-tight">Everything a query API needs</h2>
          <p className="mx-auto mt-3 max-w-xl text-zinc-600 dark:text-zinc-400">
            One pipeline from query string to SQL — validated, governed, and observable.
          </p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {capabilities.map((c) => (
            <Link
              key={c.title}
              href={c.href}
              className="group rounded-xl border border-zinc-200 p-5 transition-all hover:border-brand-400 hover:shadow-sm dark:border-zinc-800 dark:hover:border-brand-600"
            >
              <c.icon className="h-5 w-5 text-brand-500" />
              <h3 className="mt-3 font-semibold text-zinc-900 dark:text-white">{c.title}</h3>
              <p className="mt-1.5 text-sm leading-6 text-zinc-600 dark:text-zinc-400">{c.description}</p>
            </Link>
          ))}
        </div>
      </section>

      {/* Quick start */}
      <section className="border-y border-zinc-200 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900/40">
        <div className="mx-auto max-w-7xl px-4 py-20 sm:px-6">
          <div className="mb-12 text-center">
            <h2 className="text-3xl font-bold tracking-tight">Up and running in minutes</h2>
            <p className="mx-auto mt-3 max-w-xl text-zinc-600 dark:text-zinc-400">
              Three steps from NuGet to a production-grade query endpoint.
            </p>
          </div>
          <div className="grid gap-6 lg:grid-cols-3">
            {steps.map((s, i) => (
              <div key={s.title} className="relative rounded-xl border border-zinc-200 bg-white p-6 dark:border-zinc-800 dark:bg-zinc-950">
                <span className="absolute -top-3 left-6 flex h-6 w-6 items-center justify-center rounded-full bg-brand-600 text-xs font-bold text-white">
                  {i + 1}
                </span>
                <h3 className="font-semibold text-zinc-900 dark:text-white">{s.title}</h3>
                <p className="mt-1.5 text-sm text-zinc-600 dark:text-zinc-400">{s.body}</p>
                <pre className="mt-4 overflow-x-auto rounded-lg border border-zinc-200 bg-zinc-50 p-3 font-mono text-xs leading-5 dark:border-zinc-800 dark:bg-zinc-900">
                  <code>{s.code}</code>
                </pre>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Providers & integrations */}
      <section className="mx-auto max-w-7xl px-4 py-20 sm:px-6">
        <div className="mb-12 text-center">
          <h2 className="text-3xl font-bold tracking-tight">Providers & integrations</h2>
          <p className="mx-auto mt-3 max-w-xl text-zinc-600 dark:text-zinc-400">
            Works with your stack — data access, UI grids, and API documentation.
          </p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {providers.map((p) => (
            <Link
              key={p.name}
              href={p.href}
              className="group flex items-center gap-3 rounded-xl border border-zinc-200 p-4 transition-all hover:border-brand-400 dark:border-zinc-800 dark:hover:border-brand-600"
            >
              <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-50 text-brand-600 dark:bg-brand-950/60 dark:text-brand-400">
                <p.icon className="h-4.5 w-4.5" />
              </span>
              <span className="font-medium text-zinc-900 dark:text-white">{p.name}</span>
              <ArrowRight className="ml-auto h-4 w-4 text-zinc-300 transition-transform group-hover:translate-x-0.5 group-hover:text-brand-500 dark:text-zinc-700" />
            </Link>
          ))}
        </div>
      </section>

      {/* Bottom CTA */}
      <section className="mx-auto max-w-7xl px-4 pb-24 sm:px-6">
        <div className="rounded-2xl bg-gradient-to-br from-brand-600 to-accent-600 px-8 py-14 text-center">
          <Boxes className="mx-auto h-8 w-8 text-white/80" />
          <h2 className="mt-4 text-2xl font-bold tracking-tight text-white sm:text-3xl">
            Build your first query in five minutes
          </h2>
          <p className="mx-auto mt-3 max-w-lg text-white/80">
            Follow the quick-start guide and ship a dynamic query endpoint today.
          </p>
          <Link
            href="/docs/getting-started/first-query"
            className="mt-6 inline-flex items-center gap-2 rounded-lg bg-white px-5 py-2.5 text-sm font-semibold text-brand-700 shadow-sm transition-colors hover:bg-brand-50"
          >
            Start building <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      </section>
    </div>
  )
}
