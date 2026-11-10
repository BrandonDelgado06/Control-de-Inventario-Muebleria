import { describe, expect, it } from 'vitest'
import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { mensajeError } from './cliente'

function errorConRespuesta(status: number, data: unknown) {
  const respuesta = { status, data, statusText: '', headers: {}, config: { headers: new AxiosHeaders() } } as AxiosResponse
  return new AxiosError('error', String(status), undefined, undefined, respuesta)
}

describe('mensajes de error que ve el usuario', () => {
  it('usa el título que manda la API (reglas de negocio)', () => {
    expect(mensajeError(errorConRespuesta(400, { title: 'No hay suficiente existencia de "Lija".' }))).toBe('No hay suficiente existencia de "Lija".')
  })

  it('junta los errores de validación de cada campo', () => {
    const e = errorConRespuesta(400, { title: 'Revisa los datos del formulario.', errors: { Nombre: ['El nombre es obligatorio.'], RolId: ['Elige un rol.'] } })
    expect(mensajeError(e)).toBe('El nombre es obligatorio. Elige un rol.')
  })

  it('explica el 403 aunque la API no mande título (RNF-02)', () => {
    expect(mensajeError(errorConRespuesta(403, ''))).toBe('No tienes permiso para esta acción.')
  })

  it('avisa cuando no hay conexión con el servidor del taller', () => {
    expect(mensajeError(new AxiosError('Network Error', 'ERR_NETWORK'))).toBe('No hay conexión con el servidor del taller.')
  })

  it('tiene un mensaje genérico para errores desconocidos', () => {
    expect(mensajeError(new Error('x'))).toBe('Ocurrió un error inesperado.')
  })
})
