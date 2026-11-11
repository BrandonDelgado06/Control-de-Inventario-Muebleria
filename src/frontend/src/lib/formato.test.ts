import { describe, expect, it, vi, afterEach } from 'vitest'
import { diasHasta, fmtFecha, fmtNumero, nombreEstado, nombreTipoInventario } from './formato'
import { fmtBytes } from './descargar'

describe('formato de números y fechas', () => {
  it('muestra los números con formato de Costa Rica y hasta 2 decimales', () => {
    expect(fmtNumero(1234.5)).toMatch(/1.?234,5/)
    expect(fmtNumero(3)).toBe('3')
    expect(fmtNumero(2.456)).toBe('2,46')
  })

  it('convierte una fecha ISO a dd/mm/aaaa sin desfase de zona horaria', () => {
    expect(fmtFecha('2026-12-04')).toBe('04/12/2026')
    expect(fmtFecha('2026-01-01T00:00:00')).toBe('01/01/2026')
  })

  it('muestra una raya cuando no hay fecha', () => {
    expect(fmtFecha(null)).toBe('—')
    expect(fmtFecha(undefined)).toBe('—')
  })
})

describe('días hasta una fecha (alertas de vencimiento, RF-31)', () => {
  afterEach(() => vi.useRealTimers())

  it('cuenta días hacia adelante y hacia atrás desde hoy', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 10, 9, 15, 30))   // 9 de noviembre a media tarde
    expect(diasHasta('2026-11-09')).toBe(0)
    expect(diasHasta('2026-11-25')).toBe(16)
    expect(diasHasta('2026-11-07')).toBe(-2)
  })
})

describe('textos para mostrar', () => {
  it('muestra los estados con los nombres del diccionario de datos', () => {
    expect(nombreEstado.Danada).toBe('Dañada')
    expect(nombreEstado.DeBaja).toBe('De baja')
  })

  it('tiene un nombre para cada tipo de inventario', () => {
    expect(Object.keys(nombreTipoInventario)).toEqual(['General', 'Herramienta', 'Acabado', 'MateriaPrima', 'Instalacion'])
  })

  it('muestra el tamaño de los respaldos en B, KB o MB', () => {
    expect(fmtBytes(512)).toBe('512 B')
    expect(fmtBytes(2048)).toBe('2 KB')
    expect(fmtBytes(8_151_040)).toBe('7.8 MB')
  })
})
