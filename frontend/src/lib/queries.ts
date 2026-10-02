import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from './api'
import type { Profile } from '../types/models'
export function useProfile() {
  return useQuery({
    queryKey: ['profile'],
    queryFn: async () => {
      try {
        return await api<Profile>('/profile')
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    retry: false,
    staleTime: 60_000,
  })
}
export function useResource<T>(path: string, enabled = true) {
  return useQuery({ queryKey: [path], queryFn: () => api<T>(path), enabled })
}
export function useAction<T = void, V = unknown>(
  path: string | ((value: V) => string),
  method = 'POST',
) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (value: V) =>
      api<T>(
        typeof path === 'function' ? path(value) : path,
        method,
        method === 'DELETE' ? undefined : value,
      ),
    onSuccess: async () => {
      await client.invalidateQueries()
    },
  })
}
