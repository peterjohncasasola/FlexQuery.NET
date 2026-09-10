export function BrandMark({ compact = false }: { compact?: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={`grid shrink-0 place-items-center rounded-md bg-brand-600 font-mono font-semibold tracking-[-0.08em] text-white shadow-[inset_0_0_0_1px_rgba(255,255,255,0.16)] ${
        compact ? 'h-6 w-6 text-[9px]' : 'h-7 w-7 text-[10px]'
      }`}
    >
      FQ
    </span>
  )
}
