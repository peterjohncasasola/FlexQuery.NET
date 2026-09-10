import type { Metadata } from 'next'
import { notFound } from 'next/navigation'
import { Sidebar } from '@/components/sidebar'
import { Breadcrumbs } from '@/components/breadcrumbs'
import { TableOfContents } from '@/components/table-of-contents'
import { PagerNav } from '@/components/pager-nav'
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
    <div className="mx-auto flex w-full max-w-[112rem] px-4 sm:px-6 lg:px-8">
      <Sidebar />
      <div className="min-w-0 flex-1 py-10 lg:py-12 lg:pl-8 xl:pl-10">
        <div className="w-full">
          <div className="max-w-3xl">
            <Breadcrumbs group={groupTitle} title={source.frontmatter.title} />
          </div>
          <article className="prose-doc">
            <Content />
          </article>
          <div className="max-w-3xl">
            <PagerNav prev={prev} next={next} />
          </div>
        </div>
      </div>
      <TableOfContents headings={source.headings} />
    </div>
  )
}
