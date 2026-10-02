import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AuthPage } from './AuthPage'

function renderForm(register = true) {
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { mutations: { retry: false } } })}
    >
      <MemoryRouter initialEntries={['/auth']}>
        <Routes>
          <Route path="/auth" element={<AuthPage register={register} />} />
          <Route path="/discover" element={<h1>Discover meals</h1>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
function mockHttp(status: number, body: object) {
  const fetcher = vi
    .fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'csrf-test' })))
    .mockResolvedValueOnce(new Response(JSON.stringify(body), { status }))
  vi.stubGlobal('fetch', fetcher)
  return fetcher
}
afterEach(() => vi.unstubAllGlobals())
test('registration has labeled constrained fields and rejects incomplete submission', async () => {
  const fetcher = mockHttp(200, {})
  renderForm()
  await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
  expect(fetcher).not.toHaveBeenCalled()
  expect(screen.getByLabelText('Email address')).toHaveAttribute('type', 'email')
  expect(screen.getByLabelText('Password')).toHaveAttribute('minlength', '12')
  expect(screen.getByLabelText('Your name')).toBeRequired()
})
test('login refusal displays the API error and stays on the form', async () => {
  mockHttp(401, { title: 'Unable to sign in with those credentials.' })
  renderForm(false)
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Email address'), 'test@example.test')
  await user.type(screen.getByLabelText('Password'), 'Wrong-Password1!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to sign in')
  expect(screen.getByLabelText('Email address')).toBeInTheDocument()
})
test('successful registration sends the form and opens discovery', async () => {
  const fetcher = mockHttp(200, { message: 'Account created.' })
  renderForm()
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Your name'), 'Alex')
  await user.type(screen.getByLabelText('Email address'), 'alex@example.test')
  await user.type(screen.getByLabelText('Password'), 'Test-Meal-2026!')
  await user.click(screen.getByRole('button', { name: 'Create account' }))
  expect(await screen.findByRole('heading', { name: 'Discover meals' })).toBeInTheDocument()
  expect(fetcher).toHaveBeenLastCalledWith(
    '/api/auth/register',
    expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({
        email: 'alex@example.test',
        password: 'Test-Meal-2026!',
        displayName: 'Alex',
      }),
    }),
  )
})
