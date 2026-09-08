import type { MDXComponents } from 'mdx/types'
import Link from 'next/link'
import { CodeBlock } from '@/components/code-block'
import { Callout } from '@/components/callout'
import { Tabs } from '@/components/tabs'
import { ApiTable } from '@/components/api-table'

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
  Callout,
  Tabs,
  ApiTable,
}

export function useMDXComponents(): MDXComponents {
  return components
}
