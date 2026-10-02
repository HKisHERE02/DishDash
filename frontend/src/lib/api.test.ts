import { api, ApiError } from './api'
afterEach(() => vi.unstubAllGlobals())
test('state changes acquire a CSRF token and send credentials', async () => {
  const fetcher = vi
    .fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'csrf-test' })))
    .mockResolvedValueOnce(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetcher)
  await api('/favorites/1', 'PUT')
  expect(fetcher).toHaveBeenLastCalledWith(
    '/api/favorites/1',
    expect.objectContaining({
      credentials: 'same-origin',
      headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-test' }),
    }),
  )
})
test('network failure remains an explicit failure', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
  await expect(api('/dishes')).rejects.toThrow('Unable to connect')
})
test('server errors preserve actionable messages', async () => {
  vi.stubGlobal(
    'fetch',
    vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ title: 'Please sign in' }), { status: 401 }),
      ),
  )
  await expect(api('/profile')).rejects.toEqual(new ApiError(401, 'Please sign in'))
})
