import type { ReactNode } from 'react'

export function ApiTable({ children }: { children: ReactNode }) {
  return (
    <div className="my-6 overflow-x-auto rounded-xl border border-zinc-200 dark:border-zinc-800">
      <table className="w-full text-sm [&_td]:border-0 [&_th]:border-0 [&_th]:bg-zinc-50 dark:[&_th]:bg-zinc-900">
        {children}
      </table>
    </div>
  )
}
