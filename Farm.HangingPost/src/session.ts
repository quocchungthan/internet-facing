import { useEffect, useState } from 'react'

export interface Session {
  isAuthenticated: boolean
  signInAvailable: boolean
  subject: string | null
  name: string | null
  username: string | null
  picture: string | null
  email: string | null
}

const signedOut: Session = {
  isAuthenticated: false,
  signInAvailable: false,
  subject: null,
  name: null,
  username: null,
  picture: null,
  email: null,
}

type SessionState = { status: 'loading' | 'ready'; session: Session }

/** Reads the Fences-backed session from the Farm API (`/api/me`). Never throws; falls back to signed out. */
export function useSession(): SessionState {
  const [state, setState] = useState<SessionState>({ status: 'loading', session: signedOut })

  useEffect(() => {
    const controller = new AbortController()

    fetch('/api/me', { credentials: 'same-origin', signal: controller.signal })
      .then((response) => (response.ok ? (response.json() as Promise<Session>) : signedOut))
      .catch(() => signedOut)
      .then((session) => {
        if (!controller.signal.aborted) setState({ status: 'ready', session })
      })

    return () => controller.abort()
  }, [])

  return state
}

export function signInUrl(returnPath: string): string {
  return `/auth/login?returnUrl=${encodeURIComponent(returnPath)}`
}
