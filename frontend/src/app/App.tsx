import { useEffect, useRef } from 'react'
import {
  Link,
  NavLink,
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router-dom'
import { Bell, ChefHat, Heart, LogOut, ShoppingBag, UserRound } from 'lucide-react'
import { useQueryClient } from '@tanstack/react-query'
import { useAction, useProfile, useResource } from '../lib/queries'
import { ErrorMessage, Loading } from '../components/Feedback'
import { AuthPage } from '../features/auth/AuthPage'
import { DiscoverPage } from '../features/dishes/DiscoverPage'
import { DishDetailPage } from '../features/dishes/DishDetailPage'
import { CartPage } from '../features/cart/CartPage'
import { OrdersPage } from '../features/orders/OrdersPage'
import { OrderDetailPage } from '../features/orders/OrderDetailPage'
import { ProfilePage } from '../features/profile/ProfilePage'
import { NotificationsPage } from '../features/notifications/NotificationsPage'
import { CookingPage } from '../features/cooking/CookingPage'
import type { CartLine, Notice } from '../types/models'
function Protected() {
  const profile = useProfile()
  if (profile.isPending) return <Loading />
  if (profile.error)
    return <ErrorMessage error={profile.error} retry={() => void profile.refetch()} />
  return profile.data ? <Outlet /> : <Navigate to="/login" replace />
}
function Layout() {
  const { data: profile } = useProfile()
  const cart = useResource<CartLine[]>('/cart', !!profile)
  const notices = useResource<Notice[]>('/notifications', !!profile)
  const logout = useAction('/auth/logout')
  const client = useQueryClient()
  const navigate = useNavigate()
  const location = useLocation()
  const previousPath = useRef(location.pathname)
  useEffect(() => {
    if (previousPath.current !== location.pathname) {
      window.scrollTo(0, 0)
      document.getElementById('main-content')?.focus({ preventScroll: true })
      previousPath.current = location.pathname
    }
  }, [location.pathname])
  const unread = notices.data?.filter((n) => !n.isRead).length ?? 0
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <div className="demo-bar">
        A little inspiration for your next meal.{' '}
        <span>Demo experience · no real orders or payments</span>
      </div>
      <header className="site-header">
        <Link to="/discover" className="brand" aria-label="DishDash home">
          <span>
            <ChefHat size={25} />
          </span>
          DishDash<span className="brand-dot">.</span>
        </Link>
        <nav className="main-nav" aria-label="Main navigation">
          <NavLink to="/discover">Discover</NavLink>
          <NavLink to="/recommendations">For you</NavLink>
          <NavLink to="/favorites">Favourites</NavLink>
          <NavLink to="/orders">Your orders</NavLink>
        </nav>
        <div className="header-actions">
          {profile ? (
            <>
              <Link
                className="icon-button"
                to="/notifications"
                aria-label={`Notifications${unread ? `, ${unread} unread` : ''}`}
              >
                <Bell size={20} />
                {unread > 0 && <span className="notification-dot" />}
              </Link>
              <Link className="icon-button profile-link" to="/profile" aria-label="Your profile">
                <UserRound size={20} />
              </Link>
              <button
                className="icon-button logout"
                aria-label="Sign out"
                disabled={logout.isPending}
                onClick={async () => {
                  try {
                    await logout.mutateAsync(undefined)
                    client.clear()
                    navigate('/discover')
                  } catch {
                    /* Error displayed below. */
                  }
                }}
              >
                <LogOut size={18} />
              </button>
            </>
          ) : (
            <Link className="sign-in" to="/login">
              Sign in
            </Link>
          )}
          <Link className="basket-link" to="/cart">
            <ShoppingBag size={19} />
            <span>Basket</span>
            <span className="basket-count">{cart.data?.length ?? 0}</span>
          </Link>
        </div>
      </header>
      <main id="main-content" tabIndex={-1}>
        <ErrorMessage error={logout.error} />
        <Outlet />
      </main>
      <footer>
        <Link className="brand" to="/discover">
          <ChefHat size={23} />
          DishDash.
        </Link>
        <p>A good meal. A smarter choice.</p>
        <span>
          Made for the love of food. <Heart size={14} />
        </span>
        <small>Demo estimates in CAD · Illustrative food photography</small>
      </footer>
    </>
  )
}
export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Navigate to="/discover" replace />} />
        <Route path="discover" element={<DiscoverPage />} />
        <Route path="login" element={<AuthPage />} />
        <Route path="register" element={<AuthPage register />} />
        <Route path="dishes/:id" element={<DishDetailPage />} />
        <Route path="dishes/:id/cook" element={<CookingPage />} />
        <Route element={<Protected />}>
          <Route path="recommendations" element={<DiscoverPage recommended />} />
          <Route path="favorites" element={<DiscoverPage favoritesOnly />} />
          <Route path="cart" element={<CartPage />} />
          <Route path="orders" element={<OrdersPage />} />
          <Route path="orders/:id" element={<OrderDetailPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="notifications" element={<NotificationsPage />} />
        </Route>
        <Route
          path="*"
          element={
            <div className="feedback">
              <h1>That page isn’t on the menu.</h1>
              <Link to="/discover">Explore dishes</Link>
            </div>
          }
        />
      </Route>
    </Routes>
  )
}
