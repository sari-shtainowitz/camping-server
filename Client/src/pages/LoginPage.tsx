import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../api'
import { useAuth } from '../auth'

type Mode = 'login' | 'register'

function TentMark() {
  return (
    <svg viewBox="0 0 24 24" width="24" height="24" aria-hidden="true" focusable="false">
      <path d="M12 3 21 20H3Z" fill="currentColor" />
      <path d="M12 9.5 16.4 20H7.6Z" fill="var(--paper)" />
    </svg>
  )
}

// Login and Registration. The auth/JWT logic lives in ../auth + ../api, which are
// unchanged - this file is presentation/wording only.
export function LoginPage() {
  const { login, register } = useAuth()
  const navigate = useNavigate()

  const [mode, setMode] = useState<Mode>('login')
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      if (mode === 'login') {
        await login(email.trim(), password)
      } else {
        await register({
          fullName: fullName.trim(),
          email: email.trim(),
          phoneNumber: phoneNumber.trim() || undefined,
          password,
        })
      }
      // Land on the role's home screen (see route table in App.tsx).
      navigate('/', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : (err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const isLogin = mode === 'login'

  return (
    <div className="auth-screen">
      <div className="card auth-card">
        <div className="auth-card__brand">
          <span className="brand__mark brand__mark--lg">
            <TentMark />
          </span>
          <span className="brand__name">מחנה האורנים</span>
        </div>

        <h1 className="auth-card__title">{isLogin ? 'ברוכים השבים' : 'יצירת חשבון'}</h1>
        <p className="auth-card__hint">
          {isLogin
            ? 'התחברו כדי להזמין אוהל במחנה.'
            : 'הירשמו כדי להתחיל להזמין אוהלים.'}
        </p>

        <div className="segmented" role="tablist" aria-label="מצב הזדהות">
          <button
            type="button"
            role="tab"
            aria-selected={isLogin}
            className="segmented__btn"
            onClick={() => setMode('login')}
          >
            התחברות
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={!isLogin}
            className="segmented__btn"
            onClick={() => setMode('register')}
          >
            הרשמה
          </button>
        </div>

        <form className="form" onSubmit={submit}>
          {!isLogin && (
            <div className="field">
              <label htmlFor="fullName">שם מלא</label>
              <input
                id="fullName"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                required
                minLength={2}
                autoComplete="name"
              />
            </div>
          )}

          <div className="field">
            <label htmlFor="email">דוא"ל</label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
            />
          </div>

          {!isLogin && (
            <div className="field">
              <label htmlFor="phone">
                טלפון <span className="field__opt">לא חובה</span>
              </label>
              <input
                id="phone"
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                autoComplete="tel"
              />
            </div>
          )}

          <div className="field">
            <label htmlFor="password">סיסמה</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={8}
              autoComplete={isLogin ? 'current-password' : 'new-password'}
            />
          </div>

          {error && (
            <p className="banner banner--error" role="alert">
              {error}
            </p>
          )}

          <button className="btn btn--primary btn--block" type="submit" disabled={busy}>
            {busy ? 'רגע…' : isLogin ? 'התחברות' : 'יצירת חשבון'}
          </button>
        </form>

        <p className="auth-card__note">
          לאחר ההתחברות ה־JWT נשמר ב־<code dir="ltr">localStorage</code> ונשלח בכל
          בקשה ל־API בכותרת <code dir="ltr">Authorization: Bearer …</code>.
        </p>
      </div>
    </div>
  )
}
