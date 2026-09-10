import type { ReactNode } from 'react'
import { Info, Lightbulb, TriangleAlert, OctagonX } from 'lucide-react'

const variants = {
  note: {
    icon: Info,
    wrap: 'border-blue-200 bg-blue-50 dark:border-blue-900/50 dark:bg-blue-950/40',
    iconColor: 'text-blue-500',
    title: 'Note',
  },
  tip: {
    icon: Lightbulb,
    wrap: 'border-emerald-200 bg-emerald-50 dark:border-emerald-900/50 dark:bg-emerald-950/40',
    iconColor: 'text-emerald-500',
    title: 'Tip',
  },
  warning: {
    icon: TriangleAlert,
    wrap: 'border-amber-200 bg-amber-50 dark:border-amber-900/50 dark:bg-amber-950/40',
    iconColor: 'text-amber-500',
    title: 'Warning',
  },
  danger: {
    icon: OctagonX,
    wrap: 'border-red-200 bg-red-50 dark:border-red-900/50 dark:bg-red-950/40',
    iconColor: 'text-red-500',
    title: 'Important',
  },
} as const

export function Callout({
  variant = 'note',
  title,
  children,
}: {
  variant?: keyof typeof variants
  title?: string
  children: ReactNode
}) {
  const v = variants[variant]
  const Icon = v.icon
  return (
    <aside
      role={variant === 'warning' || variant === 'danger' ? 'alert' : 'note'}
      className={`my-6 rounded-lg border-l-4 p-4 ${v.wrap}`}
    >
      <div className={`flex items-center gap-2 text-sm font-semibold ${v.iconColor}`}>
        <Icon className="h-4 w-4" aria-hidden="true" />
        {title ?? v.title}
      </div>
      <div className="mt-2 text-sm leading-6 text-zinc-700 dark:text-zinc-300">{children}</div>
    </aside>
  )
}
