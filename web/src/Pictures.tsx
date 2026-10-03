import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type AdminCategory, type ImageSettings, type WordPicture, type WordPictures } from './api'
import { ErrorText, Field, inputClass, primaryButton, secondaryButton } from './ui'

const money = (n: number) => `$${n.toFixed(n < 1 ? 3 : 2)}`

/** How new pictures are drawn: chosen once at the top, used by "Draw all" and each word's own "Draw". */
interface DrawOptions {
  model: string
  versions: number
  matchStyle: boolean
}

/**
 * AI pictures for a topic's words (optional). The AI draws a few versions of each; nothing reaches a sheet until you
 * approve one. Once enough are approved, picture sheets (patterns, odd one out, counting, maths) use them.
 */
export default function PicturesPanel({ category, onChanged }: { category: AdminCategory; onChanged: () => void }) {
  const queryClient = useQueryClient()
  const settings = useQuery({ queryKey: ['admin', 'images'], queryFn: api.admin.imageSettings })
  const pictures = useQuery({ queryKey: ['admin', 'pictures', category.id], queryFn: () => api.admin.pictures(category.id) })
  const [options, setOptions] = useState<DrawOptions | null>(null)
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'pictures', category.id] })
    onChanged()
  }

  const toggleUse = useMutation({
    mutationFn: () => api.admin.updateCategory(category.id, category.name, category.description ?? '', category.isActive, !category.useImagePictures),
    onSuccess: onChanged,
  })

  const s = settings.data
  const draw: DrawOptions | null = s?.configured ? options ?? { model: s.defaultModel, versions: s.versions, matchStyle: false } : null
  const words = pictures.data?.words ?? []
  const min = s?.minApprovedForSheets ?? 4

  return (
    <div className="space-y-3 rounded-xl bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h3 className="font-bold">AI pictures</h3>
          <p className="text-sm text-slate-600">
            The AI draws black-and-white pictures of each word. Pick the best version and approve it — nothing is used until you do.
            With {min} or more approved, picture sheets use them{category.hasDrawnPictures ? ' instead of the built-in drawings' : ''}.
          </p>
          <p className="text-sm">
            Approved: <b>{category.approvedPictures}</b> of {words.length} · Spent on this topic: <b>{money(pictures.data?.spentUsd ?? 0)}</b>
          </p>
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={category.useImagePictures} disabled={toggleUse.isPending} onChange={() => toggleUse.mutate()} />
          Use approved AI pictures on sheets
        </label>
      </div>

      {s && !s.configured && (
        <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
          AI drawing is off. To turn it on, put a Gemini API key (Google AI Studio, billing on) in <code>appsettings.json</code> →{' '}
          <code>Images:Google:ApiKey</code> and restart the API. Pictures you already approved keep working.
        </p>
      )}
      <ErrorText error={settings.error ?? pictures.error ?? toggleUse.error} />

      {s && draw && (
        <DrawControls settings={s} options={draw} onOptions={setOptions} hasApproved={category.approvedPictures > 0} words={words} onDrawn={refresh} />
      )}

      {pictures.isPending && <p className="text-sm text-slate-500">Loading…</p>}
      {words.length === 0 && pictures.isSuccess && <p className="text-sm text-slate-500">Add some words first; each word gets its own picture.</p>}
      <ul className="divide-y divide-slate-100">
        {words.map((w) => (
          <WordRow key={w.wordId} word={w} draw={draw} hasApproved={category.approvedPictures > 0} onChanged={refresh} />
        ))}
      </ul>
    </div>
  )
}

/** Model, versions and "draw all": drawing costs money, so the estimate is shown before starting. */
function DrawControls({ settings, options, onOptions, hasApproved, words, onDrawn }: {
  settings: ImageSettings
  options: DrawOptions
  onOptions: (o: DrawOptions) => void
  hasApproved: boolean
  words: WordPictures[]
  onDrawn: () => void
}) {
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null)
  const [error, setError] = useState<Error | null>(null)
  const stop = useRef(false)

  const price = settings.models.find((m) => m.id === options.model)?.pricePerImage ?? 0
  const missing = words.filter((w) => w.pictures.length === 0)

  // One word at a time, so progress shows and you can stop part-way.
  const drawAll = async () => {
    const queue = [...missing]
    setError(null)
    stop.current = false
    setProgress({ done: 0, total: queue.length })
    for (let i = 0; i < queue.length && !stop.current; i++) {
      try {
        await api.admin.drawPictures(queue[i].wordId, options.model, options.versions, options.matchStyle && hasApproved)
      } catch (e) {
        setError(e as Error)
        break
      }
      setProgress({ done: i + 1, total: queue.length })
      onDrawn()
    }
    setProgress(null)
  }

  return (
    <div className="space-y-2 rounded-lg bg-slate-50 p-3">
      <div className="flex flex-wrap items-end gap-2">
        <div className="min-w-56 flex-1">
          <Field label="Draw with (Google)">
            <select className={inputClass} value={options.model} onChange={(e) => onOptions({ ...options, model: e.target.value })}>
              {settings.models.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.label} — {money(m.pricePerImage)} per picture · {m.note}
                </option>
              ))}
            </select>
          </Field>
        </div>
        <Field label="Versions">
          <input
            className={`${inputClass} w-20`}
            type="number"
            min={1}
            max={4}
            value={options.versions}
            onChange={(e) => onOptions({ ...options, versions: Math.min(4, Math.max(1, Number(e.target.value) || 1)) })}
          />
        </Field>
        <label className="flex items-center gap-2 pb-2 text-sm" title="Sends up to two approved pictures as a reference, so new ones match (same child, same lines).">
          <input type="checkbox" checked={options.matchStyle && hasApproved} disabled={!hasApproved} onChange={(e) => onOptions({ ...options, matchStyle: e.target.checked })} />
          Match approved pictures' style
        </label>
      </div>
      <div className="flex flex-wrap items-center gap-2 text-sm">
        {progress ? (
          <>
            <span>Drawing {Math.min(progress.done + 1, progress.total)} of {progress.total}…</span>
            <button className={secondaryButton} onClick={() => (stop.current = true)}>Stop after this one</button>
          </>
        ) : (
          <button className={primaryButton} disabled={missing.length === 0} onClick={drawAll}>
            Draw all without a picture ({missing.length}) · about {money(missing.length * options.versions * price)}
          </button>
        )}
        <span className="text-slate-500">Each word: {options.versions} × {money(price)} = {money(options.versions * price)}</span>
      </div>
      <ErrorText error={error} />
    </div>
  )
}

function WordRow({ word, draw, hasApproved, onChanged }: { word: WordPictures; draw: DrawOptions | null; hasApproved: boolean; onChanged: () => void }) {
  const [hint, setHint] = useState(word.pictureHint ?? '')
  const hintChanged = hint !== (word.pictureHint ?? '')
  const saveHint = useMutation({ mutationFn: () => api.admin.setPictureHint(word.wordId, hint) })
  const drawWord = useMutation({
    mutationFn: async (options: DrawOptions) => {
      if (hintChanged) await api.admin.setPictureHint(word.wordId, hint)
      return api.admin.drawPictures(word.wordId, options.model, options.versions, options.matchStyle && hasApproved)
    },
    onSuccess: onChanged,
  })

  return (
    <li className="space-y-2 py-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="w-32 font-bold">{word.word}</span>
        <input
          className={`${inputClass} min-w-48 flex-1 text-sm`}
          maxLength={300}
          value={hint}
          onChange={(e) => setHint(e.target.value)}
          onBlur={() => hintChanged && saveHint.mutate()}
          placeholder="How to draw it (optional), e.g. child standing on one leg, hands together above the head"
        />
        {draw && (
          <button className={secondaryButton} disabled={drawWord.isPending} onClick={() => drawWord.mutate(draw)}>
            {drawWord.isPending ? 'Drawing…' : word.pictures.length ? 'Draw more' : 'Draw'}
          </button>
        )}
      </div>
      <ErrorText error={drawWord.error ?? saveHint.error} />
      {word.pictures.length > 0 && (
        <div className="flex flex-wrap gap-3">
          {word.pictures.map((p) => (
            <PictureCard key={p.id} picture={p} canEdit={!!draw} onChanged={onChanged} />
          ))}
        </div>
      )}
    </li>
  )
}

function PictureCard({ picture, canEdit, onChanged }: { picture: WordPicture; canEdit: boolean; onChanged: () => void }) {
  const [fixing, setFixing] = useState(false)
  const [instruction, setInstruction] = useState('')
  const status = useMutation({ mutationFn: (s: 'Approved' | 'Rejected' | 'Draft') => api.admin.setPictureStatus(picture.id, s), onSuccess: onChanged })
  const mirror = useMutation({ mutationFn: (safe: boolean) => api.admin.setMirrorSafe(picture.id, safe), onSuccess: onChanged })
  const edit = useMutation({
    mutationFn: () => api.admin.editPicture(picture.id, instruction),
    onSuccess: () => {
      setFixing(false)
      setInstruction('')
      onChanged()
    },
  })
  const approved = picture.status === 'Approved'

  return (
    <div className={`w-44 space-y-1 rounded-lg border-2 p-2 text-xs ${approved ? 'border-green-600' : 'border-slate-200'}`}>
      <img src={picture.url} alt="" className="h-32 w-full bg-white object-contain" loading="lazy" />
      <div className="flex items-center justify-between text-slate-500">
        <span>{approved ? <b className="text-green-700">Approved</b> : picture.editedFromId ? 'Fixed draft' : 'Draft'}</span>
        <span>{money(picture.costUsd)}</span>
      </div>
      {/* What this picture can do beyond the basic picture sheets. */}
      <div className="text-slate-500" title="Dot-to-dot needs a traced outline; colour-by-number needs 3 or more areas to colour.">
        {picture.hasOutline ? 'Dot-to-dot ✓' : 'No dot-to-dot'} · {picture.areas >= 3 ? `Colour-by-number ✓ (${picture.areas})` : 'No colour-by-number'}
      </div>
      {approved && (
        <label className="flex items-center gap-1" title="Tick only if a mirror image clearly faces the other way; odd one out may then use it mirrored.">
          <input type="checkbox" checked={picture.mirrorSafe} disabled={mirror.isPending} onChange={(e) => mirror.mutate(e.target.checked)} />
          Looks different mirrored
        </label>
      )}
      <div className="flex flex-wrap gap-x-2">
        {approved ? (
          <button className="text-slate-600 underline" disabled={status.isPending} onClick={() => status.mutate('Draft')}>Unapprove</button>
        ) : (
          <button className="font-bold text-green-700 underline" disabled={status.isPending} onClick={() => status.mutate('Approved')}>Approve</button>
        )}
        <button className="text-red-700 underline" disabled={status.isPending} onClick={() => status.mutate('Rejected')}>Reject</button>
        {canEdit && !fixing && <button className="text-blue-700 underline" onClick={() => setFixing(true)}>Fix…</button>}
      </div>
      {fixing && (
        <form
          className="space-y-1"
          onSubmit={(e) => {
            e.preventDefault()
            if (instruction.trim()) edit.mutate()
          }}
        >
          <input className={`${inputClass} px-2 py-1 text-xs`} autoFocus maxLength={300} value={instruction} onChange={(e) => setInstruction(e.target.value)} placeholder="e.g. make the arms straight up" />
          <div className="flex gap-2">
            <button className="font-bold text-blue-700 underline" disabled={edit.isPending}>{edit.isPending ? 'Fixing…' : 'Fix'}</button>
            <button type="button" className="text-slate-600 underline" onClick={() => setFixing(false)}>Cancel</button>
          </div>
        </form>
      )}
      <ErrorText error={status.error ?? edit.error ?? mirror.error} />
    </div>
  )
}
