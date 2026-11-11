import { useMemo, useState } from 'react'
import { Link as RouterLink, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Breadcrumbs, Button, Chip, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, InputAdornment,
  LinearProgress, Link, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import SaveOutlined from '@mui/icons-material/SaveOutlined'
import DoneAllOutlined from '@mui/icons-material/DoneAllOutlined'
import SearchIcon from '@mui/icons-material/Search'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type Conteo, type ConteoResumen } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, Pagina, Vacio } from '../../components/Pagina'
import { fmtFecha, fmtNumero } from '../../lib/formato'
import { useCategorias, useUbicaciones } from '../catalogo/ArticulosPage'

const alcance = (c: ConteoResumen) => [c.ubicacion, c.categoria].filter(Boolean).join(' · ') || 'Todo el inventario'

function ChipConteo({ c }: { c: ConteoResumen }) {
  if (c.estado === 'Aplicado') return <Chip size="small" color="success" variant="outlined" label="Aplicado" />
  return <Chip size="small" color={c.contados === c.totalArticulos ? 'info' : 'warning'} variant="outlined"
    label={c.contados === c.totalArticulos ? 'Listo para aplicar' : 'En conteo'} />
}

function Diferencia({ valor, unidad }: { valor: number | null; unidad: string }) {
  if (valor === null) return <Typography component="span" color="text.disabled">—</Typography>
  if (valor === 0) return <Typography component="span" color="success.main">Cuadra</Typography>
  return (
    <Typography component="span" sx={{ fontWeight: 600, color: valor > 0 ? 'info.main' : 'error.main', whiteSpace: 'nowrap' }}>
      {valor > 0 ? '+' : '−'}{fmtNumero(Math.abs(valor))} {unidad}
    </Typography>
  )
}

/** RF-28: abrir un conteo por ubicación, por categoría o de todo. */
function NuevoConteoDialog({ onClose }: { onClose: () => void }) {
  const navegar = useNavigate()
  const qc = useQueryClient()
  const ubicaciones = useUbicaciones()
  const categorias = useCategorias()
  const [ubicacionId, setUbicacionId] = useState<number | ''>('')
  const [categoriaId, setCategoriaId] = useState<number | ''>('')
  const crear = useMutation({
    mutationFn: async () => (await api.post<Conteo>('/conteos', { ubicacionId: ubicacionId || null, categoriaId: categoriaId || null })).data,
    onSuccess: (c) => {
      qc.invalidateQueries({ queryKey: ['conteos'] })
      navegar(`/inventario-fisico/${c.conteo.id}`)
    },
  })
  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>Nuevo conteo físico</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2.5}>
          {crear.isError && <Alert severity="error">{mensajeError(crear.error)}</Alert>}
          <DialogContentText>
            Se guarda la existencia que tiene el sistema en este momento. Después se anota lo que realmente hay en el estante.
          </DialogContentText>
          <TextField id="conteo-ubicacion" select label="Ubicación" slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }} value={ubicacionId} onChange={(e) => setUbicacionId(e.target.value === '' ? '' : Number(e.target.value))}>
            <MenuItem value="">Todas</MenuItem>
            {ubicaciones.data?.map((u) => <MenuItem key={u.id} value={u.id}>{u.nombre}</MenuItem>)}
          </TextField>
          <TextField id="conteo-categoria" select label="Categoría" slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }} value={categoriaId} onChange={(e) => setCategoriaId(e.target.value === '' ? '' : Number(e.target.value))}>
            <MenuItem value="">Todas</MenuItem>
            {categorias.data?.map((c) => <MenuItem key={c.id} value={c.id}>{c.nombre}</MenuItem>)}
          </TextField>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={() => crear.mutate()} disabled={crear.isPending}>{crear.isPending ? 'Abriendo…' : 'Abrir conteo'}</Button>
      </DialogActions>
    </Dialog>
  )
}

/** Lista de conteos (abiertos primero). */
export function InventarioFisicoPage() {
  const navegar = useNavigate()
  const [nuevo, setNuevo] = useState(false)
  const { data, isLoading } = useQuery({ queryKey: ['conteos'], queryFn: async () => (await api.get<ConteoResumen[]>('/conteos')).data })

  return (
    <Pagina
      titulo="Inventario físico"
      subtitulo="Contar lo que hay en el taller, compararlo con el sistema y corregir las diferencias."
      acciones={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setNuevo(true)}>Nuevo conteo</Button>}
    >
      <Paper sx={{ minWidth: 0 }}>
        {isLoading ? <Cargando /> : !data?.length ? <Vacio>Todavía no se ha hecho ningún conteo.</Vacio> : (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>N.º</TableCell><TableCell>Fecha</TableCell><TableCell>Qué se cuenta</TableCell><TableCell>Responsable</TableCell>
                  <TableCell sx={{ minWidth: 160 }}>Avance</TableCell><TableCell align="right">Diferencias</TableCell><TableCell>Estado</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.map((c) => (
                  <TableRow key={c.id} hover sx={{ cursor: 'pointer' }} onClick={() => navegar(`/inventario-fisico/${c.id}`)}>
                    <TableCell>#{c.id}</TableCell>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFecha(c.fecha)}</TableCell>
                    <TableCell>{alcance(c)}</TableCell>
                    <TableCell>{c.responsable}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <LinearProgress variant="determinate" value={(c.contados / Math.max(c.totalArticulos, 1)) * 100} sx={{ flex: 1, height: 6, borderRadius: 3 }} />
                        <Typography variant="caption" sx={{ fontVariantNumeric: 'tabular-nums' }}>{c.contados}/{c.totalArticulos}</Typography>
                      </Stack>
                    </TableCell>
                    <TableCell align="right">{c.conDiferencia}</TableCell>
                    <TableCell><ChipConteo c={c} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </Paper>
      {nuevo && <NuevoConteoDialog onClose={() => setNuevo(false)} />}
    </Pagina>
  )
}

/** RF-28 (anotar lo contado) y RF-29 (comparar y aplicar las diferencias como ajustes). */
export function ConteoPage() {
  const id = Number(useParams().id)
  const { tiene } = useAuth()
  const qc = useQueryClient()
  const navegar = useNavigate()
  const notificar = useNotificar()
  const [cambios, setCambios] = useState<Record<number, string>>({})
  const [filtro, setFiltro] = useState('')
  const [soloDiferencias, setSoloDiferencias] = useState(false)
  const [confirmar, setConfirmar] = useState<'aplicar' | 'eliminar' | null>(null)

  const { data, isLoading } = useQuery({ queryKey: ['conteos', id], queryFn: async () => (await api.get<Conteo>(`/conteos/${id}`)).data })
  const refrescar = (c?: Conteo) => {
    if (c) qc.setQueryData(['conteos', id], c)
    qc.invalidateQueries({ queryKey: ['conteos'] })
  }

  const guardar = useMutation({
    mutationFn: async () => (await api.put<Conteo>(`/conteos/${id}`, {
      cantidades: Object.entries(cambios).map(([detalleId, v]) => ({ detalleId: Number(detalleId), cantidad: v === '' ? null : Number(v) })),
    })).data,
    onSuccess: (c) => { setCambios({}); refrescar(c); notificar('Conteo guardado.') },
    onError: (e) => notificar(mensajeError(e), 'error'),
  })
  const aplicar = useMutation({
    mutationFn: async () => (await api.post<Conteo>(`/conteos/${id}/aplicar`)).data,
    onSuccess: (c) => {
      setConfirmar(null); refrescar(c)
      for (const k of [['articulos'], ['movimientos'], ['panel'], ['alertas']]) qc.invalidateQueries({ queryKey: k })
      notificar(c.conteo.conDiferencia ? `Se ajustaron ${c.conteo.conDiferencia} artículos.` : 'Conteo aplicado: todo cuadraba.')
    },
    onError: (e) => { setConfirmar(null); notificar(mensajeError(e), 'error') },
  })
  const eliminar = useMutation({
    mutationFn: () => api.delete(`/conteos/${id}`),
    onSuccess: () => {
      navegar('/inventario-fisico')
      qc.removeQueries({ queryKey: ['conteos', id] })
      qc.invalidateQueries({ queryKey: ['conteos'], exact: true })
      notificar('Conteo eliminado.')
    },
    onError: (e) => { setConfirmar(null); notificar(mensajeError(e), 'error') },
  })

  const filas = useMemo(() => {
    const t = filtro.trim().toLowerCase()
    return (data?.detalles ?? []).filter((d) =>
      (!t || d.codigo.toLowerCase().includes(t) || d.articulo.toLowerCase().includes(t)) &&
      (!soloDiferencias || (d.diferencia !== null && d.diferencia !== 0)))
  }, [data, filtro, soloDiferencias])

  if (isLoading || !data) return <Cargando />
  const c = data.conteo
  const abierto = c.estado === 'Abierto'
  const pendientes = Object.keys(cambios).length
  const listo = c.contados === c.totalArticulos && pendientes === 0

  return (
    <Pagina
      titulo={`Conteo #${c.id}`}
      subtitulo={
        <Breadcrumbs component="span" sx={{ fontSize: 'inherit' }}>
          <Link component={RouterLink} to="/inventario-fisico" underline="hover" color="inherit">Inventario físico</Link>
          <span>{alcance(c)} · {fmtFecha(c.fecha)} · {c.responsable}</span>
        </Breadcrumbs>
      }
      acciones={abierto && <>
        <Button variant="outlined" startIcon={<SaveOutlined />} onClick={() => guardar.mutate()} disabled={!pendientes || guardar.isPending}>
          {guardar.isPending ? 'Guardando…' : pendientes ? `Guardar (${pendientes})` : 'Guardado'}
        </Button>
        {tiene(Permisos.MovimientosAjustar) && (
          <Button variant="contained" startIcon={<DoneAllOutlined />} onClick={() => setConfirmar('aplicar')} disabled={!listo}>Aplicar diferencias</Button>
        )}
        <Button color="error" onClick={() => setConfirmar('eliminar')}>Eliminar</Button>
      </>}
    >
      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' } }}>
        {[
          ['Artículos', c.totalArticulos], ['Contados', c.contados], ['Sin contar', c.totalArticulos - c.contados], ['Con diferencia', c.conDiferencia],
        ].map(([t, v]) => (
          <Paper key={t} sx={{ p: 2 }}>
            <Typography sx={{ fontSize: '1.6rem', fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{v}</Typography>
            <Typography color="text.secondary">{t}</Typography>
          </Paper>
        ))}
      </Box>

      {abierto ? (
        <Alert severity="info">
          Anote lo que encontró en cada estante y guarde; puede seguir después. {tiene(Permisos.MovimientosAjustar)
            ? 'Cuando todo esté contado, aplique las diferencias: cada una se registra como un ajuste.'
            : 'Cuando todo esté contado, el administrador revisa y aplica las diferencias.'}
        </Alert>
      ) : (
        <Alert severity="success">Conteo aplicado. Las diferencias quedaron registradas como ajustes en Entradas y salidas.</Alert>
      )}

      <Paper sx={{ p: 2 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ alignItems: { sm: 'center' } }}>
          <TextField id="buscar" placeholder="Buscar en este conteo…" value={filtro} onChange={(e) => setFiltro(e.target.value)} sx={{ flex: 1 }}
            slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }} />
          <Button variant={soloDiferencias ? 'contained' : 'outlined'} onClick={() => setSoloDiferencias((v) => !v)}>Solo con diferencia</Button>
        </Stack>
      </Paper>

      <Paper sx={{ minWidth: 0 }}>
        {filas.length === 0 ? <Vacio>No hay artículos que mostrar.</Vacio> : (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Código</TableCell><TableCell>Artículo</TableCell><TableCell>Ubicación</TableCell>
                  <TableCell align="right">Sistema</TableCell><TableCell align="right" sx={{ minWidth: 150 }}>Contado</TableCell><TableCell align="right">Diferencia</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filas.map((d) => {
                  const editado = d.id in cambios
                  const valor = editado ? cambios[d.id] : d.cantidadContada?.toString() ?? ''
                  const dif = valor === '' ? null : Number(valor) - d.stockSistema
                  return (
                    <TableRow key={d.id} sx={{ bgcolor: dif ? (dif > 0 ? 'rgba(2,136,209,0.06)' : 'rgba(211,47,47,0.06)') : undefined }}>
                      <TableCell sx={{ fontFamily: 'monospace', whiteSpace: 'nowrap' }}>{d.codigo}</TableCell>
                      <TableCell>{d.articulo}</TableCell>
                      <TableCell>{d.ubicacion}</TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>{fmtNumero(d.stockSistema)} {d.unidadMedida}</TableCell>
                      <TableCell align="right">
                        {abierto ? (
                          <TextField size="small" type="number" value={valor} aria-label={`Contado de ${d.articulo}`}
                            onChange={(e) => setCambios((x) => ({ ...x, [d.id]: e.target.value }))}
                            slotProps={{ htmlInput: { min: 0, step: 'any', style: { textAlign: 'right' } } }}
                            sx={{ width: 130, '& .MuiOutlinedInput-root': editado ? { bgcolor: 'action.selected' } : {} }} />
                        ) : <Box sx={{ fontVariantNumeric: 'tabular-nums' }}>{fmtNumero(d.cantidadContada ?? 0)} {d.unidadMedida}</Box>}
                      </TableCell>
                      <TableCell align="right"><Diferencia valor={dif} unidad={d.unidadMedida} /></TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </Box>
        )}
      </Paper>

      <Dialog open={confirmar !== null} onClose={() => setConfirmar(null)} maxWidth="xs" fullWidth>
        <DialogTitle>{confirmar === 'aplicar' ? '¿Aplicar las diferencias?' : '¿Eliminar este conteo?'}</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirmar === 'aplicar'
              ? c.conDiferencia
                ? `Se registrará un ajuste en ${c.conDiferencia} artículos para que el sistema coincida con lo contado. Esta acción no se puede deshacer.`
                : 'Todo cuadra: no se hará ningún ajuste y el conteo quedará cerrado.'
              : 'Se borra lo anotado hasta ahora. Las existencias no cambian.'}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmar(null)}>Cancelar</Button>
          {confirmar === 'aplicar'
            ? <Button variant="contained" onClick={() => aplicar.mutate()} disabled={aplicar.isPending}>{aplicar.isPending ? 'Aplicando…' : 'Aplicar'}</Button>
            : <Button variant="contained" color="error" onClick={() => eliminar.mutate()} disabled={eliminar.isPending}>Eliminar</Button>}
        </DialogActions>
      </Dialog>
    </Pagina>
  )
}
