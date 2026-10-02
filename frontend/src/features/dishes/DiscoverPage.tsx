import { useSearchParams, Link } from 'react-router-dom'
import { ArrowRight, ChefHat, Search, SlidersHorizontal, Sparkles, Truck } from 'lucide-react'
import { cuisines, photo } from '../../lib/api'
import { useProfile, useResource } from '../../lib/queries'
import { Loading, ErrorMessage, Empty } from '../../components/Feedback'
import { DishCard } from './DishCard'
import type { Dish } from '../../types/models'
export function DiscoverPage({
  favoritesOnly = false,
  recommended = false,
}: {
  favoritesOnly?: boolean
  recommended?: boolean
}) {
  const [params, setParams] = useSearchParams()
  const { data: profile } = useProfile()
  const dishes = useResource<{ dish: Dish; fromPrice: number }[]>(
    `/dishes?${params.toString()}`,
    !recommended,
  )
  const saved = useResource<number[]>('/favorites', !!profile)
  const recommendations = useResource<{ dish: Dish; reasons: string[] }[]>(
    '/recommendations',
    recommended,
  )
  const activeQuery = recommended ? recommendations : dishes
  const isPending = activeQuery.isPending || (favoritesOnly && saved.isPending)
  const error = activeQuery.error ?? (favoritesOnly ? saved.error : null)
  const update = (name: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(name, value)
    else next.delete(name)
    setParams(next, { replace: true })
  }
  const items = recommended
    ? recommendations.data?.map((x) => ({
        dish: x.dish,
        fromPrice: undefined,
        reason: x.reasons.join(' · '),
      }))
    : (favoritesOnly && (saved.isPending || saved.error) ? undefined : dishes.data)
        ?.filter((x) => !favoritesOnly || saved.data?.includes(x.dish.id))
        .map((x) => ({ ...x, reason: undefined }))
  return (
    <>
      {!favoritesOnly && !recommended && (
        <>
          <section className="hero">
            <div className="hero-copy">
              <span className="eyebrow">
                <span className="live-dot" /> A GOOD MEAL, YOUR WAY
              </span>
              <h1>
                A little hungry.
                <br />
                <em>A lot of possibilities.</em>
              </h1>
              <p>
                Find a dish you love. See what it takes to cook it
                <br className="desktop-break" /> or order it. The delicious part is up to you.
              </p>
              <a className="button" href="#dishes">
                Find your next meal <ArrowRight size={18} />
              </a>
              <div className="hero-notes">
                <span>
                  <ChefHat size={17} /> Cook with confidence
                </span>
                <span>
                  <Truck size={17} /> Compare before you order
                </span>
              </div>
            </div>
            <div className="hero-art">
              <img
                src={photo('photo-1512621776951-a57141f2eefd', 1100)}
                alt="A colourful bowl of fresh vegetables"
              />
              <div className="hero-sticker">
                <span>
                  One dish.
                  <br />
                  Two delicious ways.
                </span>
                <span className="sticker-icon">
                  <ChefHat size={24} />
                </span>
              </div>
              <div className="floating-note">
                <span>THE BETTER MEAL DECISION</span>
                <strong>More flavour. Less guesswork.</strong>
              </div>
            </div>
          </section>
          <div className="intro-strip">
            <Sparkles size={19} />
            <p>
              Good food fits your life.{' '}
              <span>Compare cost, time and effort, all in one place.</span>
            </p>
            <Link to={profile ? '/recommendations' : '/register'}>
              Find your fit <ArrowRight size={16} />
            </Link>
          </div>
        </>
      )}
      <section id="dishes" className="discover-section">
        <div className="section-heading">
          <div>
            <p className="eyebrow">
              {favoritesOnly
                ? 'YOUR PERSONAL RECIPE BOX'
                : recommended
                  ? 'PICKED FOR YOUR PREFERENCES'
                  : 'A LITTLE INSPIRATION'}
            </p>
            <h1 className="section-title">
              {favoritesOnly
                ? 'Your saved favourites'
                : recommended
                  ? 'Made for your kind of day'
                  : 'What sounds good today?'}
            </h1>
            <p>
              {recommended
                ? 'Every suggestion has a reason. Your excluded allergens are always filtered out.'
                : favoritesOnly
                  ? 'The dishes you’ll want to come back to.'
                  : 'Every craving has a cook-it and an order-it option.'}
            </p>
          </div>
          <span className="small-note">{items?.length ?? '…'} dishes to explore</span>
        </div>
        {!recommended && !favoritesOnly && (
          <>
            <div className="search-row">
              <label className="search-box">
                <Search size={20} />
                <span className="sr-only">Search dishes</span>
                <input
                  placeholder="Search dishes, ingredients or a little inspiration…"
                  value={params.get('search') ?? ''}
                  onChange={(e) => update('search', e.target.value)}
                  maxLength={100}
                />
              </label>
              <label className="sort-label">
                <SlidersHorizontal size={18} />
                <span className="sr-only">Sort dishes</span>
                <select
                  value={params.get('sort') ?? ''}
                  onChange={(e) => update('sort', e.target.value)}
                >
                  <option value="">Our picks</option>
                  <option value="time">Quickest first</option>
                  <option value="price">Lowest cook cost</option>
                </select>
              </label>
            </div>
            <div className="cuisine-tabs" aria-label="Cuisine filters">
              <button
                className={!params.get('cuisine') ? 'selected' : ''}
                onClick={() => update('cuisine', '')}
              >
                All cuisines
              </button>
              {cuisines.map((c) => (
                <button
                  key={c}
                  className={params.get('cuisine') === c ? 'selected' : ''}
                  onClick={() => update('cuisine', c)}
                >
                  {c}
                </button>
              ))}
            </div>
            <div className="filter-row">
              <label>
                Diet
                <select
                  value={params.get('diet') ?? ''}
                  onChange={(e) => update('diet', e.target.value)}
                >
                  <option value="">Any diet</option>
                  {['Vegan', 'Vegetarian', 'Pescatarian', 'Omnivore'].map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </label>
              <label>
                Ready in
                <select
                  value={params.get('maxMinutes') ?? ''}
                  onChange={(e) => update('maxMinutes', e.target.value)}
                >
                  <option value="">Any time</option>
                  <option value="30">30 minutes</option>
                  <option value="45">45 minutes</option>
                </select>
              </label>
              <label>
                Cook budget · 2 servings
                <select
                  value={params.get('budget') ?? ''}
                  onChange={(e) => update('budget', e.target.value)}
                >
                  <option value="">Any budget</option>
                  <option value="8">Up to $8</option>
                  <option value="15">Up to $15</option>
                  <option value="25">Up to $25</option>
                </select>
              </label>
              {params.size > 0 && (
                <button className="text-button" onClick={() => setParams({})}>
                  Clear filters
                </button>
              )}
            </div>
          </>
        )}
        {isPending ? (
          <Loading />
        ) : error ? (
          <ErrorMessage
            error={error}
            retry={() => {
              void activeQuery.refetch()
              if (favoritesOnly) void saved.refetch()
            }}
          />
        ) : items?.length ? (
          <div className="dish-grid">
            {items.map((x) => (
              <DishCard key={x.dish.id} {...x} saved={saved.data?.includes(x.dish.id)} />
            ))}
          </div>
        ) : (
          <Empty
            title={favoritesOnly ? 'Your recipe box is waiting' : 'No dishes found'}
            text={
              recommended
                ? 'Try updating your preferences to see more options.'
                : 'Try another search or save a dish that catches your eye.'
            }
          />
        )}
      </section>
      <section className="how-it-works">
        <div>
          <span className="eyebrow">ONE CRAVING. TWO PATHS.</span>
          <h2>
            Make dinner a decision
            <br />
            you feel good about.
          </h2>
        </div>
        <div>
          <ChefHat />
          <h3>Make it your own</h3>
          <p>Adjust servings, compare ingredient estimates and cook one step at a time.</p>
        </div>
        <div>
          <Truck />
          <h3>Let dinner come to you</h3>
          <p>Compare demo restaurants by cost, rating and estimated delivery time.</p>
        </div>
      </section>
    </>
  )
}
