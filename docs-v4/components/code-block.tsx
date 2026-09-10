'use client'

import { useEffect, useState, useRef, type ReactElement } from 'react'
import { Check, Copy, TriangleAlert } from 'lucide-react'

const languageLabels: Record<string, string> = {
  csharp: 'C#',
  cs: 'C#',
  js: 'JavaScript',
  javascript: 'JavaScript',
  ts: 'TypeScript',
  typescript: 'TypeScript',
  json: 'JSON',
  sql: 'SQL',
  http: 'HTTP',
  bash: 'Shell',
  shell: 'Shell',
  text: 'Plain text',
}

export function CodeBlock(props: {
  children?: ReactElement
  className?: string
  style?: React.CSSProperties
  'data-language'?: string
}) {
  const { children, className, style, 'data-language': dataLanguage } = props
  const [copyState, setCopyState] = useState<'idle' | 'success' | 'error'>('idle')
  const preRef = useRef<HTMLPreElement>(null)
  const resetTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const child: any = children?.props ?? {}
  const codeClass: string = child.className ?? ''
  const languageMatch = /language-(\w+)/.exec(codeClass)
  const language = dataLanguage ?? (languageMatch ? languageMatch[1] : null)

  useEffect(
    () => () => {
      if (resetTimerRef.current) clearTimeout(resetTimerRef.current)
    },
    [],
  )

  const copy = async () => {
    const text = preRef.current?.textContent ?? ''
    try {
      await navigator.clipboard.writeText(text)
      setCopyState('success')
    } catch {
      setCopyState('error')
    }
    if (resetTimerRef.current) clearTimeout(resetTimerRef.current)
    resetTimerRef.current = setTimeout(() => setCopyState('idle'), 2000)
  }

  return (
    <div
      className="group relative my-6 overflow-hidden rounded-lg border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-950"
      data-doc-wide="code"
    >
      <div className="flex items-center justify-between border-b border-zinc-200 bg-zinc-50 px-4 py-2 dark:border-zinc-800 dark:bg-zinc-900">
        <span className="font-mono text-[11px] font-medium text-zinc-500 dark:text-zinc-400">
          {language ? (languageLabels[language.toLowerCase()] ?? language) : 'Plain text'}
        </span>
        <button
          type="button"
          onClick={copy}
          aria-label={copyState === 'error' ? 'Copy failed; try again' : 'Copy code'}
          className="flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-zinc-500 transition-colors hover:bg-zinc-200 hover:text-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
        >
          {copyState === 'success' ? (
            <Check className="h-3.5 w-3.5 text-emerald-500" aria-hidden="true" />
          ) : copyState === 'error' ? (
            <TriangleAlert className="h-3.5 w-3.5 text-amber-500" aria-hidden="true" />
          ) : (
            <Copy className="h-3.5 w-3.5" aria-hidden="true" />
          )}
          <span aria-live="polite">
            {copyState === 'success' ? 'Copied' : copyState === 'error' ? 'Try again' : 'Copy'}
          </span>
        </button>
      </div>
      <pre
        ref={preRef}
        className={`${className ?? ''} max-h-[40rem] overflow-auto p-4 text-[13px] leading-6 [&_code]:bg-transparent [&_code]:p-0 [&_code]:font-mono`}
        style={style}
        data-language={dataLanguage}
      >
        {children}
      </pre>
    </div>
  )
}
