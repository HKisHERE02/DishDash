import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ComparisonPanel } from './ComparisonPanel'
import type { Comparison } from '../../types/models'
const value: Comparison = {
  servings: 4,
  cook: { id: 'g', name: 'Demo grocery', total: 21.4, ingredients: [] },
  order: {
    id: 'r',
    name: 'Demo restaurant',
    mealPrice: 33.9,
    deliveryFee: 3,
    total: 36.9,
    etaMinutes: 31,
    rating: 4.7,
  },
  cookMinutes: 55,
  effort: 'Medium',
  moneySavedCooking: 15.5,
  minutesSavedOrdering: 24,
  explanation: 'Cooking is estimated to save $15.50, while ordering may save 24 minutes.',
}
test('shows cost, time and transparent estimate assumptions', () => {
  render(<ComparisonPanel value={value} choose={() => {}} />)
  expect(screen.getByText('$21.40')).toBeInTheDocument()
  expect(screen.getByText('$36.90')).toBeInTheDocument()
  expect(screen.getByText('55 min')).toBeInTheDocument()
  expect(screen.getByText('31 min ETA')).toBeInTheDocument()
  expect(screen.getByText(value.explanation)).toBeInTheDocument()
  expect(screen.getByText(/not full packages/)).toBeInTheDocument()
})
test('keyboard users can select either fulfillment path', async () => {
  const choose = vi.fn()
  const user = userEvent.setup()
  render(<ComparisonPanel value={value} choose={choose} />)
  await user.tab()
  await user.keyboard('{Enter}')
  expect(choose).toHaveBeenLastCalledWith('Cook')
  await user.tab()
  await user.keyboard('{Enter}')
  expect(choose).toHaveBeenLastCalledWith('Restaurant')
})
