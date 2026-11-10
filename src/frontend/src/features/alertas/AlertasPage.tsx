import { useState, type ReactNode } from 'react'
import { Link as RouterLink } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Chip, InputAdornment, Link, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined'
import EventBusyOutlined from '@mui/icons-material/EventBusyOutlined'
import HandymanOutlined from '@mui/icons-material/HandymanOutlined'
import BackupOutlined from '@mui/icons-material/BackupOutlined'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type Alertas } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, ChipStock, Pagina } from '../../components/Pagina'
import { fmtFecha, fmtNumero } from '../../lib/formato'

function Seccion({ icono, titulo, requisito, cantidad, tono, children }: {
  icono: ReactNode; titulo: string; requisito: string; cantidad: number; tono: 'warning' | 'error'; children: ReactNode
}) {
  return (
    <Paper sx={{ minWidth: 0, borderLeft: 4, borderLeftColor: cantidad ? `${tono}.main` : 'success.main' }}>
      <Stack direction="row" spacing={1.5} sx={{ p: 2, alignItems: 'center' }}>
        <Box sx={{ color: cantidad ? `${tono}.main` : 'success.main', display: 'flex' }}>{icono}</Box>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h2">{titulo}</Typography>
          <Typography variant="caption" color="text.secondary">{requisito}</Typography>
        </Box>
        <Chip color={cantidad ? tono : 'success'} label={cantidad ? `${cantidad} activa${cantidad === 1 ? '' : 's'}` : 'Sin alertas'} />
      </Stack>
      {cantidad > 0 && <Box sx={{ overflowX: 'auto' }}>{children}</Box>}
    </Paper>
  )
}

/** RF-30 a RF-32 (y aviso de respaldo semanal, RNF-07). */
export function AlertasPage() {
  const { tiene } = useAuth()
  const { data, isLoading } = useQuery({ queryKey: ['alertas'], queryFn: async () => (await api.get<Alertas>('/alertas')).data })

  return (
    <Pagina titulo="Alertas" subtitulo="Se calculan con los datos de este momento: lo que hay que reponer, lo que está por vencer y lo que no ha regresado.">
      {isLoading || !data ? <Cargando /> : <>
        {data.respaldo && (
          <Alert severity="warning" icon={<BackupOutlined />} action={<Button color="inherit" size="small" component={RouterLink} to="/respaldos">Ir a respaldos</Button>}>
            {data.respaldo.mensaje}
          </Alert>
        )}

        <Seccion icono={<Inventory2Outlined />} titulo="Stock mínimo" requisito="RF-30 · Existencia igual o menor al mínimo definido" cantidad={data.stockMinimo.length} tono="warning">
          <Table size="small">
            <TableHead><TableRow><TableCell>Artículo</TableCell><TableCell>Ubicación</TableCell><TableCell>Existencia</TableCell><TableCell align="right">Mínimo</TableCell><TableCell align="right">Faltan</TableCell></TableRow></TableHead>
            <TableBody>
              {data.stockMinimo.map((a) => (
                <TableRow key={a.id} hover>
                  <TableCell><Link component={RouterLink} to={`/articulos/${a.id}`} underline="hover">{a.nombre}</Link></TableCell>
                  <TableCell>{a.ubicacion}</TableCell>
                  <TableCell><ChipStock actual={a.stockActual} minimo={a.stockMinimo} unidad={a.unidadMedida} /></TableCell>
                  <TableCell align="right">{fmtNumero(a.stockMinimo)}</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 600 }}>{fmtNumero(Math.max(0, a.stockMinimo - a.stockActual))}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Seccion>

        <Seccion icono={<EventBusyOutlined />} titulo="Productos por vencer" requisito={`RF-31 · Vencidos o que vencen en los próximos ${data.diasAnticipacion} días`} cantidad={data.vencimientos.length} tono="error">
          <Table size="small">
            <TableHead><TableRow><TableCell>Producto</TableCell><TableCell>Lote</TableCell><TableCell>Vence</TableCell><TableCell align="right">Existencia</TableCell><TableCell /></TableRow></TableHead>
            <TableBody>
              {data.vencimientos.map((v) => (
                <TableRow key={v.articuloId} hover>
                  <TableCell><Link component={RouterLink} to={`/articulos/${v.articuloId}`} underline="hover">{v.producto}</Link></TableCell>
                  <TableCell>{v.lote ?? '—'}</TableCell>
                  <TableCell>{fmtFecha(v.fechaVencimiento)}</TableCell>
                  <TableCell align="right">{fmtNumero(v.stockActual)} {v.unidadMedida}</TableCell>
                  <TableCell align="right">
                    <Chip size="small" color={v.diasRestantes < 0 ? 'error' : 'warning'}
                      label={v.diasRestantes < 0 ? `Vencido hace ${-v.diasRestantes} días` : v.diasRestantes === 0 ? 'Vence hoy' : `Faltan ${v.diasRestantes} días`} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Seccion>

        <Seccion icono={<HandymanOutlined />} titulo="Herramientas sin devolver" requisito="RF-32 · Fuera del taller después de la fecha prevista" cantidad={data.herramientasSinDevolver.length} tono="error">
          <Table size="small">
            <TableHead><TableRow><TableCell>Herramienta</TableCell><TableCell>Responsable</TableCell><TableCell>Destino</TableCell><TableCell>Debía volver</TableCell><TableCell align="right">Retraso</TableCell></TableRow></TableHead>
            <TableBody>
              {data.herramientasSinDevolver.map((h) => (
                <TableRow key={h.id} hover>
                  <TableCell>{h.herramienta}</TableCell><TableCell>{h.responsable}</TableCell><TableCell>{h.destino}</TableCell>
                  <TableCell>{fmtFecha(h.fechaPrevista)}</TableCell>
                  <TableCell align="right"><Chip size="small" color="error" label={`${h.diasRetraso} día${h.diasRetraso === 1 ? '' : 's'}`} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Seccion>

        {tiene(Permisos.CatalogosGestionar) && <ConfigAnticipacion actual={data.diasAnticipacion} />}
      </>}
    </Pagina>
  )
}

function ConfigAnticipacion({ actual }: { actual: number }) {
  const [dias, setDias] = useState(String(actual))
  const qc = useQueryClient()
  const notificar = useNotificar()
  const guardar = useMutation({
    mutationFn: () => api.put('/alertas/configuracion', { diasAnticipacion: Number(dias) }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['alertas'] }); qc.invalidateQueries({ queryKey: ['menu-contadores'] }); qc.invalidateQueries({ queryKey: ['panel'] }); notificar('Anticipación actualizada.') },
    onError: (e) => notificar(mensajeError(e), 'error'),
  })
  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: { sm: 'center' } }}>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h2">Anticipación de vencimiento</Typography>
          <Typography variant="body2" color="text.secondary">Con cuántos días de anticipación avisar que un producto de acabado está por vencer (RF-31).</Typography>
        </Box>
        <TextField id="dias-anticipacion" type="number" value={dias} onChange={(e) => setDias(e.target.value)} sx={{ width: 140 }}
          slotProps={{ input: { endAdornment: <InputAdornment position="end">días</InputAdornment> }, htmlInput: { min: 1, max: 365 } }} />
        <Button variant="outlined" onClick={() => guardar.mutate()} disabled={guardar.isPending || Number(dias) === actual}>Guardar</Button>
      </Stack>
    </Paper>
  )
}
