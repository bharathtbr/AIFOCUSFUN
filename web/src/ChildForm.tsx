import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type PaperSize, type Script } from './api'
import { Card, ErrorText, Field, Segmented, inputClass, primaryButton } from './ui'

const scriptLabels: Record<Script, string> = { en: 'English', hi: 'Hindi', te: 'Telugu' }

export default function ChildForm() {
  const queryClient = useQueryClient()
  const [firstName, setFirstName] = useState('')
  const [age, setAge] = useState(6)
  const [scripts, setScripts] = useState<Script[]>(['en', 'hi', 'te'])
  const [paperSize, setPaperSize] = useState<PaperSize>('Letter')

  const create = useMutation({
    mutationFn: () => api.createChild({ firstName: firstName.trim(), age, scripts, paperSize }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['children'] }),
  })

  const toggle = (s: Script) => setScripts((cur) => (cur.includes(s) ? cur.filter((x) => x !== s) : [...cur, s]))

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    create.mutate()
  }

  return (
    <Card title="Who are the sheets for?">
      <form className="space-y-4" onSubmit={onSubmit}>
        <Field label="First name (printed on sheets)">
          <input className={inputClass} required maxLength={30} value={firstName} onChange={(e) => setFirstName(e.target.value)} />
        </Field>
        <Field label="Age">
          <input className={inputClass} type="number" min={3} max={12} value={age} onChange={(e) => setAge(Number(e.target.value))} />
        </Field>
        <fieldset className="space-y-1">
          <legend className="text-sm font-bold">Scripts to practise</legend>
          <div className="flex gap-4">
            {(Object.keys(scriptLabels) as Script[]).map((s) => (
              <label key={s} className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={scripts.includes(s)} onChange={() => toggle(s)} />
                {scriptLabels[s]}
              </label>
            ))}
          </div>
        </fieldset>
        <Field label="Paper">
          <Segmented options={['Letter', 'A4'] as PaperSize[]} value={paperSize} onChange={setPaperSize} />
        </Field>
        <p className="text-xs text-slate-500">Only the first name is stored: no photos, school or location.</p>
        <button className={`${primaryButton} w-full`} disabled={create.isPending || scripts.length === 0}>
          Save
        </button>
        <ErrorText error={create.error} />
      </form>
    </Card>
  )
}
