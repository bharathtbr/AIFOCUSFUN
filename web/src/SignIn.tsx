import { useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { api, setToken } from './api'
import { Card, ErrorText, Field, inputClass, primaryButton } from './ui'

export default function SignIn() {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const submit = useMutation({
    mutationFn: () => (mode === 'login' ? api.login(email, password) : api.register(email, password)),
    onSuccess: ({ token }) => setToken(token),
  })

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    submit.mutate()
  }

  return (
    <Card title={mode === 'login' ? 'Sign in to Focus Fun' : 'Create the parent account'}>
      <form className="space-y-4" onSubmit={onSubmit}>
        <Field label={mode === 'login' ? 'Email or username' : 'Email'}>
          <input
            className={inputClass}
            type={mode === 'login' ? 'text' : 'email'}
            autoComplete="username"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </Field>
        <Field label="Password">
          <input
            className={inputClass}
            type="password"
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            minLength={mode === 'register' ? 10 : undefined}
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </Field>
        <button className={`${primaryButton} w-full`} disabled={submit.isPending}>
          {mode === 'login' ? 'Sign in' : 'Create account'}
        </button>
        <ErrorText error={submit.error} />
      </form>
      <button
        type="button"
        className="text-sm text-blue-700 underline"
        onClick={() => {
          setMode(mode === 'login' ? 'register' : 'login')
          submit.reset()
        }}
      >
        {mode === 'login' ? 'First time here? Create the parent account' : 'Already have an account? Sign in'}
      </button>
    </Card>
  )
}
