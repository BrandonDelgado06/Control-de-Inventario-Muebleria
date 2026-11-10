import { api } from '../api/cliente'

/** Descarga un archivo que genera la API (PDF, Excel) usando la sesión actual. */
export async function descargar(url: string, params: Record<string, unknown>) {
  const r = await api.get<Blob>(url, { params, responseType: 'blob' })
  const disposicion = String(r.headers['content-disposition'] ?? '')
  const nombre = /filename="?([^";]+)"?/.exec(disposicion)?.[1] ?? 'reporte'
  const enlace = document.createElement('a')
  enlace.href = URL.createObjectURL(r.data)
  enlace.download = nombre
  document.body.appendChild(enlace)
  enlace.click()
  enlace.remove()
  setTimeout(() => URL.revokeObjectURL(enlace.href), 2000)
}

export function fmtBytes(n: number): string {
  if (n < 1024) return `${n} B`
  if (n < 1024 ** 2) return `${(n / 1024).toFixed(0)} KB`
  return `${(n / 1024 ** 2).toFixed(1)} MB`
}
