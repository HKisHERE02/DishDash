import { Link } from 'react-router-dom'
import { ArrowUpRight } from 'lucide-react'
import { useResource } from '../../lib/queries'
import { money } from '../../lib/api'
import { Empty, ErrorMessage, Loading } from '../../components/Feedback'
import type { Order } from '../../types/models'
export const statusLabel = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2')
export function OrdersPage() {
  const orders = useResource<Order[]>('/orders')
  return (
    <div className="page">
      <p className="eyebrow">YOUR MEALS, REMEMBERED</p>
      <h1>Order history</h1>
      <p className="lede">Every demo order, all in one place.</p>
      {orders.isPending ? (
        <Loading />
      ) : orders.error ? (
        <ErrorMessage error={orders.error} retry={() => void orders.refetch()} />
      ) : !orders.data?.length ? (
        <Empty
          title="Your first meal is ahead of you"
          text="Try a Cook It or Order It demo to see your history here."
        />
      ) : (
        <div className="order-list">
          {orders.data.map((o) => (
            <Link to={`/orders/${o.id}`} className="panel order-preview" key={o.id}>
              <div>
                <span className="pill">{statusLabel(o.status)}</span>
                <h2>{o.items.map((i) => i.dishName).join(' + ')}</h2>
                <p>
                  {new Date(o.createdAt).toLocaleDateString('en-CA', {
                    month: 'long',
                    day: 'numeric',
                    year: 'numeric',
                  })}{' '}
                  · {o.items.length} item{o.items.length !== 1 ? 's' : ''}
                </p>
              </div>
              <strong>{money(o.total)}</strong>
              <ArrowUpRight />
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
