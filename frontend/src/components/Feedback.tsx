import { Link } from 'react-router-dom'
import { AlertCircle, UtensilsCrossed } from 'lucide-react'
export function Loading() {
  return (
    <div className="feedback" role="status">
      <span className="spinner" /> Finding something good…
    </div>
  )
}
export function ErrorMessage({ error, retry }: { error: Error | null; retry?: () => void }) {
  return error ? (
    <div className="error" role="alert">
      <AlertCircle size={20} />
      <span>{error.message}</span>
      {retry && <button onClick={retry}>Try again</button>}
    </div>
  ) : null
}
export function Empty({ title, text }: { title: string; text: string }) {
  return (
    <div className="feedback empty">
      <UtensilsCrossed size={34} />
      <h2>{title}</h2>
      <p>{text}</p>
      <Link className="button secondary" to="/discover">
        Explore dishes
      </Link>
    </div>
  )
}
