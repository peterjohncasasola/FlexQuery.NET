export interface NavItem {
  title: string
  slug: string
}

export interface NavGroup {
  title: string
  items: NavItem[]
}

export const navigation: NavGroup[] = [
  {
    title: 'Introduction',
    items: [
      { title: 'Overview', slug: 'introduction' },
      { title: 'Installation', slug: 'getting-started/installation' },
      { title: 'First Query', slug: 'getting-started/first-query' },
    ],
  },
  {
    title: 'Core Concepts',
    items: [
      { title: 'Configuration', slug: 'concepts/configuration' },
      { title: 'Execution Pipeline', slug: 'concepts/pipeline' },
      { title: 'Query Options', slug: 'concepts/query-options' },
      { title: 'Query Result', slug: 'concepts/query-result' },
      { title: 'Query Syntax', slug: 'concepts/query-syntax' },
    ],
  },
  {
    title: 'Guides',
    items: [
      { title: 'Filtering', slug: 'guides/filtering' },
      { title: 'Operators', slug: 'guides/operators' },
      { title: 'Sorting', slug: 'guides/sorting' },
      { title: 'Paging', slug: 'guides/paging' },
      { title: 'Projection', slug: 'guides/projection' },
      { title: 'Include', slug: 'guides/include' },
      { title: 'Expand', slug: 'guides/expand' },
      { title: 'Grouping & Aggregates', slug: 'guides/grouping' },
      { title: 'Keyset Pagination', slug: 'guides/keyset-pagination' },
      { title: 'Fluent API', slug: 'guides/fluent-api' },
      { title: 'Query Composition', slug: 'guides/query-composition' },
      { title: 'Typed DTO Projection', slug: 'guides/typed-dto-projection' },
      { title: 'Validation', slug: 'guides/validation' },
    ],
  },
  {
    title: 'Providers',
    items: [
      { title: 'Entity Framework Core', slug: 'providers/ef-core' },
      { title: 'Dapper', slug: 'providers/dapper' },
    ],
  },
  {
    title: 'Integrations',
    items: [
      { title: 'ASP.NET Core', slug: 'integrations/aspnetcore' },
      { title: 'AG Grid', slug: 'integrations/ag-grid' },
      { title: 'Kendo UI', slug: 'integrations/kendo' },
      { title: 'OpenAPI', slug: 'integrations/openapi' },
    ],
  },
  {
    title: 'Security',
    items: [{ title: 'Security & Governance', slug: 'security' }],
  },
  {
    title: 'Diagnostics',
    items: [{ title: 'Diagnostics & Observability', slug: 'diagnostics' }],
  },
  {
    title: 'Resources',
    items: [
      { title: 'Recipes', slug: 'recipes' },
      { title: 'Troubleshooting', slug: 'troubleshooting' },
      { title: 'Migrate from v3', slug: 'migration/v3-to-v4' },
      { title: 'v3 ' + '\u2192' + ' v4 Change Matrix', slug: 'migration/change-matrix' },
      { title: 'API Reference', slug: 'api-reference' },
    ],
  },
]

export const allItems: NavItem[] = navigation.flatMap((g) => g.items)

export function getNeighbors(slug: string): { prev: NavItem | null; next: NavItem | null } {
  const idx = allItems.findIndex((i) => i.slug === slug)
  if (idx < 0) return { prev: null, next: null }
  return {
    prev: idx > 0 ? allItems[idx - 1] : null,
    next: idx < allItems.length - 1 ? allItems[idx + 1] : null,
  }
}

export function getGroupTitle(slug: string): string | null {
  for (const g of navigation) {
    if (g.items.some((i) => i.slug === slug)) return g.title
  }
  return null
}