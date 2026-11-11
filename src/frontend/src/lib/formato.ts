import type { EstadoArticulo, TipoInventario, TipoMovimiento } from '../api/tipos'

const numero = new Intl.NumberFormat('es-CR', { maximumFractionDigits: 2 })
const fechaHora = new Intl.DateTimeFormat('es-CR', { dateStyle: 'medium', timeStyle: 'short' })

export const fmtNumero = (n: number) => numero.format(n)
export const fmtFechaHora = (iso: string) => fechaHora.format(new Date(iso))

/** Fecha sin hora ("2026-11-25") → "25/11/2026", sin desfases por zona horaria. */
export function fmtFecha(iso: string | null | undefined): string {
  if (!iso) return '—'
  const [a, m, d] = iso.slice(0, 10).split('-')
  return `${d}/${m}/${a}`
}

export function diasHasta(iso: string): number {
  const [a, m, d] = iso.slice(0, 10).split('-').map(Number)
  const hoy = new Date()
  const objetivo = new Date(a, m - 1, d)
  hoy.setHours(0, 0, 0, 0)
  return Math.round((objetivo.getTime() - hoy.getTime()) / 86_400_000)
}

export const nombreTipoInventario: Record<TipoInventario, string> = {
  General: 'General',
  Herramienta: 'Herramientas y accesorios',
  Acabado: 'Productos de acabado',
  MateriaPrima: 'Materia prima',
  Instalacion: 'Herramientas de instalación',
}

export const nombreEstado: Record<EstadoArticulo, string> = {
  Nueva: 'Nueva',
  Buena: 'Buena',
  Desgastada: 'Desgastada',
  Danada: 'Dañada',
  DeBaja: 'De baja',
}

export const nombreTipoMovimiento: Record<TipoMovimiento, string> = {
  Entrada: 'Entrada',
  Salida: 'Salida',
  Ajuste: 'Ajuste',
  Baja: 'Baja',
  Merma: 'Merma',
}
