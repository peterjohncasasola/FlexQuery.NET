import type { Metadata } from 'next'
import { notFound } from 'next/navigation'
import { Sidebar } from '@/components/sidebar'
import { Breadcrumbs } from '@/components/breadcrumbs'
import { TableOfContents } from '@/components/table-of-contents'
import { PagerNav } from '@/components/pager-nav'
import { SearchDialog } from '@/components/search-dialog'
import { getAllDocSlugs, readDocSource } from '@/lib/docs'
import { getNeighbors, getGroupTitle } from '@/lib/navigation'

export const dynamicParams = false

export function generateStaticParams() {
  return getAllDocSlugs().map((slug) => ({ slug }))
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string[] }>
}): Promise<Metadata> {
  const { slug } = await params
  const source = readDocSource(slug)
  if (!source) return {}
  return {
    title: source.frontmatter.title,
    description: source.frontmatter.description,
  }
}

export default async function DocPage({ params }: { params: Promise<{ slug: string[] }> }) {
  const { slug } = await params
  const source = readDocSource(slug)
  if (!source) notFound()

  const slugPath = source.modulePath.replace(/\.mdx$/, '')
  const { default: Content } = await import(`@/content/docs/${slugPath}.mdx`)

  const groupTitle = source.frontmatter.section ?? getGroupTitle(slugPath)
  const { prev, next } = getNeighbors(slugPath)

  return (
    <div className="mx-auto flex max-w-7xl gap-8 px-4 sm:px-6">
      <Sidebar />
      <div className="min-w-0 flex-1 py-10 lg:py-12">
        <div className="mx-auto max-w-3xl">
          <Breadcrumbs group={groupTitle} title={source.frontmatter.title} />
          <article className="prose-doc">
            <Content />
          </article>
          <PagerNav prev={prev} next={next} />
        </div>
      </div>
      <TableOfContents headings={source.headings} />
      <SearchDialog />
    </div>
  )
}
