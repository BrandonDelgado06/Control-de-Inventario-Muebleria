import { useState, type FormEvent } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Autocomplete, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Stack, Tab, Table,
  TableBody, TableCell, TableHead, TablePagination, TableRow, Tabs, TextField, Tooltip, Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type ArticuloResumen, type EstadoSolicitud, type Paginado, type Solicitud } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, Pagina, Vacio } from '../../components/Pagina'
import { fmtFechaHora, fmtNumero } from '../../lib/formato'

const colorEstado: Record<EstadoSolicitud, 'warning' | 'info' | 'error' | 'success'> = {
  Pendiente: 'warning', Aprobada: 'info', Rechazada: 'error', Entregada: 'success',
}

export function ChipSolicitud({ estado }: { estado: EstadoSolicitud }) {
  return <Chip size="small" color={colorEstado[estado]} variant={estado === 'Pendiente' ? 'filled' : 'outlined'} label={estado} />
}

/** RF-23 a RF-25. */
export function SolicitudesPage() {
  const { tiene } = useAuth()
  const aprueba = tiene(Permisos.SolicitudesAprobar)
  const [estado, setEstado] = useState<EstadoSolicitud | ''>(aprueba ? 'Pendiente' : '')
  const [pagina, setPagina] = useState(0)
  const [nueva, setNueva] = useState(false)
  const [rechazando, setRechazando] = useState<Solicitud | null>(null)
  const qc = useQueryClient()
  const notificar = useNotificar()

  const params = { estado: estado || undefined, pagina: pagina + 1, tamano: 15 }
  const { data, isLoading } = useQuery({
    queryKey: ['solicitudes', params],
    queryFn: async () => (await api.get<Paginado<Solicitud>>('/solicitudes', { params })).data,
    placeholderData: keepPreviousData,
  })

  const accion = useMutation({
    mutationFn: async ({ id, que }: { id: number; que: 'aprobar' | 'entregar' }) => (await api.post<Solicitud>(`/solicitudes/${id}/${que}`)).data,
    onSuccess: (s) => {
      for (const k of [['solicitudes'], ['panel'], ['articulos'], ['movimientos'], ['menu-contadores']]) qc.invalidateQueries({ queryKey: k })
      notificar(s.estado === 'Entregada'
        ? `Entregado: ${fmtNumero(s.cantidad)} ${s.unidadMedida} de ${s.articulo}. Quedan ${fmtNumero(s.stockActual)}.`
        : `Solicitud #${s.id} aprobada. Falta entregarla.`)
    },
    onError: (e) => notificar(mensajeError(e), 'error'),
  })

  return (
    <Pagina
      titulo="Solicitudes de materiales"
      subtitulo={aprueba ? 'Revisa lo que piden los trabajadores, aprueba o rechaza, y registra la entrega.' : 'Pide los materiales o herramientas que necesitas y sigue el estado de tus solicitudes.'}
      acciones={tiene(Permisos.SolicitudesCrear) && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setNueva(true)}>Nueva solicitud</Button>}
    >
      <Tabs value={estado} onChange={(_, v) => { setEstado(v); setPagina(0) }} variant="scrollable" allowScrollButtonsMobile>
        <Tab value="" label="Todas" />
        <Tab value="Pendiente" label="Pendientes" />
        <Tab value="Aprobada" label="Por entregar" />
        <Tab value="Entregada" label="Entregadas" />
        <Tab value="Rechazada" label="Rechazadas" />
      </Tabs>

      <Paper sx={{ minWidth: 0 }}>
        {isLoading ? <Cargando /> : !data?.items.length ? <Vacio>No hay solicitudes en esta vista.</Vacio> : (
          <>
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>#</TableCell><TableCell>Fecha</TableCell>
                    {aprueba && <TableCell>Solicitante</TableCell>}
                    <TableCell>Artículo</TableCell><TableCell align="right">Cantidad</TableCell><TableCell align="right">Existencia</TableCell>
                    <TableCell>Motivo</TableCell><TableCell>Estado</TableCell>{aprueba && <TableCell align="right">Acciones</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.items.map((s) => {
                    const falta = s.estado !== 'Entregada' && s.estado !== 'Rechazada' && s.cantidad > s.stockActual
                    return (
                      <TableRow key={s.id} hover>
                        <TableCell sx={{ color: 'text.secondary' }}>{s.id}</TableCell>
                        <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(s.fechaSolicitud)}</TableCell>
                        {aprueba && <TableCell>{s.solicitante}</TableCell>}
                        <TableCell>{s.articulo}<Typography variant="caption" color="text.secondary" component="div" sx={{ fontFamily: 'monospace' }}>{s.codigo}</Typography></TableCell>
                        <TableCell align="right" sx={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>{fmtNumero(s.cantidad)} {s.unidadMedida}</TableCell>
                        <TableCell align="right" sx={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums', color: falta ? 'error.main' : 'text.secondary' }}>
                          {fmtNumero(s.stockActual)}{falta && ' · no alcanza'}
                        </TableCell>
                        <TableCell>
                          {s.motivo}
                          {s.motivoRechazo && <Typography variant="caption" color="error" component="div">Rechazo: {s.motivoRechazo}</Typography>}
                          {s.aprobador && s.estado !== 'Rechazada' && <Typography variant="caption" color="text.secondary" component="div">Aprobó: {s.aprobador}</Typography>}
                        </TableCell>
                        <TableCell><ChipSolicitud estado={s.estado} /></TableCell>
                        {aprueba && (
                          <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                            {s.estado === 'Pendiente' && <>
                              <Button size="small" onClick={() => accion.mutate({ id: s.id, que: 'aprobar' })} disabled={accion.isPending}>Aprobar</Button>
                              <Button size="small" color="error" onClick={() => setRechazando(s)}>Rechazar</Button>
                            </>}
                            {s.estado === 'Aprobada' && (
                              <Tooltip title={falta ? 'No hay existencia suficiente para entregar' : 'Descuenta la existencia y registra la salida'}>
                                <span><Button size="small" variant="contained" onClick={() => accion.mutate({ id: s.id, que: 'entregar' })} disabled={accion.isPending || falta}>Entregar</Button></span>
                              </Tooltip>
                            )}
                          </TableCell>
                        )}
                      </TableRow>
                    )
                  })}
                </TableBody>
              </Table>
            </Box>
            <TablePagination component="div" count={data.total} page={pagina} rowsPerPage={15} rowsPerPageOptions={[15]}
              onPageChange={(_, p) => setPagina(p)} labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`} />
          </>
        )}
      </Paper>

      {nueva && <NuevaSolicitudDialog onClose={() => setNueva(false)} />}
      {rechazando && <RechazarDialog solicitud={rechazando} onClose={() => setRechazando(null)} />}
    </Pagina>
  )
}

function NuevaSolicitudDialog({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [articulo, setArticulo] = useState<ArticuloResumen | null>(null)
  const [busqueda, setBusqueda] = useState('')
  const [cantidad, setCantidad] = useState('')
  const [motivo, setMotivo] = useState('')
  const opciones = useQuery({
    queryKey: ['articulos', 'buscar', busqueda],
    queryFn: async () => (await api.get<Paginado<ArticuloResumen>>('/articulos', { params: { texto: busqueda, tamano: 15 } })).data.items,
  })
  const crear = useMutation({
    mutationFn: async () => (await api.post<Solicitud>('/solicitudes', { articuloId: articulo?.id ?? 0, cantidad: Number(cantidad), motivo })).data,
    onSuccess: (s) => {
      qc.invalidateQueries({ queryKey: ['solicitudes'] }); qc.invalidateQueries({ queryKey: ['menu-contadores'] })
      notificar(`Solicitud #${s.id} enviada. El encargado de bodega la revisará.`)
      onClose()
    },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); crear.mutate() }

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>Nueva solicitud</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2.5}>
          {crear.isError && <Alert severity="error">{mensajeError(crear.error)}</Alert>}
          <Autocomplete
            id="articulo" options={opciones.data ?? []} value={articulo}
            onChange={(_, v) => setArticulo(v)} onInputChange={(_, v) => setBusqueda(v)}
            getOptionLabel={(o) => `${o.codigo} · ${o.nombre}`} isOptionEqualToValue={(a, b) => a.id === b.id}
            filterOptions={(x) => x} loading={opciones.isFetching} noOptionsText="Sin resultados"
            renderInput={(p) => <TextField {...p} label="¿Qué necesitas?" required placeholder="Busca por código, nombre u otro nombre" />}
          />
          <TextField id="cantidad" label="Cantidad" type="number" required value={cantidad} onChange={(e) => setCantidad(e.target.value)}
            slotProps={{ htmlInput: { min: 0.01, step: 'any' } }}
            helperText={articulo ? `Hay ${fmtNumero(articulo.stockActual)} ${articulo.unidadMedida} en ${articulo.ubicacion}` : ' '} />
          <TextField id="motivo" label="¿Para qué lo necesitas?" required multiline minRows={2} value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="Ej.: lijar la cómoda del pedido de la señora Rodríguez" />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={crear.isPending || !articulo || !cantidad || !motivo.trim()}>Enviar solicitud</Button>
      </DialogActions>
    </Dialog>
  )
}

function RechazarDialog({ solicitud, onClose }: { solicitud: Solicitud; onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [motivo, setMotivo] = useState('')
  const rechazar = useMutation({
    mutationFn: () => api.post(`/solicitudes/${solicitud.id}/rechazar`, { motivo }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['solicitudes'] }); qc.invalidateQueries({ queryKey: ['menu-contadores'] })
      notificar(`Solicitud #${solicitud.id} rechazada.`)
      onClose()
    },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); rechazar.mutate() }
  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>Rechazar solicitud #{solicitud.id}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Typography>{solicitud.solicitante} pidió {fmtNumero(solicitud.cantidad)} {solicitud.unidadMedida} de {solicitud.articulo}.</Typography>
          {rechazar.isError && <Alert severity="error">{mensajeError(rechazar.error)}</Alert>}
          <TextField id="motivo-rechazo" label="Motivo del rechazo" required multiline minRows={2} value={motivo} onChange={(e) => setMotivo(e.target.value)}
            helperText="El trabajador verá este motivo." />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" color="error" variant="contained" disabled={rechazar.isPending || !motivo.trim()}>Rechazar</Button>
      </DialogActions>
    </Dialog>
  )
}
