import { useState, useSyncExternalStore } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, getToken, isAdmin, setToken } from './api'
import Admin from './Admin'
import SignIn from './SignIn'
import ChildForm from './ChildForm'
import SheetMaker from './SheetMaker'
import { ErrorText } from './ui'

const subscribeToAuth = (onChange: () => void) => {
  window.addEventListener('focusfun-auth', onChange)
  return () => window.removeEventListener('focusfun-auth', onChange)
}

export default function App() {
  const token = useSyncExternalStore(subscribeToAuth, getToken)
  const queryClient = useQueryClient()
  const admin = isAdmin(token)
  const [page, setPage] = useState<'sheets' | 'admin'>('sheets')

  const signOut = () => {
    setToken(null)
    queryClient.clear()
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900 print:bg-white">
      <header className="no-print border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-4">
          <div>
            <h1 className="text-2xl font-bold">Focus Fun</h1>
            <p className="text-slate-600">Pick a sheet, preview it, print it.</p>
          </div>
          {token && (
            <nav className="flex items-center gap-4 text-sm">
              {admin && (
                <>
                  <NavButton active={page === 'sheets'} onClick={() => setPage('sheets')}>Sheets</NavButton>
                  <NavButton active={page === 'admin'} onClick={() => setPage('admin')}>Admin</NavButton>
                </>
              )}
              <button className="text-blue-700 underline" onClick={signOut}>Sign out</button>
            </nav>
          )}
        </div>
      </header>

      {!token ? <SignIn /> : admin && page === 'admin' ? <Admin /> : <SignedIn />}
    </div>
  )
}

function SignedIn() {
  const children = useQuery({ queryKey: ['children'], queryFn: api.children })

  if (children.isPending) return <p className="p-6 text-slate-500">Loading…</p>
  if (children.isError) return <div className="p-6"><ErrorText error={children.error} /></div>
  if (children.data.length === 0) return <ChildForm />
  return <SheetMaker child={children.data[0]} />
}

function NavButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button className={active ? 'font-bold text-slate-900' : 'text-slate-600 hover:text-slate-900'} aria-current={active ? 'page' : undefined} onClick={onClick}>
      {children}
    </button>
  )
}
