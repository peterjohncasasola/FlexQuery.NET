import Link from 'next/link'
import {
  ArrowRight,
  ArrowDown,
  ArrowUpRight,
  Check,
  X,
  Filter,
  ArrowDownWideNarrow,
  Layers,
  GitBranch,
  BarChart3,
  KeyRound,
  ShieldCheck,
  Stethoscope,
  Database,
  FileCode2,
  MousePointerClick,
  Plug,
} from 'lucide-react'
import { CodeBlock } from '@/components/code-block'

const capabilityGroups = [
  {
    label: 'Query',
    description: 'Shape the result set',
    items: [
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
    ],
  },
  {
    label: 'Data',
    description: 'Load and shape relationships',
    items: [
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
    ],
  },
  {
    label: 'Control',
    description: 'Keep it safe and observable',
    items: [
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
    ],
  },
]

const integrationGroups = [
  {
    label: 'Data providers',
    items: [
      { icon: Database, name: 'Entity Framework Core', href: '/docs/providers/ef-core' },
      { icon: Database, name: 'Dapper', href: '/docs/providers/dapper' },
    ],
  },
  {
    label: 'API & platform',
    items: [
      { icon: Plug, name: 'ASP.NET Core', href: '/docs/integrations/aspnetcore' },
      { icon: FileCode2, name: 'OpenAPI / Swagger', href: '/docs/integrations/openapi' },
    ],
  },
  {
    label: 'UI data grids',
    items: [
      { icon: MousePointerClick, name: 'AG Grid', href: '/docs/integrations/ag-grid' },
      { icon: MousePointerClick, name: 'Kendo UI', href: '/docs/integrations/kendo' },
    ],
  },
]

const steps = [
  {
    title: 'Install',
    body: 'Add the core package and your provider package from NuGet.',
    language: 'bash',
    code: 'dotnet add package FlexQuery.NET.EntityFrameworkCore',
  },
  {
    title: 'Configure',
    body: 'Set global defaults once at startup - then never think about them again.',
    language: 'csharp',
    code: 'FlexQueryCore.Configure(options =>\n{\n    options.DefaultPageSize = 20;\n    options.MaxPageSize = 1000;\n});',
  },
  {
    title: 'Query',
    body: 'Bind FlexQueryParameters and execute. That is the whole endpoint.',
    language: 'csharp',
    code: 'var result = await db.Customers\n    .AsNoTracking()\n    .FlexQueryAsync(parameters,\n        cancellationToken: ct);',
  },
]

const pipeline = [
  { step: '01', title: 'HTTP query', detail: '?filter=…&sort=…&page=…' },
  { step: '02', title: 'Parser', detail: 'Query string → query model' },
  { step: '03', title: 'Governance', detail: 'Allow-lists, roles, limits' },
  { step: '04', title: 'Expression tree', detail: 'Typed IQueryable composition' },
  { step: '05', title: 'Provider', detail: 'EF Core or Dapper' },
  { step: '06', title: 'Database', detail: 'Parameterized SQL' },
]

function HeroQuery() {
  return (
    <div className="mx-auto mt-12 max-w-4xl text-left">
      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
        <div className="flex items-center justify-between border-b border-zinc-200 bg-zinc-50 px-4 py-2 dark:border-zinc-800 dark:bg-zinc-900">
          <span className="flex items-center gap-2 font-mono text-[11px] font-medium text-zinc-500 dark:text-zinc-400">
            HTTP
            <span className="rounded bg-emerald-600/10 px-1.5 py-0.5 font-semibold text-emerald-700 dark:bg-emerald-400/10 dark:text-emerald-400">
              GET
            </span>
          </span>
          <Link
            href="/docs/concepts/query-syntax"
            className="group inline-flex items-center gap-1 text-xs font-medium text-zinc-500 transition-colors hover:text-brand-600 dark:text-zinc-400 dark:hover:text-brand-400"
          >
            Query syntax
            <ArrowUpRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5" aria-hidden="true" />
          </Link>
        </div>
        <pre className="overflow-x-auto p-4 font-mono text-[13px] leading-7 sm:p-5 sm:text-sm" tabIndex={0} aria-label="Example FlexQuery HTTP request">
          <code>
            <span className="font-semibold text-emerald-600 dark:text-emerald-400">GET</span>
            {' '}
            <span className="text-zinc-900 dark:text-zinc-100">/api/customers</span>
            {'\n    '}
            <span className="text-zinc-400 dark:text-zinc-600">?</span>
            <span className="text-brand-600 dark:text-brand-400">filter</span>
            <span className="text-zinc-400 dark:text-zinc-600">=</span>
            <span className="text-zinc-500 dark:text-zinc-400">Status:eq:Active</span>
            {'\n    '}
            <span className="text-zinc-400 dark:text-zinc-600">&amp;</span>
            <span className="text-brand-600 dark:text-brand-400">sort</span>
            <span className="text-zinc-400 dark:text-zinc-600">=</span>
            <span className="text-zinc-500 dark:text-zinc-400">LastName:asc</span>
            {'\n    '}
            <span className="text-zinc-400 dark:text-zinc-600">&amp;</span>
            <span className="text-brand-600 dark:text-brand-400">select</span>
            <span className="text-zinc-400 dark:text-zinc-600">=</span>
            <span className="text-zinc-500 dark:text-zinc-400">Id,FirstName,Email</span>
            {'\n    '}
            <span className="text-zinc-400 dark:text-zinc-600">&amp;</span>
            <span className="text-brand-600 dark:text-brand-400">page</span>
            <span className="text-zinc-400 dark:text-zinc-600">=</span>
            <span className="text-zinc-500 dark:text-zinc-400">1</span>
            <span className="text-zinc-400 dark:text-zinc-600">&amp;</span>
            <span className="text-brand-600 dark:text-brand-400">pageSize</span>
            <span className="text-zinc-400 dark:text-zinc-600">=</span>
            <span className="text-zinc-500 dark:text-zinc-400">20</span>
          </code>
        </pre>
      </div>
      <p className="mt-3 text-center font-mono text-xs text-zinc-400 dark:text-zinc-600">
        query parameters → FlexQuery pipeline → SQL expression tree
      </p>
    </div>
  )
}

export default function Home() {
  return (
    <div>
      {/* Hero */}
      <section className="border-b border-zinc-200 dark:border-zinc-800">
        <div className="mx-auto max-w-7xl px-4 pt-14 pb-16 text-center sm:px-6 lg:pt-20 lg:pb-20">
          <div className="mb-5 inline-flex items-center gap-2 text-xs font-semibold tracking-[0.08em] text-brand-700 uppercase dark:text-brand-400">
            v4 — typed DTOs, expand trees, keyset pagination
          </div>
          <h1 className="mx-auto max-w-3xl text-4xl font-bold tracking-tight text-balance sm:text-6xl">
            Dynamic querying for{' '}
            <span className="text-brand-600 dark:text-brand-400">.NET APIs</span>
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
          <HeroQuery />
        </div>
      </section>

      {/* Quick start */}
      <section className="border-b border-zinc-200 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900/40">
        <div className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
          <div className="mb-10 text-center">
            <h2 className="text-3xl font-bold tracking-tight">Up and running in minutes</h2>
            <p className="mx-auto mt-3 max-w-xl text-zinc-600 dark:text-zinc-400">
              Three steps from NuGet to a production-grade query endpoint.
            </p>
          </div>
          <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
            {steps.map((s, i) => (
              <div key={s.title} className="flex min-w-0 flex-col rounded-lg border border-zinc-200 bg-white p-5 dark:border-zinc-800 dark:bg-zinc-950">
                <div className="flex items-center gap-2.5">
                  <span className="flex h-6 w-6 items-center justify-center rounded-md bg-brand-600 font-mono text-xs font-bold text-white">
                    {i + 1}
                  </span>
                  <h3 className="font-semibold text-zinc-900 dark:text-white">{s.title}</h3>
                </div>
                <p className="mt-2 text-sm leading-6 text-zinc-600 dark:text-zinc-400">{s.body}</p>
                <div className="mt-4 [&>div]:my-0 flex-1">
                  <CodeBlock data-language={s.language}>
                    <code>{s.code}</code>
                  </CodeBlock>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Capabilities, grouped by role in the pipeline */}
      <section className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
        <div className="mb-10 text-center">
          <h2 className="text-3xl font-bold tracking-tight">Everything a query API needs</h2>
          <p className="mx-auto mt-3 max-w-xl text-zinc-600 dark:text-zinc-400">
            One pipeline from query string to SQL — validated, governed, and observable.
          </p>
        </div>
        <div className="grid gap-x-10 gap-y-12 lg:grid-cols-3">
          {capabilityGroups.map((group) => (
            <div key={group.label}>
              <div className="flex items-baseline gap-2.5 border-b border-zinc-200 pb-2.5 dark:border-zinc-800">
                <h3 className="font-mono text-xs font-semibold tracking-[0.1em] text-brand-700 uppercase dark:text-brand-400">
                  {group.label}
                </h3>
                <span className="text-xs text-zinc-400 dark:text-zinc-500">{group.description}</span>
              </div>
              <div>
                {group.items.map((item) => (
                  <Link
                    key={item.title}
                    href={item.href}
                    className="group border-t border-zinc-200 py-4 transition-colors first:border-t-0 hover:border-brand-500 dark:border-zinc-800 dark:first:border-t-0 dark:hover:border-brand-600"
                  >
                    <div className="flex items-center gap-2">
                      <item.icon className="h-4 w-4 shrink-0 text-brand-500 dark:text-brand-400" aria-hidden="true" />
                      <h4 className="font-semibold text-zinc-900 dark:text-white">{item.title}</h4>
                      <ArrowRight
                        className="ml-auto h-3.5 w-3.5 -translate-x-1 text-transparent transition-all group-hover:translate-x-0 group-hover:text-brand-500"
                        aria-hidden="true"
                      />
                    </div>
                    <p className="mt-1 text-sm leading-6 text-zinc-600 dark:text-zinc-400">{item.description}</p>
                  </Link>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Why FlexQuery */}
      <section className="border-y border-zinc-200 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900/40">
        <div className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
          <div className="mb-10 max-w-2xl">
            <h2 className="text-3xl font-bold tracking-tight">Why FlexQuery?</h2>
            <p className="mt-3 text-zinc-600 dark:text-zinc-400">
              Every list endpoint needs filtering, sorting, paging, projection, and include logic —
              plus validation for all of it. FlexQuery centralizes that repetition into one governed pipeline.
            </p>
          </div>
          <div className="grid gap-6 lg:grid-cols-2">
            <div className="min-w-0 rounded-lg border border-zinc-200 bg-white p-6 dark:border-zinc-800 dark:bg-zinc-950">
              <h3 className="flex items-center gap-2 text-xs font-semibold tracking-[0.1em] text-zinc-500 uppercase dark:text-zinc-400">
                <X className="h-3.5 w-3.5 text-zinc-400" aria-hidden="true" />
                Without FlexQuery
              </h3>
              <ul className="mt-4 space-y-2.5 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                {[
                  'Custom filter parsing in every endpoint',
                  'Ad-hoc sorting and pagination logic',
                  'Projection and include code repeated per feature',
                  'Validation re-implemented — or forgotten',
                  'Multiple endpoints, or one overloaded query object',
                ].map((line) => (
                  <li key={line} className="flex items-start gap-2.5">
                    <span className="mt-2.5 h-1 w-1 shrink-0 rounded-full bg-zinc-400 dark:bg-zinc-600" aria-hidden="true" />
                    {line}
                  </li>
                ))}
              </ul>
            </div>
            <div className="min-w-0 rounded-lg border border-brand-200 bg-white p-6 dark:border-brand-900/60 dark:bg-zinc-950">
              <h3 className="flex items-center gap-2 text-xs font-semibold tracking-[0.1em] text-brand-700 uppercase dark:text-brand-400">
                <Check className="h-3.5 w-3.5" aria-hidden="true" />
                With FlexQuery
              </h3>
              <ul className="mt-4 space-y-2.5 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                {[
                  'One bind and one call per endpoint',
                  'Query string → validated expression tree → SQL',
                  'Defaults and governance configured once, globally',
                  'Identical query behavior on EF Core and Dapper',
                ].map((line) => (
                  <li key={line} className="flex items-start gap-2.5">
                    <Check className="mt-1.5 h-3.5 w-3.5 shrink-0 text-brand-600 dark:text-brand-400" aria-hidden="true" />
                    {line}
                  </li>
                ))}
              </ul>
              <pre className="mt-5 overflow-x-auto rounded-md border border-zinc-200 bg-zinc-50 p-3 font-mono text-[13px] leading-6 text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
                <code>{'var result = await db.Customers\n    .FlexQueryAsync(parameters, cancellationToken: ct);'}</code>
              </pre>
            </div>
          </div>
        </div>
      </section>

      {/* Architecture */}
      <section className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
        <div className="mb-10 text-center">
          <h2 className="text-3xl font-bold tracking-tight">From query string to SQL</h2>
          <p className="mx-auto mt-3 max-w-xl text-zinc-600 dark:text-zinc-400">
            Nothing is string-matched into LINQ at runtime. Requests become governed, typed expression
            trees before they ever reach your database.
          </p>
        </div>
        <ol className="grid gap-3 sm:grid-cols-2 lg:flex lg:items-stretch lg:gap-0">
          {pipeline.map((node, i) => (
            <li key={node.step} className="flex min-w-0 flex-1 items-start gap-3 lg:gap-0">
              <div className="flex-1 rounded-lg border border-zinc-200 bg-white px-4 py-3.5 lg:rounded-none lg:border-0 lg:bg-transparent lg:px-0 dark:border-zinc-800 dark:bg-zinc-950 lg:dark:bg-transparent">
                <div className="flex items-baseline gap-2 lg:flex-col lg:gap-1">
                  <span className="font-mono text-[11px] font-semibold text-brand-600 dark:text-brand-400">{node.step}</span>
                  <span className="font-mono text-sm font-semibold text-zinc-900 dark:text-white">{node.title}</span>
                </div>
                <p className="mt-0.5 text-xs leading-5 text-zinc-500 dark:text-zinc-400">{node.detail}</p>
              </div>
              {i < pipeline.length - 1 && (
                <div className="hidden shrink-0 items-center justify-center self-center px-1 text-zinc-300 lg:flex lg:px-3 dark:text-zinc-700" aria-hidden="true">
                  <ArrowDown className="h-4 w-4 lg:hidden" />
                  <ArrowRight className="hidden h-4 w-4 lg:block" />
                </div>
              )}
            </li>
          ))}
        </ol>
        <p className="mt-6 text-center text-sm text-zinc-500 dark:text-zinc-400">
          See the full{' '}
          <Link href="/docs/concepts/pipeline" className="font-medium text-brand-700 underline decoration-brand-300 underline-offset-4 hover:text-brand-800 dark:text-brand-300 dark:decoration-brand-800 dark:hover:text-brand-200">
            execution pipeline
          </Link>{' '}
          in the docs.
        </p>
      </section>

      {/* Providers & integrations */}
      <section className="border-t border-zinc-200 dark:border-zinc-800">
        <div className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
          <div className="mb-10 text-center">
            <h2 className="text-3xl font-bold tracking-tight">Providers &amp; integrations</h2>
            <p className="mx-auto mt-3 max-w-xl text-zinc-600 dark:text-zinc-400">
              Works with your stack — data access, UI grids, and API documentation.
            </p>
          </div>
          <div className="grid gap-x-10 gap-y-10 sm:grid-cols-2 lg:grid-cols-3">
            {integrationGroups.map((group) => (
              <div key={group.label}>
                <h3 className="border-b border-zinc-200 pb-2 font-mono text-xs font-semibold tracking-[0.1em] text-zinc-500 uppercase dark:border-zinc-800 dark:text-zinc-400">
                  {group.label}
                </h3>
                <div>
                  {group.items.map((p) => (
                    <Link
                      key={p.name}
                      href={p.href}
                      className="group flex items-center gap-3 border-t border-zinc-200 py-3.5 transition-colors first:border-t-0 hover:border-brand-500 dark:border-zinc-800 dark:first:border-t-0 dark:hover:border-brand-600"
                    >
                      <span className="flex h-8 w-8 items-center justify-center rounded-md bg-brand-50 text-brand-600 dark:bg-brand-950/60 dark:text-brand-400">
                        <p.icon className="h-4 w-4" aria-hidden="true" />
                      </span>
                      <span className="font-medium text-zinc-900 dark:text-white">{p.name}</span>
                      <ArrowRight
                        className="ml-auto h-4 w-4 -translate-x-1 text-transparent transition-all group-hover:translate-x-0 group-hover:text-brand-500"
                        aria-hidden="true"
                      />
                    </Link>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Bottom CTA */}
      <section className="mx-auto max-w-7xl px-4 pb-20 sm:px-6">
        <div className="grid gap-8 rounded-xl border border-zinc-800 bg-zinc-950 px-8 py-10 sm:px-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)] lg:items-center lg:gap-12 dark:border-zinc-800 dark:bg-zinc-900/50">
          <div className="min-w-0">
            <h2 className="text-2xl font-bold tracking-tight text-white sm:text-3xl">
              Build your first query in five minutes
            </h2>
            <p className="mt-3 max-w-md text-zinc-400">
              Follow the quick-start guide and ship a dynamic query endpoint today.
            </p>
            <Link
              href="/docs/getting-started/first-query"
              className="mt-6 inline-flex items-center gap-2 rounded-md bg-white px-5 py-2.5 text-sm font-semibold text-zinc-950 transition-colors hover:bg-brand-50"
            >
              Start building <ArrowRight className="h-4 w-4" />
            </Link>
          </div>
          <div className="min-w-0 [&>div]:my-0">
            <CodeBlock data-language="bash">
              <code>{'dotnet add package FlexQuery.NET.EntityFrameworkCore'}</code>
            </CodeBlock>
          </div>
        </div>
      </section>
    </div>
  )
}
