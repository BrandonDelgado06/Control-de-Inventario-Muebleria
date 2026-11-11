// Tipos que devuelve la API (mismos nombres que los DTO de .NET, en camelCase).

export type TipoInventario = 'General' | 'Herramienta' | 'Acabado' | 'MateriaPrima' | 'Instalacion'
export type TipoDato = 'Texto' | 'Numero' | 'Fecha' | 'Lista'
export type EstadoArticulo = 'Nueva' | 'Buena' | 'Desgastada' | 'Danada' | 'DeBaja'
export type TipoMovimiento = 'Entrada' | 'Salida' | 'Ajuste' | 'Baja' | 'Merma'

export interface Paginado<T> {
  items: T[]
  total: number
  pagina: number
  tamano: number
}

export interface Sesion {
  id: number
  nombre: string
  usuario: string
  rol: string
  permisos: string[]
}

export interface Usuario {
  id: number
  nombre: string
  usuario: string
  rolId: number
  rol: string
  activo: boolean
}

export interface Rol {
  id: number
  nombre: string
  permisos: string[]
}

export interface Ubicacion {
  id: number
  nombre: string
  descripcion: string | null
  activo: boolean
  cantidadArticulos: number
}

export interface Campo {
  id: number
  nombre: string
  tipoDato: TipoDato
  obligatorio: boolean
  opciones: string[]
  orden: number
}

export interface Categoria {
  id: number
  nombre: string
  tipoInventario: TipoInventario
  cantidadArticulos: number
  campos: Campo[]
}

export interface ArticuloResumen {
  id: number
  codigo: string
  nombre: string
  categoriaId: number
  categoria: string
  tipoInventario: TipoInventario
  ubicacionId: number
  ubicacion: string
  unidadMedida: string
  stockActual: number
  stockMinimo: number
  estado: EstadoArticulo
  lote: string | null
  fechaVencimiento: string | null
  bajoMinimo: boolean
  activo: boolean
}

export interface ValorCampo {
  campoId: number
  campo: string
  tipoDato: TipoDato
  obligatorio: boolean
  opciones: string[]
  valor: string | null
}

export interface ArticuloDetalle {
  articulo: ArticuloResumen
  presentacion: string | null
  alias: string[]
  campos: ValorCampo[]
}

export interface Movimiento {
  id: number
  fechaHora: string
  tipo: TipoMovimiento
  articuloId: number
  codigo: string
  articulo: string
  unidadMedida: string
  cantidad: number
  stockAnterior: number
  stockNuevo: number
  motivo: string
  usuario: string
}

export interface RegistroBitacora {
  id: number
  fechaHora: string
  usuario: string
  modulo: string
  accion: string
}

export interface Resumen {
  totalArticulos: number
  bajoMinimo: number
  porVencer: number
  movimientosHoy: number
  articulosBajoMinimo: ArticuloResumen[]
  ultimosMovimientos: Movimiento[]
}

export const Permisos = {
  ArticulosVer: 'Articulos.Ver',
  ArticulosEditar: 'Articulos.Editar',
  MovimientosRegistrar: 'Movimientos.Registrar',
  MovimientosAjustar: 'Movimientos.Ajustar',
  CatalogosGestionar: 'Catalogos.Gestionar',
  UsuariosGestionar: 'Usuarios.Gestionar',
  BitacoraVer: 'Bitacora.Ver',
} as const
