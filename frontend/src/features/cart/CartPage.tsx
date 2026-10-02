import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { CheckCircle2, ShieldCheck, Trash2 } from 'lucide-react'
import { useAction, useResource } from '../../lib/queries'
import { money } from '../../lib/api'
import { Empty, ErrorMessage, Loading } from '../../components/Feedback'
import type { CartLine, Order } from '../../types/models'
export function CartPage() {
  const cart = useResource<CartLine[]>('/cart')
  const remove = useAction<void, { id: string }>((v) => `/cart/${v.id}`, 'DELETE')
  const clear = useAction('/cart', 'DELETE')
  const checkout = useAction<Order>('/orders')
  const [outcome, setOutcome] = useState('success')
  const [requestKey] = useState(() => crypto.randomUUID())
  const navigate = useNavigate()
  if (cart.isPending) return <Loading />
  if (!cart.data)
    return (
      <div className="page">
        <h1>Review your basket</h1>
        <ErrorMessage error={cart.error} retry={() => void cart.refetch()} />
        <p>
          A changed allergy preference or unavailable option can prevent this basket from being
          quoted. You can clear it and choose a new dish.
        </p>
        <ErrorMessage error={clear.error} />
        <button
          className="button secondary"
          disabled={clear.isPending}
          onClick={() => clear.mutate(undefined)}
        >
          Clear basket
        </button>
      </div>
    )
  if (!cart.data.length)
    return (
      <Empty
        title="Your basket is waiting"
        text="Choose a dish, compare your options and add something delicious."
      />
    )
  const total = Math.round(cart.data.reduce((sum, x) => sum + x.total, 0) * 100) / 100
  return (
    <div className="page">
      <p className="eyebrow">A GOOD CHOICE IS ON THE MENU</p>
      <h1>Your basket</h1>
      <p className="lede">A final look before your demo order.</p>
      <div className="checkout-grid">
        <section className="panel">
          <ErrorMessage error={remove.error} />
          {cart.data.map((line) => (
            <article className="cart-line" key={line.id}>
              <span className={`path-icon ${line.kind === 'Cook' ? '' : 'warm-bg'}`}>
                {line.kind === 'Cook' ? 'C' : 'O'}
              </span>
              <div>
                <Link to={`/dishes/${line.dishId}`}>
                  <h2>{line.dishName}</h2>
                </Link>
                <p>
                  {line.kind === 'Cook' ? 'Cook it' : 'Order it'} · {line.servings} servings
                </p>
                <small>{line.providerName}</small>
              </div>
              <strong>{money(line.total)}</strong>
              <button
                className="icon-button"
                aria-label={`Remove ${line.dishName}`}
                disabled={remove.isPending}
                onClick={() => remove.mutate({ id: line.id })}
              >
                <Trash2 size={18} />
              </button>
            </article>
          ))}
          <Link to="/discover" className="text-link">
            + Add another dish
          </Link>
        </section>
        <form
          className="panel order-summary"
          onSubmit={async (e) => {
            e.preventDefault()
            try {
              const order = await checkout.mutateAsync({
                requestKey,
                outcome,
                expectedTotal: total,
              })
              navigate(`/orders/${order.id}?confirmed=1`)
            } catch {
              /* Payment feedback is displayed below. */
            }
          }}
        >
          <h2>Demo checkout</h2>
          <div className="summary-row">
            <span>Basket total</span>
            <strong>{money(total)}</strong>
          </div>
          <small>CAD · delivery included for restaurant items. Taxes and tips are excluded.</small>
          <div className="safe-payment">
            <ShieldCheck />
            <div>
              <strong>No real payment. Ever.</strong>
              <p>This simulation places no real orders and collects no payment details.</p>
            </div>
          </div>
          <label>
            Demo payment result
            <select value={outcome} onChange={(e) => setOutcome(e.target.value)}>
              <option value="success">Simulate successful payment</option>
              <option value="decline">Simulate declined payment</option>
            </select>
          </label>
          <ErrorMessage error={checkout.error} />
          <button className="button" disabled={checkout.isPending}>
            <CheckCircle2 size={18} />
            {checkout.isPending ? 'Confirming…' : 'Place demo order'}
          </button>
        </form>
      </div>
    </div>
  )
}
