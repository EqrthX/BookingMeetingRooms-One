export const API_BASE = (import.meta.env.VITE_API_URL || 'https://localhost:7271/api').replace(/\/$/, '')
export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) { super(message); this.status = status }
}
export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  const token = localStorage.getItem('token')
  if (token) headers.set('Authorization', 'Bearer ' + token)
  if (options.body) headers.set('Content-Type', 'application/json')
  const response = await fetch(API_BASE + path, { ...options, headers })
  if (!response.ok) {
    const message = await response.text()
    throw new ApiError(response.status, message || 'Request failed (' + response.status + ')')
  }
  if (response.status === 204) return undefined as T
  return await response.json() as T
}
