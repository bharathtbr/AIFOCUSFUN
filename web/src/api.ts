export type SheetType =
  | 'RoadMaze'
  | 'DotToDot'
  | 'Pattern'
  | 'EnglishTracing'
  | 'Coloring'
  | 'ColorByNumber'
  | 'SpotTheDifference'
  | 'WordSearch'
  | 'OddOneOut'
  | 'CountVehicles'
  | 'RoadTracing'
  | 'Maths'
  | 'WordMagic'
  | 'TeluguTracing'
  | 'HindiTracing'
  | 'PictureCards'
export type Level = 'Easy' | 'Medium' | 'Challenge'
export type PaperSize = 'Letter' | 'A4'
export type Script = 'en' | 'hi' | 'te'

export interface ChildLevels {
  puzzles: Level
  coloring: Level
  tracingEn: Level
  tracingHi: Level
  tracingTe: Level
  memory: Level
  paperArts: Level
}

export interface Child {
  id: string
  firstName: string
  age: number
  themes: string[]
  scripts: Script[]
  levels: ChildLevels
  paperSize: PaperSize
}

export interface ChildInput {
  firstName: string
  age: number
  scripts: Script[]
  paperSize: PaperSize
}

export interface SheetSpec {
  type: SheetType
  level: Level
  seed: string
  theme: string
  paperSize: PaperSize
  letters: string | null
}

export interface Sheet {
  id: string
  spec: SheetSpec
  title: string
  instruction: string
  isFavourite: boolean
  createdAt: string
  svgUrl: string
  answerKeySvgUrl: string | null
}

export interface CreateSheetInput {
  type: SheetType
  level: Level
  seed?: string
  paperSize: PaperSize
  theme?: string
  letters?: string
  date?: string
}

export interface GeneratedSheet {
  spec: SheetSpec
  title: string
  instruction: string
  svg: string
  answerKeySvg: string | null
}

const TOKEN_KEY = 'focusfun.token'

export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function setToken(token: string | null) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    // Storage unavailable (private mode): the session just won't survive a reload.
  }
  window.dispatchEvent(new Event('focusfun-auth'))
}

/** Admin rights come from the login token, so the menu updates as soon as an admin signs in. */
export function isAdmin(token: string | null): boolean {
  if (!token) return false
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')))
    return payload.admin === 'true'
  } catch {
    return false
  }
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getToken()
  const res = await fetch(`/api${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
  })
  if (res.status === 401 && token) setToken(null)
  if (!res.ok) throw new ApiError(res.status, await errorMessage(res))
  return res.status === 204 ? (undefined as T) : res.json()
}

async function errorMessage(res: Response): Promise<string> {
  try {
    const body = await res.json()
    if (body.errors) return Object.values(body.errors as Record<string, string[]>).flat().join(' ')
    return body.error ?? body.detail ?? body.title ?? `Request failed (${res.status})`
  } catch {
    return res.status === 401 ? 'Wrong email or password.' : `Request failed (${res.status})`
  }
}

const post = <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: JSON.stringify(body) })
const put = <T>(path: string, body: unknown) => request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
const del = (path: string) => request<void>(path, { method: 'DELETE' })

export interface Category {
  key: string
  name: string
  wordCount: number
  hasPictures: boolean
  /** Enough approved AI pictures: sheets use them, and dot-to-dot / colour-by-number can too. */
  hasImagePictures: boolean
}

export interface AdminCategory extends Category {
  id: string
  description: string | null
  isActive: boolean
  /** Pictures drawn in code (vehicles, animals, fruits). */
  hasDrawnPictures: boolean
  /** Picture sheets switch to approved AI pictures once there are enough. */
  useImagePictures: boolean
  approvedPictures: number
}

export interface CategoryWord {
  id: string
  word: string
  hindi: string | null
  telugu: string | null
  source: 'admin' | 'ai'
  pictureHint: string | null
}

export interface WordInput {
  word: string
  hindi?: string | null
  telugu?: string | null
  /** How to draw it, for AI pictures. */
  pictureHint?: string | null
}

export interface WordSuggestion extends WordInput {
  note: string | null
  /** The AI's idea for how to draw it; saved as the word's picture hint. */
  picture: string | null
}

export interface ImageModelInfo {
  id: string
  label: string
  pricePerImage: number
  note: string
}

export interface ImageSettings {
  configured: boolean
  provider: string
  models: ImageModelInfo[]
  defaultModel: string
  editModel: string
  versions: number
  minApprovedForSheets: number
}

export type PictureStatus = 'Draft' | 'Approved' | 'Rejected'

export interface WordPicture {
  id: string
  wordId: string
  status: PictureStatus
  model: string
  costUsd: number
  editedFromId: string | null
  createdAt: string
  url: string
  /** Looks clearly different mirrored, so odd one out may use it that way. */
  mirrorSafe: boolean
  /** Colourable areas found (colour-by-number needs 3+). */
  areas: number
  /** An outline was traced (dot-to-dot can use it). */
  hasOutline: boolean
}

export interface WordPictures {
  wordId: string
  word: string
  pictureHint: string | null
  pictures: WordPicture[]
}

export interface TopicPictures {
  spentUsd: number
  words: WordPictures[]
}

export interface AiStats {
  task: string
  calls: number
  reviewed: number
  inputTokens: number
  outputTokens: number
}

export const api = {
  register: (email: string, password: string) => post<{ token: string }>('/auth/register', { email, password }),
  login: (email: string, password: string) => post<{ token: string }>('/auth/login', { email, password }),

  children: () => request<Child[]>('/children'),
  createChild: (input: ChildInput) => post<Child>('/children', input),

  /** Generates a sheet to preview and print; nothing is stored. */
  generateSheet: (childId: string, input: CreateSheetInput) => post<GeneratedSheet>(`/children/${childId}/sheets/generate`, input),
  /** Stores a generated sheet (by its seed) in the saved list. */
  saveSheet: (childId: string, input: CreateSheetInput) => post<Sheet>(`/children/${childId}/sheets`, input),
  sheets: (childId: string, favourites = false) =>
    request<Sheet[]>(`/children/${childId}/sheets?take=20${favourites ? '&favourites=true' : ''}`),
  setFavourite: (childId: string, sheetId: string, isFavourite: boolean) =>
    put<Sheet>(`/children/${childId}/sheets/${sheetId}/favourite`, { isFavourite }),

  categories: () => request<Category[]>('/categories'),

  admin: {
    categories: () => request<AdminCategory[]>('/admin/categories'),
    createCategory: (name: string, description: string) => post<AdminCategory>('/admin/categories', { name, description }),
    updateCategory: (id: string, name: string, description: string, isActive: boolean, useImagePictures?: boolean) =>
      put<AdminCategory>(`/admin/categories/${id}`, { name, description, isActive, useImagePictures }),
    words: (categoryId: string) => request<CategoryWord[]>(`/admin/categories/${categoryId}/words`),
    /** Pass the suggestion's callId so the words you keep (with your edits) become training data for our own model. */
    addWords: (categoryId: string, words: WordInput[], aiCallId?: string) =>
      post<{ added: number }>(`/admin/categories/${categoryId}/words`, { words, fromAi: !!aiCallId, aiCallId }),
    updateWord: (id: string, word: WordInput) => put<CategoryWord>(`/admin/words/${id}`, word),
    deleteWord: (id: string) => del(`/admin/words/${id}`),
    /** Asks the AI (with web search) for new words; nothing is saved until you add them. */
    suggest: (categoryId: string, count: number, notes: string) =>
      post<{ callId: string; words: WordSuggestion[]; sources: string[] }>(`/admin/categories/${categoryId}/suggest`, { count, notes }),
    aiStats: () => request<AiStats[]>('/admin/ai/stats'),

    /** AI pictures (optional): whether drawing is switched on, and the models it can use. */
    imageSettings: () => request<ImageSettings>('/admin/images'),
    pictures: (categoryId: string) => request<TopicPictures>(`/admin/categories/${categoryId}/pictures`),
    /** Draws new versions of a word's picture as drafts (costs money per picture). */
    drawPictures: (wordId: string, model: string, count: number, matchStyle: boolean) =>
      post<WordPicture[]>(`/admin/words/${wordId}/pictures`, { model, count, matchStyle }),
    /** "Fix with words": a new draft edited from this picture. */
    editPicture: (pictureId: string, instruction: string) => post<WordPicture>(`/admin/pictures/${pictureId}/edit`, { instruction }),
    setPictureStatus: (pictureId: string, status: PictureStatus) => put<WordPicture>(`/admin/pictures/${pictureId}/status`, { status }),
    setPictureHint: (wordId: string, hint: string) => put<{ pictureHint: string | null }>(`/admin/words/${wordId}/picture-hint`, { hint }),
    setMirrorSafe: (pictureId: string, mirrorSafe: boolean) => put<WordPicture>(`/admin/pictures/${pictureId}/mirror-safe`, { mirrorSafe }),
    /** Downloads reviewed AI calls as chat-format JSONL for fine-tuning. */
    downloadTrainingData: async () => {
      const token = getToken()
      const res = await fetch('/api/admin/ai/training-data.jsonl', { headers: token ? { Authorization: `Bearer ${token}` } : {} })
      if (!res.ok) throw new ApiError(res.status, `Download failed (${res.status})`)
      const url = URL.createObjectURL(await res.blob())
      const link = document.createElement('a')
      link.href = url
      link.download = `focusfun-training-${new Date().toISOString().slice(0, 10)}.jsonl`
      link.click()
      URL.revokeObjectURL(url)
    },
  },
}
