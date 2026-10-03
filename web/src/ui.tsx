import type { ReactNode } from 'react'

export const inputClass = 'w-full rounded-lg border border-slate-300 px-3 py-2'
export const primaryButton = 'rounded-lg bg-blue-600 px-4 py-2.5 font-bold text-white hover:bg-blue-700 disabled:opacity-50'
export const secondaryButton = 'rounded-lg border border-slate-300 px-4 py-2 font-bold hover:bg-slate-50 disabled:opacity-50'

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-1">
      <span className="text-sm font-bold">{label}</span>
      {children}
    </label>
  )
}

export function Segmented<T extends string>({ options, value, onChange }: { options: T[]; value: T; onChange: (v: T) => void }) {
  return (
    <div className="flex rounded-lg bg-slate-100 p-1">
      {options.map((o) => (
        <button
          key={o}
          type="button"
          onClick={() => onChange(o)}
          className={`flex-1 rounded-md px-2 py-1.5 text-sm ${o === value ? 'bg-white font-bold shadow-sm' : 'text-slate-600'}`}
        >
          {o}
        </button>
      ))}
    </div>
  )
}

export function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="mx-auto mt-16 max-w-sm space-y-4 rounded-xl bg-white p-6 shadow-sm">
      <h1 className="text-2xl font-bold">{title}</h1>
      {children}
    </div>
  )
}

export function ErrorText({ error }: { error: Error | null }) {
  return error ? <p className="text-sm text-red-600">{error.message}</p> : null
}
