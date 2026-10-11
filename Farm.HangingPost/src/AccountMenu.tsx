import { signInUrl, type Session } from './session'

interface AccountMenuProps {
  session: Session
  loading: boolean
  pathname: string
  onNavigate: (path: string) => void
}

export function AccountMenu({ session, loading, pathname, onNavigate }: AccountMenuProps) {
  if (loading || !session.signInAvailable) return null

  if (!session.isAuthenticated) {
    return (
      <div className="account">
        <a
          className="account-button account-button-primary"
          href={signInUrl('/connect')}
          title="Sign in to give your AI assistants one private memory"
        >
          Sign in to connect your AI
        </a>
      </div>
    )
  }

  const label = session.name ?? session.username ?? 'Account'

  return (
    <div className="account">
      <a
        className="account-link"
        href="/connect"
        onClick={(event) => {
          event.preventDefault()
          onNavigate('/connect')
        }}
      >
        Connect AI
      </a>
      <span className="account-user" title={session.email ?? undefined}>
        {session.picture ? <img className="account-avatar" src={session.picture} alt="" width={28} height={28} /> : null}
        <span className="account-name">{label}</span>
      </span>
      <form method="post" action={`/auth/logout?returnUrl=${encodeURIComponent(pathname)}`}>
        <button className="account-button" type="submit">
          Sign out
        </button>
      </form>
    </div>
  )
}
