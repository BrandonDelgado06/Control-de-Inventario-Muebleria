import axios, { AxiosError } from 'axios'

/** Cliente HTTP. La sesión viaja en una cookie HttpOnly, por eso no hay tokens aquí. */
export const api = axios.create({ baseURL: '/api', withCredentials: true })

export const EVENTO_SESION_EXPIRADA = 'sesion-expirada'

api.interceptors.response.use(
  (r) => r,
  (e: AxiosError) => {
    // 401 fuera del login = la sesión expiró por inactividad (RF-01).
    if (e.response?.status === 401 && !e.config?.url?.startsWith('/auth/')) {
      window.dispatchEvent(new Event(EVENTO_SESION_EXPIRADA))
    }
    return Promise.reject(e)
  },
)

interface ProblemDetails {
  title?: string
  errors?: Record<string, string[]>
}

/** Mensaje legible a partir de un error de la API (ProblemDetails de ASP.NET Core). */
export function mensajeError(e: unknown): string {
  if (e instanceof AxiosError) {
    if (!e.response) return 'No hay conexión con el servidor del taller.'
    if (e.response.status === 403) return (e.response.data as ProblemDetails)?.title ?? 'No tienes permiso para esta acción.'
    const p = e.response.data as ProblemDetails | undefined
    if (p?.errors) return Object.values(p.errors).flat().join(' ')
    if (p?.title) return p.title
  }
  return 'Ocurrió un error inesperado.'
}
