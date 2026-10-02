export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message)
  }
}
export async function api<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  try {
    if (method !== 'GET') {
      const csrf = await fetch('/api/auth/csrf', { credentials: 'same-origin' })
      if (!csrf.ok)
        throw new ApiError(csrf.status, 'Could not verify your session. Please refresh.')
      headers['X-CSRF-TOKEN'] = (await csrf.json()).token
    }
    const response = await fetch(`/api${path}`, {
      method,
      credentials: 'same-origin',
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
    if (!response.ok) {
      const error = await response.json().catch(() => null)
      throw new ApiError(
        response.status,
        error?.title ??
          (response.status === 401
            ? 'Please sign in to continue.'
            : response.status === 404
              ? 'This item could not be found.'
              : response.status === 429
                ? 'Too many attempts. Please wait a minute.'
                : 'We could not complete that request. Please try again.'),
      )
    }
    return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
  } catch (error) {
    if (error instanceof ApiError) throw error
    throw new ApiError(0, 'Unable to connect. Check your connection and try again.')
  }
}
export const money = (value: number) =>
  new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' }).format(value)
export const photo = (id: string, width = 700) =>
  `https://images.unsplash.com/${id}?auto=format&fit=crop&w=${width}&q=85`
export const cuisines = [
  'Indian',
  'Italian',
  'Japanese',
  'Mexican',
  'Mediterranean',
  'Thai',
  'Chinese',
  'Korean',
]
export const allergens = [
  'milk',
  'egg',
  'fish',
  'shellfish',
  'peanut',
  'tree nuts',
  'soy',
  'wheat',
  'sesame',
]
