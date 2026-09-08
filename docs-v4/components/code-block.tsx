'use client'

import { useState, useRef, type ReactElement } from 'react'
import { Check, Copy } from 'lucide-react'

export function CodeBlock(props: {
  children?: ReactElement
  className?: string
  style?: React.CSSProperties
  'data-language'?: string
}) {
  const { children, className, style, 'data-language': dataLanguage } = props
  const [copied, setCopied] = useState(false)
  const preRef = useRef<HTMLPreElement>(null)

  const child: any = children?.props ?? {}
  const codeClass: string = child.className ?? ''
  const languageMatch = /language-(\w+)/.exec(codeClass)
  const language = dataLanguage ?? (languageMatch ? languageMatch[1] : null)

  const copy = async () => {
    const text = preRef.current?.textContent ?? ''
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // clipboard unavailable — ignore
    }
  }

  return (
    <div className="group relative my-6 overflow-hidden rounded-xl border border-zinc-200 dark:border-zinc-800">
      <div className="flex items-center justify-between border-b border-zinc-200 bg-zinc-50 px-4 py-2 dark:border-zinc-800 dark:bg-zinc-900">
        <span className="font-mono text-xs text-zinc-500 dark:text-zinc-400">{language ?? 'text'}</span>
        <button
          type="button"
          onClick={copy}
          aria-label="Copy code"
          className="flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-zinc-500 transition-colors hover:bg-zinc-200 hover:text-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
        >
          {copied ? <Check className="h-3.5 w-3.5 text-emerald-500" /> : <Copy className="h-3.5 w-3.5" />}
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      <pre
        ref={preRef}
        className={`${className ?? ''} overflow-x-auto p-4 text-[13px] leading-6 [&_code]:bg-transparent [&_code]:p-0 [&_code]:font-mono`}
        style={style}
        data-language={dataLanguage}
      >
        {children}
      </pre>
    </div>
  )
}
