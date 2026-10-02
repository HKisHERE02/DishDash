import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Leaf } from 'lucide-react'
import { useAction } from '../../lib/queries'
import { ErrorMessage } from '../../components/Feedback'
export function AuthPage({ register = false }: { register?: boolean }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setName] = useState('')
  const action = useAction(`/auth/${register ? 'register' : 'login'}`)
  const navigate = useNavigate()
  const client = useQueryClient()
  return (
    <div className="auth-layout">
      <div className="auth-story">
        <Leaf size={42} />
        <p className="eyebrow">GOOD FOOD. BETTER CHOICES.</p>
        <h1>
          Your next great meal
          <br />
          <em>starts here.</em>
        </h1>
        <p>
          A little inspiration. A clear comparison.
          <br />A delicious decision that’s entirely yours.
        </p>
        <span className="pill">Cook it. Order it. Make it yours.</span>
      </div>
      <form
        className="panel auth-form"
        onSubmit={async (e) => {
          e.preventDefault()
          try {
            await action.mutateAsync({ email, password, displayName })
            client.clear()
            navigate('/discover')
          } catch {
            /* The mutation error is displayed below. */
          }
        }}
      >
        <p className="eyebrow">WELCOME TO THE TABLE</p>
        <h2>{register ? 'Create your account' : 'Good to see you again'}</h2>
        <p>
          {register
            ? 'Save your favourites and find meals that fit your day.'
            : 'Sign in to pick up where you left off.'}
        </p>
        {register && (
          <label>
            Your name
            <input
              autoComplete="name"
              required
              maxLength={60}
              value={displayName}
              onChange={(e) => setName(e.target.value)}
            />
          </label>
        )}
        <label>
          Email address
          <input
            type="email"
            autoComplete="email"
            required
            maxLength={254}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label>
          Password
          <input
            type="password"
            required
            minLength={register ? 12 : 1}
            maxLength={128}
            autoComplete={register ? 'new-password' : 'current-password'}
            aria-describedby={register ? 'password-help' : undefined}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        {register && (
          <small id="password-help">
            Use 12+ characters with uppercase, lowercase, a number and a symbol.
          </small>
        )}
        <ErrorMessage error={action.error} />
        <button className="button" disabled={action.isPending}>
          {action.isPending ? 'One moment…' : register ? 'Create account' : 'Sign in'}
        </button>
        <p>
          {register ? 'Already have an account?' : 'New to DishDash?'}{' '}
          <Link to={register ? '/login' : '/register'}>
            {register ? 'Sign in' : 'Create an account'}
          </Link>
        </p>
        <small>This is a demo. No real orders or payments are processed.</small>
      </form>
    </div>
  )
}
