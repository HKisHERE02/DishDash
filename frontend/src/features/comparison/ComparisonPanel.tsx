import { ChefHat, Truck, ArrowRight, Clock3 } from 'lucide-react'
import { money } from '../../lib/api'
import type { Comparison } from '../../types/models'
export function ComparisonPanel({
  value,
  choose,
}: {
  value: Comparison
  choose: (kind: 'Cook' | 'Restaurant') => void
}) {
  return (
    <section className="comparison" aria-label="Cook versus order comparison">
      <div className="compare-grid">
        <article className="compare-card cook">
          <div className="compare-title">
            <ChefHat />
            <h2>Cook it</h2>
            <span>THE HANDS-ON WAY</span>
          </div>
          <p className="price">
            {money(value.cook.total)}
            <small> for {value.servings} servings</small>
          </p>
          <p>Estimated ingredients · {value.cook.name}</p>
          <div className="compare-facts">
            <span>
              <Clock3 size={17} />
              {value.cookMinutes} min
            </span>
            <span>{value.effort} effort</span>
          </div>
          <button className="button" onClick={() => choose('Cook')}>
            Explore grocery options <ArrowRight size={17} />
          </button>
        </article>
        <article className="compare-card order">
          <div className="compare-title">
            <Truck />
            <h2>Order it</h2>
            <span>THE TAKE-IT-EASY WAY</span>
          </div>
          <p className="price">
            {money(value.order.total)}
            <small> for {value.servings} servings</small>
          </p>
          <p>Includes {money(value.order.deliveryFee)} delivery · Demo</p>
          <div className="compare-facts">
            <span>
              <Clock3 size={17} />
              {value.order.etaMinutes} min ETA
            </span>
            <span>★ {value.order.rating.toFixed(1)} rating</span>
          </div>
          <button className="button warm" onClick={() => choose('Restaurant')}>
            Explore restaurants <ArrowRight size={17} />
          </button>
        </article>
      </div>
      <div className="comparison-insight">
        <span>THE TRADE-OFF</span>
        <p>{value.explanation}</p>
      </div>
      <small className="estimate-note">
        CAD estimates. Groceries are priced by the quantity used, not full packages. Restaurant
        prices include demo delivery; taxes and tips are excluded. Times are estimates.
      </small>
    </section>
  )
}
