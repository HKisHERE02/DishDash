import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { DiscoverPage } from './DiscoverPage'
import type { Dish, Profile } from '../../types/models'

const dish: Dish = {
  id: 1,
  name: 'Butter Chicken',
  cuisine: 'Indian',
  description: 'A warming curry.',
  image: 'photo-test',
  diet: 'Omnivore',
  allergens: ['milk'],
  baseServings: 2,
  prepMinutes: 15,
  cookMinutes: 30,
  effort: 'Medium',
  ingredients: [],
  steps: [],
}
const profile: Profile = {
  displayName: 'Alex',
  diet: 'Any',
  allergens: [],
  cuisines: [],
  defaultServings: 2,
  budget: 30,
  maxMinutes: 60,
}

function renderPage(favoritesOnly = true) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  })
  client.setQueryData(['profile'], profile)
  client.setQueryData(['/dishes?'], [{ dish, fromPrice: 12 }])
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <DiscoverPage favoritesOnly={favoritesOnly} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return client
}

afterEach(() => vi.unstubAllGlobals())

test.each([{ ids: [1] }, { ids: [] }])(
  'waits for favourites before rendering saved dishes or an empty state ($ids)',
  async ({ ids }) => {
    let resolve!: (response: Response) => void
    vi.stubGlobal(
      'fetch',
      vi.fn(
        () =>
          new Promise<Response>((done) => {
            resolve = done
          }),
      ),
    )
    renderPage()
    expect(screen.getByRole('status')).toHaveTextContent('Finding something good')
    expect(screen.queryByText('Your recipe box is waiting')).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: dish.name })).not.toBeInTheDocument()
    expect(screen.queryByText('0 dishes to explore')).not.toBeInTheDocument()
    await act(async () => resolve(new Response(JSON.stringify(ids))))
    if (ids.length)
      expect(await screen.findByRole('heading', { name: dish.name })).toBeInTheDocument()
    else expect(await screen.findByText('Your recipe box is waiting')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  },
)

test('shows a favourites failure and retries it without presenting an empty recipe box', async () => {
  const favorites = vi
    .fn()
    .mockResolvedValueOnce(
      new Response(JSON.stringify({ title: 'Favourites unavailable.' }), { status: 503 }),
    )
    .mockResolvedValue(new Response(JSON.stringify([1])))
  const fetcher = vi.fn((path: string) =>
    path === '/api/favorites'
      ? favorites()
      : Promise.resolve(new Response(JSON.stringify([{ dish, fromPrice: 12 }]))),
  )
  vi.stubGlobal('fetch', fetcher)
  renderPage()
  expect(await screen.findByRole('alert')).toHaveTextContent('Favourites unavailable.')
  expect(screen.queryByText('Your recipe box is waiting')).not.toBeInTheDocument()
  await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
  expect(await screen.findByRole('heading', { name: dish.name })).toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  expect(fetcher.mock.calls.filter(([path]) => path === '/api/favorites')).toHaveLength(2)
})

test('normal discovery remains visible while favourites load', () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => new Promise<Response>(() => {})),
  )
  renderPage(false)
  expect(screen.getByRole('heading', { name: dish.name })).toBeInTheDocument()
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
})

test('normal discovery remains visible if favourites fail', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 503 })))
  const client = renderPage(false)
  await waitFor(() => expect(client.getQueryState(['/favorites'])?.status).toBe('error'))
  expect(screen.getByRole('heading', { name: dish.name })).toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
})
