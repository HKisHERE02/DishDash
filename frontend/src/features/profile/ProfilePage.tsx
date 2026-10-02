import { useState } from 'react'
import { useProfile, useAction } from '../../lib/queries'
import { allergens, cuisines } from '../../lib/api'
import { ErrorMessage, Loading } from '../../components/Feedback'
import type { Profile } from '../../types/models'
export function ProfilePage() {
  const profile = useProfile()
  if (profile.isPending) return <Loading />
  if (!profile.data) return <ErrorMessage error={profile.error} />
  return <ProfileForm profile={profile.data} />
}
function ProfileForm({ profile }: { profile: Profile }) {
  const [form, setForm] = useState(profile)
  const save = useAction('/profile', 'PUT')
  const toggle = (field: 'allergens' | 'cuisines', value: string) =>
    setForm((f) => ({
      ...f,
      [field]: f[field].includes(value)
        ? f[field].filter((x) => x !== value)
        : [...f[field], value],
    }))
  return (
    <div className="page narrow">
      <p className="eyebrow">GOOD FOOD STARTS WITH YOU</p>
      <h1>Your preferences</h1>
      <p className="lede">Help us put the right dishes on your table.</p>
      <form
        className="panel profile-form"
        onSubmit={(e) => {
          e.preventDefault()
          save.mutate(form)
        }}
      >
        <label>
          Your name
          <input
            required
            maxLength={60}
            value={form.displayName}
            onChange={(e) => setForm({ ...form, displayName: e.target.value })}
          />
        </label>
        <div className="form-columns">
          <label>
            Dietary preference
            <select value={form.diet} onChange={(e) => setForm({ ...form, diet: e.target.value })}>
              {['Any', 'Omnivore', 'Vegetarian', 'Vegan', 'Pescatarian'].map((d) => (
                <option key={d}>{d}</option>
              ))}
            </select>
          </label>
          <label>
            Default servings
            <input
              type="number"
              required
              min={1}
              max={12}
              value={form.defaultServings}
              onChange={(e) => setForm({ ...form, defaultServings: Number(e.target.value) })}
            />
          </label>
        </div>
        <fieldset>
          <legend>Allergen exclusions</legend>
          <p>
            Excluded from recommendations and blocked when adding to your basket. Demo data does not
            certify allergen safety or cross-contact.
          </p>
          <div className="check-grid">
            {allergens.map((a) => (
              <label key={a}>
                <input
                  type="checkbox"
                  checked={form.allergens.includes(a)}
                  onChange={() => toggle('allergens', a)}
                />
                {a}
              </label>
            ))}
          </div>
        </fieldset>
        <fieldset>
          <legend>Favourite cuisines</legend>
          <div className="check-grid">
            {cuisines.map((c) => (
              <label key={c}>
                <input
                  type="checkbox"
                  checked={form.cuisines.includes(c)}
                  onChange={() => toggle('cuisines', c)}
                />
                {c}
              </label>
            ))}
          </div>
        </fieldset>
        <div className="form-columns">
          <label>
            Grocery budget (CAD)
            <input
              type="number"
              min={1}
              max={500}
              required
              value={form.budget}
              onChange={(e) => setForm({ ...form, budget: Number(e.target.value) })}
            />
          </label>
          <label>
            Preferred cooking time (minutes)
            <input
              type="number"
              min={5}
              max={240}
              required
              value={form.maxMinutes}
              onChange={(e) => setForm({ ...form, maxMinutes: Number(e.target.value) })}
            />
          </label>
        </div>
        <ErrorMessage error={save.error} />
        {save.isSuccess && (
          <p className="success" role="status">
            Preferences saved. Your recommendations have been updated.
          </p>
        )}
        <button className="button" disabled={save.isPending}>
          Save preferences
        </button>
      </form>
    </div>
  )
}
