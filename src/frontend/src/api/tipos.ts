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
  herramientasFuera: number
  herramientasAtrasadas: number
  solicitudesPendientes: number
  movimientosHoy: number
  alertasActivas: number
  articulosBajoMinimo: ArticuloResumen[]
  ultimosMovimientos: Movimiento[]
  consumoPorCategoria: { categoria: string; salidas: number }[]
}

// ---------------------------------------------------------------- Solicitudes (RF-23 a RF-25)
export type EstadoSolicitud = 'Pendiente' | 'Aprobada' | 'Rechazada' | 'Entregada'

export interface Solicitud {
  id: number
  articuloId: number
  codigo: string
  articulo: string
  unidadMedida: string
  stockActual: number
  cantidad: number
  motivo: string
  estado: EstadoSolicitud
  solicitanteId: number
  solicitante: string
  aprobador: string | null
  motivoRechazo: string | null
  fechaSolicitud: string
  fechaResolucion: string | null
  fechaEntrega: string | null
}

// ---------------------------------------------------------------- Herramientas de instalación (RF-16 a RF-18)
export interface SalidaHerramienta {
  id: number
  articuloId: number
  codigo: string
  herramienta: string
  responsableId: number
  responsable: string
  destino: string
  fechaSalida: string
  fechaPrevista: string
  fechaDevolucion: string | null
  estadoDevolucion: EstadoArticulo | null
  diasRetraso: number
}

// ---------------------------------------------------------------- Alertas (RF-30 a RF-32)
export interface AlertaVencimiento {
  articuloId: number
  codigo: string
  producto: string
  lote: string | null
  fechaVencimiento: string
  diasRestantes: number
  stockActual: number
  unidadMedida: string
}

export interface Alertas {
  stockMinimo: ArticuloResumen[]
  vencimientos: AlertaVencimiento[]
  herramientasSinDevolver: SalidaHerramienta[]
  respaldo: { ultimo: string | null; diasDesdeUltimo: number | null; mensaje: string } | null
  diasAnticipacion: number
  total: number
}

export interface ResumenAlertas {
  stockMinimo: number
  vencimientos: number
  herramientasSinDevolver: number
  respaldo: number
  total: number
}

// ---------------------------------------------------------------- Reportes (RF-33 a RF-35)
export type TipoColumna = 'Texto' | 'Numero' | 'Fecha' | 'FechaHora'

export interface TipoReporte {
  clave: string
  titulo: string
  descripcion: string
  requerimiento: string
  filtros: string[]
}

export interface Reporte {
  clave: string
  titulo: string
  descripcion: string
  filtros: string[]
  columnas: { titulo: string; tipo: TipoColumna }[]
  filas: (string | number | null)[][]
  generado: string
  generadoPor: string
}

// ---------------------------------------------------------------- Respaldos (RF-38, RF-39)
export interface RespaldoInfo {
  id: number
  archivo: string
  fecha: string
  tamanoBytes: number
  descripcion: string | null
}

export const Permisos = {
  ArticulosVer: 'Articulos.Ver',
  ArticulosEditar: 'Articulos.Editar',
  MovimientosRegistrar: 'Movimientos.Registrar',
  MovimientosAjustar: 'Movimientos.Ajustar',
  SolicitudesCrear: 'Solicitudes.Crear',
  SolicitudesAprobar: 'Solicitudes.Aprobar',
  HerramientasPrestar: 'Herramientas.Prestar',
  AlertasVer: 'Alertas.Ver',
  CatalogosGestionar: 'Catalogos.Gestionar',
  UsuariosGestionar: 'Usuarios.Gestionar',
  ReportesVer: 'Reportes.Ver',
  BitacoraVer: 'Bitacora.Ver',
  RespaldosGestionar: 'Respaldos.Gestionar',
} as const
