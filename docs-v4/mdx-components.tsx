import type { MDXComponents } from 'mdx/types'
import type { ComponentProps } from 'react'
import Link from 'next/link'
import { CodeBlock } from '@/components/code-block'
import { Callout } from '@/components/callout'
import { Tabs } from '@/components/tabs'
import { ApiTable } from '@/components/api-table'

function DocTable(props: ComponentProps<'table'>) {
  return (
    <div className="doc-table-wrap" data-doc-wide="table">
      <table {...props} />
    </div>
  )
}

const components: MDXComponents = {
  a: ({ href = '', children, ...props }) => {
    if (href.startsWith('/') || href.startsWith('#')) {
      return (
        <Link href={href} {...props}>
          {children}
        </Link>
      )
    }
    return (
      <a href={href} target="_blank" rel="noopener noreferrer" {...props}>
        {children}
      </a>
    )
  },
  pre: CodeBlock,
  table: DocTable,
  Callout,
  Tabs,
  ApiTable,
}

export function useMDXComponents(): MDXComponents {
  return components
}
