import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, ArrowRight, CheckCircle2, Timer } from 'lucide-react'
import { useResource } from '../../lib/queries'
import { ErrorMessage, Loading } from '../../components/Feedback'
import type { Dish } from '../../types/models'
type Session = { step: number; deadline: number | null; complete: boolean }
function load(key: string): { session: Session; warning: string } {
  const initial = { step: 0, deadline: null, complete: false }
  try {
    const raw = localStorage.getItem(key)
    if (!raw) return { session: initial, warning: '' }
    const parsed: unknown = JSON.parse(raw)
    if (
      typeof parsed !== 'object' ||
      !parsed ||
      !('step' in parsed) ||
      !Number.isInteger(parsed.step) ||
      Number(parsed.step) < 0 ||
      !('deadline' in parsed) ||
      (parsed.deadline !== null && typeof parsed.deadline !== 'number') ||
      !('complete' in parsed) ||
      typeof parsed.complete !== 'boolean'
    )
      throw new Error('Invalid saved progress')
    return { session: parsed as Session, warning: '' }
  } catch {
    return {
      session: initial,
      warning: 'Saved progress could not be restored. You can still cook from the first step.',
    }
  }
}
export function CookingPage() {
  const { id } = useParams()
  const dish = useResource<Dish>(`/dishes/${id}`)
  if (dish.isPending) return <Loading />
  if (!dish.data) return <ErrorMessage error={dish.error} />
  return <CookingGuide key={id} dish={dish.data} />
}
function CookingGuide({ dish }: { dish: Dish }) {
  const key = `dishdash.cooking.${dish.id}`
  const [initial] = useState(() => load(key))
  const [session, setSession] = useState(initial.session)
  const [warning, setWarning] = useState(initial.warning)
  const [now, setNow] = useState(() => Date.now())
  const steps = [...dish.steps].sort((a, b) => a.position - b.position)
  const index = Math.min(session.step, steps.length - 1)
  const current = steps[index]
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [])
  const remaining = session.deadline ? Math.max(0, Math.ceil((session.deadline - now) / 1000)) : 0
  useEffect(() => {
    const protect = (event: BeforeUnloadEvent) => {
      event.preventDefault()
    }
    if (remaining > 0) window.addEventListener('beforeunload', protect)
    return () => window.removeEventListener('beforeunload', protect)
  }, [remaining])
  function update(value: Session) {
    setSession(value)
    try {
      localStorage.setItem(key, JSON.stringify(value))
      setWarning('')
    } catch {
      setWarning('Progress cannot be saved in this browser. Keep this page open while cooking.')
    }
  }
  return (
    <div className="page cooking-page">
      <Link className="back-link" to={`/dishes/${dish.id}`}>
        <ArrowLeft size={16} /> Back to the recipe
      </Link>
      <p className="eyebrow">A LITTLE GUIDANCE. A GREAT MEAL.</p>
      <h1>{dish.name}</h1>
      <p className="lede">One step at a time. You’ve got this.</p>
      {warning && (
        <p className="error" role="alert">
          {warning}
        </p>
      )}
      <div className="cooking-layout">
        <aside className="panel">
          <h2>Your recipe roadmap</h2>
          <ol className="cooking-roadmap">
            {steps.map((s, i) => (
              <li key={s.id} className={index === i ? 'current' : ''}>
                <button
                  onClick={() => update({ step: i, deadline: null, complete: false })}
                  aria-current={index === i ? 'step' : undefined}
                >
                  <span>{i < index || session.complete ? <CheckCircle2 size={20} /> : i + 1}</span>
                  Step {i + 1}
                </button>
              </li>
            ))}
          </ol>
          <p className="small-note">
            Progress is saved on this device. Timers continue when you return.
          </p>
        </aside>
        <section className="panel active-step">
          {session.complete ? (
            <>
              <CheckCircle2 size={48} />
              <h2>Made by you. Enjoy!</h2>
              <p>Your cooking guide is complete.</p>
              <button
                className="button secondary"
                onClick={() => update({ step: 0, deadline: null, complete: false })}
              >
                Start again
              </button>
            </>
          ) : (
            <>
              <span className="eyebrow">
                STEP {index + 1} OF {steps.length}
              </span>
              <progress aria-label="Cooking progress" max={steps.length} value={index + 1} />
              <h2>{current.instruction}</h2>
              {current.timerSeconds > 0 && (
                <div className="timer">
                  <Timer size={24} />
                  {session.deadline ? (
                    <>
                      <output aria-label="Timer remaining">
                        {Math.floor(remaining / 60)}:{String(remaining % 60).padStart(2, '0')}
                      </output>
                      {remaining === 0 && (
                        <span role="status">Timer finished. Check your food before moving on.</span>
                      )}
                      <button
                        className="text-button"
                        onClick={() => update({ ...session, deadline: null })}
                      >
                        Reset timer
                      </button>
                    </>
                  ) : (
                    <button
                      className="button secondary"
                      onClick={() => {
                        setNow(Date.now())
                        update({ ...session, deadline: Date.now() + current.timerSeconds * 1000 })
                      }}
                    >
                      Start {Math.round(current.timerSeconds / 60)} minute timer
                    </button>
                  )}
                </div>
              )}
              <div className="button-row">
                <button
                  className="button secondary"
                  disabled={index === 0}
                  onClick={() => update({ step: index - 1, deadline: null, complete: false })}
                >
                  <ArrowLeft size={17} /> Previous
                </button>
                <button
                  className="button"
                  onClick={() =>
                    update({
                      step: Math.min(index + 1, steps.length - 1),
                      deadline: null,
                      complete: index === steps.length - 1,
                    })
                  }
                >
                  {index === steps.length - 1 ? 'Finish cooking' : 'Next step'}
                  <ArrowRight size={17} />
                </button>
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  )
}
