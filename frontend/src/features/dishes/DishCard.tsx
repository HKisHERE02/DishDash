import { Link, useNavigate } from 'react-router-dom'
import { ArrowUpRight, Clock3, Heart } from 'lucide-react'
import { money, photo } from '../../lib/api'
import { useAction, useProfile } from '../../lib/queries'
import { ErrorMessage } from '../../components/Feedback'
import type { Dish } from '../../types/models'
export function DishCard({
  dish,
  fromPrice,
  saved = false,
  reason,
}: {
  dish: Dish
  fromPrice?: number
  saved?: boolean
  reason?: string
}) {
  const { data: profile } = useProfile()
  const navigate = useNavigate()
  const favorite = useAction(`/favorites/${dish.id}`, saved ? 'DELETE' : 'PUT')
  return (
    <article className="dish-card">
      <div className="dish-image">
        <Link to={`/dishes/${dish.id}`} tabIndex={-1} aria-hidden="true">
          <img
            src={photo(dish.image)}
            alt=""
            loading="lazy"
            onError={(e) => {
              e.currentTarget.style.visibility = 'hidden'
            }}
          />
        </Link>
        <span className="diet-tag">{dish.diet}</span>
        <button
          className={`save-button ${saved ? 'saved' : ''}`}
          aria-label={`${saved ? 'Unsave' : 'Save'} ${dish.name}`}
          aria-pressed={saved}
          disabled={favorite.isPending}
          onClick={() => (profile ? favorite.mutate(undefined) : navigate('/login'))}
        >
          <Heart size={19} fill={saved ? 'currentColor' : 'none'} />
        </button>
      </div>
      <div className="dish-body">
        <div className="dish-meta">
          <span>{dish.cuisine}</span>
          <span>
            <Clock3 size={14} /> {dish.prepMinutes + dish.cookMinutes} min
          </span>
        </div>
        <h3>
          <Link to={`/dishes/${dish.id}`}>{dish.name}</Link>
        </h3>
        <p>{reason ?? dish.description}</p>
        <div className="dish-bottom">
          <span>
            {fromPrice !== undefined ? (
              <>
                Cook from <strong>{money(fromPrice / 2)}</strong>
                <small> / serving</small>
              </>
            ) : (
              dish.effort + ' effort'
            )}
          </span>
          <Link
            to={`/dishes/${dish.id}`}
            className="card-arrow"
            aria-label={`Compare ${dish.name}`}
          >
            <ArrowUpRight size={20} />
          </Link>
        </div>
        <ErrorMessage error={favorite.error} />
      </div>
    </article>
  )
}
