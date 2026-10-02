import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Check, CheckCircle2, Package, Star } from 'lucide-react'
import { useAction, useResource } from '../../lib/queries'
import { money } from '../../lib/api'
import { Loading, ErrorMessage } from '../../components/Feedback'
import { statusLabel } from './OrdersPage'
import type { Order, Rating } from '../../types/models'
export function OrderDetailPage() {
  const { id } = useParams()
  const [params] = useSearchParams()
  const order = useResource<Order>(`/orders/${id}`)
  const advance = useAction(`/orders/${id}/advance-demo`)
  const ratings = useResource<Rating[]>('/ratings')
  if (order.isPending) return <Loading />
  if (!order.data) return <ErrorMessage error={order.error} retry={() => void order.refetch()} />
  const value = order.data
  const statuses = ['Confirmed', 'Preparing', 'OutForDelivery', 'Delivered']
  const rating = ratings.data?.find((r) => r.orderId === id)
  return (
    <div className="page narrow">
      <Link className="back-link" to="/orders">
        ← Order history
      </Link>
      {params.has('confirmed') && (
        <div className="success" role="status">
          <CheckCircle2 /> Your demo order is confirmed. No payment was taken.
        </div>
      )}
      <p className="eyebrow">DINNER IS TAKING SHAPE</p>
      <h1>{value.status === 'Delivered' ? 'Enjoy every bite.' : 'Good things are on the way.'}</h1>
      <p className="lede">Demo order · {value.id.slice(0, 8).toUpperCase()}</p>
      <section className="panel">
        <div className="section-heading">
          <h2>
            <Package size={22} /> Your order journey
          </h2>
          <span className="pill">Simulation</span>
        </div>
        <p>
          Advance each step to explore the delivery experience. This is a demo timeline, with no
          live delivery or GPS tracking.
        </p>
        <ol className="timeline">
          {statuses.map((s, i) => {
            const done = i <= statuses.indexOf(value.status)
            const event = value.events.find((e) => e.status === s)
            return (
              <li className={done ? 'done' : ''} key={s}>
                <span>{done ? <Check size={18} /> : i + 1}</span>
                <div>
                  <strong>{statusLabel(s)}</strong>
                  <small>
                    {event
                      ? new Date(event.createdAt).toLocaleTimeString('en-CA', {
                          hour: '2-digit',
                          minute: '2-digit',
                        })
                      : 'Next in the demo'}
                  </small>
                </div>
              </li>
            )
          })}
        </ol>
        <ErrorMessage error={advance.error} />
        {value.status !== 'Delivered' && (
          <button
            className="button"
            disabled={advance.isPending}
            onClick={() => advance.mutate(undefined)}
          >
            Advance demo status
          </button>
        )}
      </section>
      <section className="panel">
        <h2>What’s on the menu</h2>
        {value.items.map((item, i) => (
          <div className="summary-row" key={i}>
            <div>
              <h3>{item.dishName}</h3>
              <p>
                {item.kind === 'Cook' ? 'Cook it' : 'Order it'} · {item.servings} servings ·{' '}
                {item.providerName}
              </p>
              {item.kind === 'Cook' && (
                <Link className="text-link" to={`/dishes/${item.dishId}/cook`}>
                  Start guided cooking →
                </Link>
              )}
            </div>
            <strong>{money(item.total)}</strong>
          </div>
        ))}
        <div className="summary-row total">
          <strong>Total · CAD</strong>
          <strong>{money(value.total)}</strong>
        </div>
      </section>
      {value.status === 'Delivered' && (
        <RatingForm key={rating?.id ?? 'new'} orderId={value.id} rating={rating} />
      )}
    </div>
  )
}
function RatingForm({ orderId, rating }: { orderId: string; rating?: Rating }) {
  const [stars, setStars] = useState(rating?.stars ?? 5)
  const [comment, setComment] = useState(rating?.comment ?? '')
  const save = useAction('/ratings', 'PUT')
  const remove = useAction(`/ratings/${rating?.id}`, 'DELETE')
  return (
    <form
      className="panel"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate({ orderId, stars, comment })
      }}
    >
      <p className="eyebrow">HOW WAS YOUR MEAL?</p>
      <h2>
        <Star size={22} /> Leave a little feedback
      </h2>
      <label>
        Your rating
        <select value={stars} onChange={(e) => setStars(Number(e.target.value))}>
          {[5, 4, 3, 2, 1].map((n) => (
            <option key={n} value={n}>
              {n} star{n > 1 ? 's' : ''}
            </option>
          ))}
        </select>
      </label>
      <label>
        Comment (optional)
        <textarea
          maxLength={500}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          rows={3}
        />
      </label>
      <ErrorMessage error={save.error ?? remove.error} />
      {save.isSuccess && (
        <p className="success" role="status">
          Your rating is saved.
        </p>
      )}
      <div className="button-row">
        <button className="button" disabled={save.isPending}>
          {rating ? 'Update rating' : 'Save rating'}
        </button>
        {rating && (
          <button
            className="button secondary"
            type="button"
            disabled={remove.isPending}
            onClick={() => remove.mutate(undefined)}
          >
            Delete rating
          </button>
        )}
      </div>
    </form>
  )
}
