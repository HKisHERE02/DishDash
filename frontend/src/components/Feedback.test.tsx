import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Loading, ErrorMessage } from './Feedback'
test('loading status is exposed to assistive technology', () => {
  render(<Loading />)
  expect(screen.getByRole('status')).toHaveTextContent('Finding something good')
})
test('errors are announced and retry is operable', async () => {
  const retry = vi.fn()
  render(<ErrorMessage error={new Error('Network unavailable')} retry={retry} />)
  expect(screen.getByRole('alert')).toHaveTextContent('Network unavailable')
  await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
  expect(retry).toHaveBeenCalledOnce()
})
