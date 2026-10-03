import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type AdminCategory, type CategoryWord, type WordSuggestion } from './api'
import PicturesPanel from './Pictures'
import { ErrorText, Field, inputClass, primaryButton, secondaryButton } from './ui'

/** Admin: categories (vehicles, animals, fruits…), their word lists, and AI word suggestions to review. */
export default function Admin() {
  const categories = useQuery({ queryKey: ['admin', 'categories'], queryFn: api.admin.categories })
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const selected = categories.data?.find((c) => c.id === selectedId) ?? categories.data?.[0]

  return (
    <main className="mx-auto grid max-w-6xl gap-6 px-4 py-6 md:grid-cols-[16rem_1fr]">
      <aside className="space-y-4">
        <section className="space-y-2 rounded-xl bg-white p-4 shadow-sm">
          <h2 className="font-bold">Categories</h2>
          {categories.isPending && <p className="text-sm text-slate-500">Loading…</p>}
          <ErrorText error={categories.error} />
          <ul className="space-y-1">
            {categories.data?.map((c) => (
              <li key={c.id}>
                <button
                  className={`w-full rounded-lg px-3 py-2 text-left text-sm ${c.id === selected?.id ? 'bg-blue-50 font-bold text-blue-800' : 'hover:bg-slate-50'}`}
                  onClick={() => setSelectedId(c.id)}
                >
                  {c.name} <span className="text-slate-500">· {c.wordCount}</span>
                  {!c.isActive && <span className="ml-1 text-xs text-slate-400">(hidden)</span>}
                </button>
              </li>
            ))}
          </ul>
        </section>
        <NewCategory onCreated={setSelectedId} />
        <TrainingData />
      </aside>

      {selected ? <CategoryPanel key={selected.id} category={selected} /> : <p className="text-slate-500">Add a category to begin.</p>}
    </main>
  )
}

function NewCategory({ onCreated }: { onCreated: (id: string) => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const create = useMutation({
    mutationFn: () => api.admin.createCategory(name.trim(), description.trim()),
    onSuccess: (c) => {
      setName('')
      setDescription('')
      queryClient.invalidateQueries({ queryKey: ['admin', 'categories'] })
      queryClient.invalidateQueries({ queryKey: ['categories'] })
      onCreated(c.id)
    },
  })

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    create.mutate()
  }

  return (
    <form className="space-y-3 rounded-xl bg-white p-4 shadow-sm" onSubmit={onSubmit}>
      <h2 className="font-bold">New category</h2>
      <Field label="Name">
        <input className={inputClass} required maxLength={60} value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Fruits" />
      </Field>
      <Field label="About it (helps the AI)">
        <input className={inputClass} maxLength={300} value={description} onChange={(e) => setDescription(e.target.value)} placeholder="e.g. common fruits in India" />
      </Field>
      <button className={`${primaryButton} w-full`} disabled={create.isPending}>Add category</button>
      <ErrorText error={create.error} />
    </form>
  )
}

function CategoryPanel({ category }: { category: AdminCategory }) {
  const queryClient = useQueryClient()
  const words = useQuery({ queryKey: ['admin', 'words', category.id], queryFn: () => api.admin.words(category.id) })
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['admin'] })
    queryClient.invalidateQueries({ queryKey: ['categories'] })
  }

  const toggleActive = useMutation({
    mutationFn: () => api.admin.updateCategory(category.id, category.name, category.description ?? '', !category.isActive),
    onSuccess: refresh,
  })

  return (
    <section className="space-y-6">
      <div className="rounded-xl bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="text-xl font-bold">{category.name}</h2>
            <p className="text-sm text-slate-600">
              {category.description ?? 'No description.'}{' '}
              {category.hasPictures
                ? 'Has pictures, so it works on picture sheets too.'
                : 'No pictures yet, so it works on word sheets (word search, word magic). Approve AI pictures below to use it on picture sheets.'}
            </p>
          </div>
          <button className={secondaryButton} disabled={toggleActive.isPending} onClick={() => toggleActive.mutate()}>
            {category.isActive ? 'Hide from sheet maker' : 'Show in sheet maker'}
          </button>
        </div>
      </div>

      <AiSuggestions category={category} existing={words.data ?? []} onAdded={refresh} />

      <div className="space-y-3 rounded-xl bg-white p-4 shadow-sm">
        <h3 className="font-bold">Words ({words.data?.length ?? 0})</h3>
        <AddWord categoryId={category.id} onAdded={refresh} />
        <ErrorText error={words.error} />
        <table className="w-full text-sm">
          <thead className="text-left text-slate-500">
            <tr>
              <th className="py-1">Word / letter</th>
              <th>Hindi</th>
              <th>Telugu</th>
              <th>From</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {words.data?.map((w) => <WordRow key={w.id} word={w} onChanged={refresh} />)}
          </tbody>
        </table>
      </div>

      <PicturesPanel category={category} onChanged={refresh} />
    </section>
  )
}

function AddWord({ categoryId, onAdded }: { categoryId: string; onAdded: () => void }) {
  const [word, setWord] = useState('')
  const [hindi, setHindi] = useState('')
  const [telugu, setTelugu] = useState('')
  const add = useMutation({
    mutationFn: () => api.admin.addWords(categoryId, [{ word, hindi, telugu }]),
    onSuccess: () => {
      setWord('')
      setHindi('')
      setTelugu('')
      onAdded()
    },
  })

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    add.mutate()
  }

  return (
    <form className="flex flex-wrap gap-2" onSubmit={onSubmit}>
      <input className={`${inputClass} w-40 flex-1`} required maxLength={40} value={word} onChange={(e) => setWord(e.target.value)} placeholder="Word or letter (apple, అ, क)" />
      <input className={`${inputClass} w-32 flex-1`} maxLength={60} value={hindi} onChange={(e) => setHindi(e.target.value)} placeholder="हिंदी (optional)" />
      <input className={`${inputClass} w-32 flex-1`} maxLength={60} value={telugu} onChange={(e) => setTelugu(e.target.value)} placeholder="తెలుగు (optional)" />
      <button className={secondaryButton} disabled={add.isPending}>Add</button>
      <ErrorText error={add.error} />
    </form>
  )
}

function WordRow({ word, onChanged }: { word: CategoryWord; onChanged: () => void }) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState({ word: word.word, hindi: word.hindi ?? '', telugu: word.telugu ?? '' })
  const save = useMutation({
    mutationFn: () => api.admin.updateWord(word.id, draft),
    onSuccess: () => {
      setEditing(false)
      onChanged()
    },
  })
  const remove = useMutation({ mutationFn: () => api.admin.deleteWord(word.id), onSuccess: onChanged })

  if (editing) {
    return (
      <tr className="border-t border-slate-100">
        <td className="py-1 pr-2"><input className={inputClass} value={draft.word} onChange={(e) => setDraft({ ...draft, word: e.target.value })} /></td>
        <td className="pr-2"><input className={inputClass} value={draft.hindi} onChange={(e) => setDraft({ ...draft, hindi: e.target.value })} /></td>
        <td className="pr-2"><input className={inputClass} value={draft.telugu} onChange={(e) => setDraft({ ...draft, telugu: e.target.value })} /></td>
        <td />
        <td className="space-x-2 whitespace-nowrap text-right">
          <button className="text-blue-700 underline" disabled={save.isPending} onClick={() => save.mutate()}>Save</button>
          <button className="text-slate-600 underline" onClick={() => setEditing(false)}>Cancel</button>
        </td>
      </tr>
    )
  }

  return (
    <tr className="border-t border-slate-100">
      <td className="py-1.5">{word.word}</td>
      <td>{word.hindi}</td>
      <td>{word.telugu}</td>
      <td className="text-slate-500">{word.source === 'ai' ? 'AI' : 'You'}</td>
      <td className="space-x-2 whitespace-nowrap text-right">
        <button className="text-blue-700 underline" onClick={() => setEditing(true)}>Edit</button>
        <button className="text-red-700 underline" disabled={remove.isPending} onClick={() => remove.mutate()}>Delete</button>
      </td>
    </tr>
  )
}

/** Suggestions stay here, editable, until the admin ticks and adds them; Hindi/Telugu spelling needs a human check. */
function AiSuggestions({ category, existing, onAdded }: { category: AdminCategory; existing: CategoryWord[]; onAdded: () => void }) {
  const [count, setCount] = useState(15)
  const [notes, setNotes] = useState('')
  const [rows, setRows] = useState<(WordSuggestion & { keep: boolean })[]>([])
  const [sources, setSources] = useState<string[]>([])
  const [callId, setCallId] = useState<string>()

  const suggest = useMutation({
    mutationFn: () => api.admin.suggest(category.id, count, notes),
    onSuccess: (r) => {
      setRows(r.words.map((w) => ({ ...w, keep: true })))
      setSources(r.sources)
      setCallId(r.callId)
    },
  })
  const add = useMutation({
    // The AI's drawing idea is saved as the word's picture hint, for AI pictures later.
    mutationFn: () =>
      api.admin.addWords(category.id, rows.filter((r) => r.keep && r.word.trim()).map((r) => ({ ...r, pictureHint: r.picture })), callId),
    onSuccess: () => {
      setRows([])
      setSources([])
      onAdded()
    },
  })

  const update = (i: number, patch: Partial<WordSuggestion & { keep: boolean }>) =>
    setRows((cur) => cur.map((r, j) => (j === i ? { ...r, ...patch } : r)))
  const kept = rows.filter((r) => r.keep).length

  return (
    <div className="space-y-3 rounded-xl bg-white p-4 shadow-sm">
      <div>
        <h3 className="font-bold">Ask AI to find words</h3>
        <p className="text-sm text-slate-600">
          The AI searches the web for words children aged 6–8 know in “{category.name}”, skipping the {existing.length} you already have.
          Nothing is saved until you add it — check the Hindi and Telugu spellings first.
        </p>
      </div>
      <div className="flex flex-wrap items-end gap-2">
        <Field label="How many">
          <input className={`${inputClass} w-24`} type="number" min={5} max={40} value={count} onChange={(e) => setCount(Number(e.target.value))} />
        </Field>
        <div className="min-w-48 flex-1">
          <Field label="Notes for the AI (optional)">
            <input className={inputClass} maxLength={200} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="e.g. farm animals only, short words" />
          </Field>
        </div>
        <button className={primaryButton} disabled={suggest.isPending} onClick={() => suggest.mutate()}>
          {suggest.isPending ? 'Searching the web…' : 'Find words'}
        </button>
      </div>
      <ErrorText error={suggest.error} />

      {rows.length > 0 && (
        <>
          <table className="w-full text-sm">
            <thead className="text-left text-slate-500">
              <tr>
                <th className="w-8" />
                <th className="py-1">Word / letter</th>
                <th>Hindi</th>
                <th>Telugu</th>
                <th>Note</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i} className="border-t border-slate-100">
                  <td><input type="checkbox" aria-label={`Keep ${r.word}`} checked={r.keep} onChange={(e) => update(i, { keep: e.target.checked })} /></td>
                  <td className="py-1 pr-2"><input className={inputClass} value={r.word} onChange={(e) => update(i, { word: e.target.value })} /></td>
                  <td className="pr-2"><input className={inputClass} value={r.hindi ?? ''} onChange={(e) => update(i, { hindi: e.target.value })} /></td>
                  <td className="pr-2"><input className={inputClass} value={r.telugu ?? ''} onChange={(e) => update(i, { telugu: e.target.value })} /></td>
                  <td className="text-slate-500">{r.note}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {sources.length > 0 && (
            <details className="text-sm text-slate-600">
              <summary>Sources the AI used ({sources.length})</summary>
              <ul className="list-disc pl-5">
                {sources.map((s) => (
                  <li key={s}><a className="text-blue-700 underline" href={s} target="_blank" rel="noreferrer">{s}</a></li>
                ))}
              </ul>
            </details>
          )}
          <button className={primaryButton} disabled={add.isPending || kept === 0} onClick={() => add.mutate()}>
            Add {kept} word{kept === 1 ? '' : 's'} to {category.name}
          </button>
          <ErrorText error={add.error} />
        </>
      )}
    </div>
  )
}

/** Every AI search is logged; the words you keep (and your fixes) are the answers our own model will learn from. */
function TrainingData() {
  const stats = useQuery({ queryKey: ['admin', 'ai-stats'], queryFn: api.admin.aiStats })
  const download = useMutation({ mutationFn: api.admin.downloadTrainingData })
  const reviewed = stats.data?.reduce((n, s) => n + s.reviewed, 0) ?? 0
  const calls = stats.data?.reduce((n, s) => n + s.calls, 0) ?? 0

  return (
    <section className="space-y-2 rounded-xl bg-white p-4 shadow-sm">
      <h2 className="font-bold">AI training data</h2>
      <p className="text-sm text-slate-600">
        {calls} AI searches logged, {reviewed} reviewed. Reviewed ones (the words you kept, with your fixes) are the examples for training our own model.
      </p>
      <ErrorText error={stats.error ?? download.error} />
      <button className={`${secondaryButton} w-full`} disabled={reviewed === 0 || download.isPending} onClick={() => download.mutate()}>
        Download for fine-tuning (.jsonl)
      </button>
    </section>
  )
}
