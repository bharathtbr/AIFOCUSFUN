import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Category, type Child, type GeneratedSheet, type Level, type PaperSize, type Sheet, type SheetSpec, type SheetType } from './api'
import { ErrorText, Field, Segmented, inputClass, primaryButton, secondaryButton } from './ui'

const sheetGroups: { module: string; types: { value: SheetType; label: string }[] }[] = [
  {
    module: 'Puzzles',
    types: [
      { value: 'RoadMaze', label: 'Maze' },
      { value: 'DotToDot', label: 'Dot-to-dot' },
      { value: 'SpotTheDifference', label: 'Spot the difference' },
      { value: 'WordSearch', label: 'Word search' },
      { value: 'OddOneOut', label: 'Odd one out' },
      { value: 'CountVehicles', label: 'Count the vehicles' },
    ],
  },
  {
    module: 'Coloring',
    types: [
      { value: 'Coloring', label: 'Coloring page' },
      { value: 'ColorByNumber', label: 'Color by number' },
      { value: 'PictureCards', label: 'Picture cards (yoga cards for a yoga topic)' },
    ],
  },
  {
    module: 'Tracing & writing',
    types: [
      { value: 'EnglishTracing', label: 'English letter / number tracing' },
      { value: 'TeluguTracing', label: 'Telugu letter tracing' },
      { value: 'HindiTracing', label: 'Hindi letter tracing' },
      { value: 'RoadTracing', label: 'Road tracing' },
    ],
  },
  {
    module: 'Maths & words',
    types: [
      { value: 'Maths', label: 'Maths (add, take away, number bonds)' },
      { value: 'WordMagic', label: 'Word magic (missing letters, unscramble)' },
    ],
  },
  {
    module: 'Memory & patterns',
    types: [{ value: 'Pattern', label: 'What comes next? (patterns)' }],
  },
]
/** Sheets that can use any topic's word list, even without pictures. */
const wordSheets: SheetType[] = ['WordSearch', 'WordMagic']
/** Sheets that draw the topic's pictures (drawn or approved AI pictures); the rest are vehicle scenes. */
const topicSheets: SheetType[] = [...wordSheets, 'Pattern', 'OddOneOut', 'CountVehicles', 'Maths', 'Coloring', 'SpotTheDifference', 'RoadMaze', 'PictureCards']
/** Sheets that need a picture's traced shape, which only approved AI pictures have. */
const imageSheets: SheetType[] = ['DotToDot', 'ColorByNumber']
/** Telugu / Hindi tracing: uses a topic's words written in that script (e.g. a Varnamala list), else its own letters by level. */
const scriptTracing: Partial<Record<SheetType, string>> = { TeluguTracing: 'Telugu', HindiTracing: 'Hindi' }
const levels: Level[] = ['Easy', 'Medium', 'Challenge']
const paperSizes: PaperSize[] = ['Letter', 'A4']

/** Local date, so the sheet shows the parent's today rather than UTC's. */
const today = () => {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export default function SheetMaker({ child }: { child: Child }) {
  const queryClient = useQueryClient()
  const [type, setType] = useState<SheetType>('RoadMaze')
  const [level, setLevel] = useState<Level>('Easy')
  const [paperSize, setPaperSize] = useState<PaperSize>(child.paperSize)
  const [theme, setTheme] = useState(child.themes[0] ?? 'vehicles')
  const categories = useQuery({ queryKey: ['categories'], queryFn: api.categories })
  const topic = categories.data?.find((c) => c.key === theme)
  const [letters, setLetters] = useState('')
  const [includeAnswerKey, setIncludeAnswerKey] = useState(true)

  // Generating stores nothing; only "Save" writes to the database and R2.
  const sheet = useMutation({
    mutationFn: (spec: Partial<SheetSpec>) =>
      api.generateSheet(child.id, {
        type: spec.type ?? type,
        theme: spec.theme ?? theme,
        level: spec.level ?? level,
        paperSize: spec.paperSize ?? paperSize,
        seed: spec.seed,
        letters: (spec.letters ?? letters).trim() || undefined,
        date: today(),
      }),
  })

  const save = useMutation({
    mutationFn: ({ spec }: GeneratedSheet) =>
      api.saveSheet(child.id, { ...spec, letters: spec.letters ?? undefined, date: today() }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sheets', child.id] }),
  })
  const savedThis = save.isSuccess && save.variables === sheet.data

  const reprint = (s: Sheet) => {
    setType(s.spec.type)
    setLevel(s.spec.level)
    setPaperSize(s.spec.paperSize)
    setLetters(s.spec.letters ?? '')
    setTheme(s.spec.theme)
    sheet.mutate(s.spec)
  }

  return (
    <main className="mx-auto grid max-w-5xl gap-6 px-4 py-6 md:grid-cols-[18rem_1fr] print:block print:max-w-none print:p-0">
      <aside className="no-print space-y-6">
        <section className="space-y-4 rounded-xl bg-white p-4 shadow-sm">
          <Field label="Sheet">
            <select className={inputClass} value={type} onChange={(e) => setType(e.target.value as SheetType)}>
              {sheetGroups.map((g) => (
                <optgroup key={g.module} label={g.module}>
                  {g.types.map((t) => (
                    <option key={t.value} value={t.value}>{t.label}</option>
                  ))}
                </optgroup>
              ))}
            </select>
          </Field>

          <Field label="Topic">
            <select className={inputClass} value={theme} onChange={(e) => setTheme(e.target.value)}>
              {(categories.data ?? [{ key: theme, name: theme, wordCount: 0, hasPictures: true, hasImagePictures: false }]).map((c) => (
                <option key={c.key} value={c.key}>{c.name}{c.hasPictures ? '' : ' (words only)'}</option>
              ))}
            </select>
          </Field>
          <TopicHint topic={topic} type={type} />

          <Field label="Level">
            <Segmented options={levels} value={level} onChange={setLevel} />
          </Field>

          {type === 'EnglishTracing' && (
            <Field label="Letters or numbers">
              <input className={inputClass} value={letters} maxLength={36} onChange={(e) => setLetters(e.target.value.toUpperCase())} placeholder="e.g. B or ABC (blank = any letter)" />
            </Field>
          )}
          {scriptTracing[type] && (
            <Field label="Letters (optional)">
              <input
                className={inputClass}
                value={letters}
                maxLength={60}
                onChange={(e) => setLetters(e.target.value)}
                placeholder={type === 'TeluguTracing' ? 'e.g. క or క, కా, కి' : 'e.g. क or क, का, कि'}
              />
            </Field>
          )}

          <Field label="Paper">
            <Segmented options={paperSizes} value={paperSize} onChange={setPaperSize} />
          </Field>

          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={includeAnswerKey} onChange={(e) => setIncludeAnswerKey(e.target.checked)} />
            Print answer key (parent page)
          </label>

          <div className="flex flex-col gap-2">
            <button className={primaryButton} disabled={sheet.isPending} onClick={() => sheet.mutate({})}>
              {sheet.data ? 'New sheet' : 'Make sheet'}
            </button>
            {sheet.data && (
              <>
                <button className={secondaryButton} onClick={() => window.print()}>Print</button>
                <button className={secondaryButton} disabled={save.isPending || savedThis} onClick={() => save.mutate(sheet.data)}>
                  {savedThis ? 'Saved ✓' : save.isPending ? 'Saving…' : 'Save to my sheets'}
                </button>
              </>
            )}
          </div>
          <ErrorText error={sheet.error ?? save.error} />
        </section>

        <History child={child} onReprint={reprint} />
      </aside>

      <section className="space-y-6 print:space-y-0">
        {sheet.data ? (
          <Preview sheet={sheet.data} includeAnswerKey={includeAnswerKey} />
        ) : (
          <div className="no-print grid h-96 place-items-center rounded-xl border-2 border-dashed border-slate-300 text-slate-500">
            {sheet.isPending ? 'Making your sheet…' : `Make a sheet for ${child.firstName}.`}
          </div>
        )}
      </section>
    </main>
  )
}

function History({ child, onReprint }: { child: Child; onReprint: (s: Sheet) => void }) {
  const queryClient = useQueryClient()
  const [favouritesOnly, setFavouritesOnly] = useState(false)
  const sheets = useQuery({
    queryKey: ['sheets', child.id, favouritesOnly],
    queryFn: () => api.sheets(child.id, favouritesOnly),
  })
  const favourite = useMutation({
    mutationFn: (s: Sheet) => api.setFavourite(child.id, s.id, !s.isFavourite),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sheets', child.id] }),
  })

  return (
    <section className="space-y-3 rounded-xl bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="font-bold">{favouritesOnly ? 'Favourites' : 'Saved sheets'}</h2>
        <button className="text-sm text-blue-700 underline" onClick={() => setFavouritesOnly(!favouritesOnly)}>
          {favouritesOnly ? 'Show recent' : 'Show favourites'}
        </button>
      </div>
      {sheets.isPending && <p className="text-sm text-slate-500">Loading…</p>}
      <ErrorText error={sheets.error} />
      {sheets.data?.length === 0 && <p className="text-sm text-slate-500">Nothing saved yet. Press “Save to my sheets” on one you like.</p>}
      <ul className="space-y-2">
        {sheets.data?.map((s) => (
          <li key={s.id} className="flex items-center gap-2 text-sm">
            <button
              aria-label={s.isFavourite ? 'Remove from favourites' : 'Add to favourites'}
              className="text-lg leading-none"
              onClick={() => favourite.mutate(s)}
            >
              {s.isFavourite ? '★' : '☆'}
            </button>
            <span className="flex-1 truncate" title={s.instruction}>
              {s.title} <span className="text-slate-500">· {s.spec.level}</span>
            </span>
            <button className="text-blue-700 underline" onClick={() => onReprint(s)}>Reprint</button>
          </li>
        ))}
      </ul>
    </section>
  )
}

function Preview({ sheet, includeAnswerKey }: { sheet: GeneratedSheet; includeAnswerKey: boolean }) {
  // The SVG comes from our own API and contains only generated line art and escaped text.
  return (
    <>
      <div className="sheet print-page mx-auto max-w-2xl bg-white shadow" dangerouslySetInnerHTML={{ __html: sheet.svg }} />
      {includeAnswerKey && sheet.answerKeySvg && (
        <div className="sheet print-page mx-auto max-w-2xl bg-white shadow" dangerouslySetInnerHTML={{ __html: sheet.answerKeySvg }} />
      )}
    </>
  )
}

function TopicHint({ topic, type }: { topic?: Category; type: SheetType }) {
  const script = scriptTracing[type]
  if (script)
    return (
      <p className="text-xs text-slate-600">
        Uses this topic's words that are written in {script} (e.g. your {script} Varnamala list). Otherwise: Easy vowels, Medium consonants,
        Challenge consonant + vowel sign.
      </p>
    )
  if (!topic || topic.key === 'vehicles') return null
  let hint: string | null = null
  if (imageSheets.includes(type)) {
    if (!topic.hasImagePictures)
      hint = `This sheet needs approved AI pictures (Admin → AI pictures); until ${topic.name} has them, it uses a vehicle.`
  } else if (!topicSheets.includes(type))
    hint = `This sheet uses vehicle pictures; ${topic.name} works on the other picture and word sheets.`
  else if (!topic.hasPictures && !wordSheets.includes(type))
    hint = `${topic.name} has no pictures yet, so this sheet uses vehicle pictures. Word search and word magic use its words.`
  return hint ? <p className="text-xs text-amber-700">{hint}</p> : null
}
