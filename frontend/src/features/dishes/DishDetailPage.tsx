import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, ChefHat, Clock3, Minus, Plus, Star } from 'lucide-react'
import { useAction, useProfile, useResource } from '../../lib/queries'
import { money, photo } from '../../lib/api'
import { Loading, ErrorMessage } from '../../components/Feedback'
import { ComparisonPanel } from '../comparison/ComparisonPanel'
import type { Comparison, Dish, GroceryOption, RestaurantOption } from '../../types/models'
export function DishDetailPage() {
  const { id } = useParams()
  const dish = useResource<Dish>(`/dishes/${id}`)
  const { data: profile } = useProfile()
  if (dish.isPending) return <Loading />
  if (!dish.data) return <ErrorMessage error={dish.error} retry={() => void dish.refetch()} />
  return (
    <DishDetail
      key={`${id}-${profile ? `member-${profile.defaultServings}` : 'guest'}`}
      dish={dish.data}
      defaultServings={profile?.defaultServings ?? 2}
    />
  )
}
function DishDetail({ dish, defaultServings }: { dish: Dish; defaultServings: number }) {
  const [servings, setServings] = useState(defaultServings)
  const [kind, setKind] = useState<'Cook' | 'Restaurant' | null>(null)
  const { data: profile } = useProfile()
  const navigate = useNavigate()
  const comparison = useResource<Comparison>(`/dishes/${dish.id}/comparison?servings=${servings}`)
  const groceries = useResource<GroceryOption[]>(
    `/dishes/${dish.id}/grocery-options?servings=${servings}`,
    kind === 'Cook',
  )
  const restaurants = useResource<RestaurantOption[]>(
    `/dishes/${dish.id}/restaurant-options?servings=${servings}`,
    kind === 'Restaurant',
  )
  const add = useAction('/cart')
  const choose = (value: 'Cook' | 'Restaurant') => {
    setKind(value)
    setTimeout(
      () =>
        document
          .getElementById('provider-options')
          ?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
      50,
    )
  }
  const putInCart = async (providerId: string) => {
    if (!profile) {
      navigate('/login')
      return
    }
    try {
      await add.mutateAsync({ dishId: dish.id, servings, kind, providerId })
      navigate('/cart')
    } catch {
      /* Mutation feedback stays visible. */
    }
  }
  return (
    <div className="page">
      <Link className="back-link" to="/discover">
        <ArrowLeft size={16} /> Back to all dishes
      </Link>
      <section className="detail-hero">
        <div>
          <p className="eyebrow">
            {dish.cuisine} · {dish.diet}
          </p>
          <h1>{dish.name}</h1>
          <p className="lede">{dish.description}</p>
          <div className="detail-facts">
            <span>
              <Clock3 size={18} />
              {dish.prepMinutes} min prep
            </span>
            <span>{dish.cookMinutes} min cooking</span>
            <span>
              <ChefHat size={18} />
              {dish.effort}
            </span>
          </div>
          <div className="serving-control">
            <span id="serving-label">How many are eating?</span>
            <div>
              <button
                aria-label="Fewer servings"
                disabled={servings === 1}
                onClick={() => setServings((s) => s - 1)}
              >
                <Minus size={17} />
              </button>
              <output aria-live="polite" aria-labelledby="serving-label">
                {servings} servings
              </output>
              <button
                aria-label="More servings"
                disabled={servings === 12}
                onClick={() => setServings((s) => s + 1)}
              >
                <Plus size={17} />
              </button>
            </div>
          </div>
          {dish.allergens.length > 0 && (
            <p className="allergen-note">
              Contains: {dish.allergens.join(', ')}. Demo recipe labels do not cover cross-contact.
            </p>
          )}
        </div>
        <img className="detail-image" src={photo(dish.image, 1000)} alt={dish.name} />
      </section>
      <div className="section-heading">
        <div>
          <p className="eyebrow">SAME CRAVING. YOUR CALL.</p>
          <h2>How would you like it?</h2>
        </div>
        <span className="pill">Comparing {servings} servings</span>
      </div>
      {comparison.isPending ? (
        <Loading />
      ) : comparison.data ? (
        <ComparisonPanel value={comparison.data} choose={choose} />
      ) : (
        <ErrorMessage error={comparison.error} retry={() => void comparison.refetch()} />
      )}
      {kind && (
        <section className="panel provider-section" id="provider-options">
          <div className="section-heading">
            <div>
              <p className="eyebrow">CLEAR PRICES. NO SURPRISES.</p>
              <h2>{kind === 'Cook' ? 'Choose your grocery basket' : 'Choose your restaurant'}</h2>
            </div>
            <span className="pill">Demo providers</span>
          </div>
          <ErrorMessage error={add.error} />
          {(kind === 'Cook' ? groceries.isPending : restaurants.isPending) ? (
            <Loading />
          ) : (
            <>
              <ErrorMessage error={kind === 'Cook' ? groceries.error : restaurants.error} />
              <div className="provider-grid">
                {kind === 'Cook'
                  ? groceries.data?.map((p) => (
                      <article className="provider-card" key={p.id}>
                        <ChefHat />
                        <h3>{p.name}</h3>
                        <p>
                          {dish.ingredients.length} ingredients · quantities for {servings}
                        </p>
                        <strong className="provider-price">{money(p.total)}</strong>
                        <button
                          className="button"
                          disabled={add.isPending}
                          onClick={() => void putInCart(p.id)}
                        >
                          Add grocery basket
                        </button>
                      </article>
                    ))
                  : restaurants.data?.map((p) => (
                      <article className="provider-card" key={p.id}>
                        <Star />
                        <h3>{p.name}</h3>
                        <p>
                          ★ {p.rating} · {p.etaMinutes} min ETA
                        </p>
                        <p>
                          Meals {money(p.mealPrice)} + delivery {money(p.deliveryFee)}
                        </p>
                        <strong className="provider-price">{money(p.total)}</strong>
                        <button
                          className="button warm"
                          disabled={add.isPending}
                          onClick={() => void putInCart(p.id)}
                        >
                          Add restaurant meal
                        </button>
                      </article>
                    ))}
              </div>
            </>
          )}
        </section>
      )}
      <div className="recipe-grid">
        <section className="panel">
          <h2>Your ingredients</h2>
          <p>For {servings} servings · lowest grocery estimate</p>
          <ul className="ingredients">
            {comparison.data?.cook.ingredients.map((i) => (
              <li key={i.name}>
                <span>{i.name}</span>
                <strong>
                  {i.quantity} {i.unit}
                </strong>
                <span>{money(i.cost)}</span>
              </li>
            ))}
          </ul>
        </section>
        <section className="panel">
          <div className="section-heading">
            <h2>Let’s make it</h2>
            <Link className="text-link" to={`/dishes/${dish.id}/cook`}>
              Guided cooking <ArrowLeft className="flip" size={16} />
            </Link>
          </div>
          <ol className="recipe-steps">
            {[...dish.steps]
              .sort((a, b) => a.position - b.position)
              .map((s) => (
                <li key={s.id}>{s.instruction}</li>
              ))}
          </ol>
        </section>
      </div>
    </div>
  )
}
