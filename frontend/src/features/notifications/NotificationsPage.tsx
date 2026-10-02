import { Link } from 'react-router-dom'
import { Bell } from 'lucide-react'
import { useAction, useResource } from '../../lib/queries'
import { ErrorMessage, Loading, Empty } from '../../components/Feedback'
import type { Notice } from '../../types/models'
export function NotificationsPage() {
  const notices = useResource<Notice[]>('/notifications')
  const read = useAction<void, { id: string }>((v) => `/notifications/${v.id}/read`, 'PUT')
  return (
    <div className="page narrow">
      <p className="eyebrow">A LITTLE UPDATE</p>
      <h1>Your notifications</h1>
      <p className="lede">The latest from your demo orders.</p>
      <ErrorMessage error={read.error} />
      {notices.isPending ? (
        <Loading />
      ) : notices.error ? (
        <ErrorMessage error={notices.error} retry={() => void notices.refetch()} />
      ) : !notices.data?.length ? (
        <Empty
          title="All quiet at the table"
          text="Updates will appear here when you place a demo order."
        />
      ) : (
        notices.data.map((n) => (
          <article key={n.id} className={`panel notification ${n.isRead ? '' : 'unread'}`}>
            <Bell size={22} />
            <div>
              <Link to={`/orders/${n.orderId}`}>{n.message}</Link>
              <small>{new Date(n.createdAt).toLocaleString('en-CA')}</small>
            </div>
            {!n.isRead && (
              <button
                className="text-button"
                disabled={read.isPending}
                onClick={() => read.mutate({ id: n.id })}
              >
                Mark read
              </button>
            )}
          </article>
        ))
      )}
    </div>
  )
}
