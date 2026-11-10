import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Paper, Stack,
  Switch, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { api, mensajeError } from '../../api/cliente'
import type { ArticuloResumen, EstadoArticulo, Paginado, SalidaHerramienta } from '../../api/tipos'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, ChipEstado, Pagina, Vacio } from '../../components/Pagina'
import { fmtFecha, nombreEstado } from '../../lib/formato'

/** RF-16 a RF-18: herramientas que salen del taller a instalar muebles. */
export function InstalacionPage() {
  const [historial, setHistorial] = useState(false)
  const [salida, setSalida] = useState(false)
  const [devolviendo, setDevolviendo] = useState<SalidaHerramienta | null>(null)
  const { data, isLoading } = useQuery({
    queryKey: ['instalacion', historial],
    queryFn: async () => (await api.get<SalidaHerramienta[]>('/instalacion', { params: { soloFuera: !historial } })).data,
  })

  return (
    <Pagina
      titulo="Herramientas fuera del taller"
      subtitulo="Quién se llevó cada herramienta de instalación, a dónde y cuándo debe devolverla."
      acciones={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setSalida(true)}>Registrar salida</Button>}
    >
      <FormControlLabel control={<Switch id="historial" checked={historial} onChange={(e) => setHistorial(e.target.checked)} />} label="Ver también las devueltas" />
      <Paper sx={{ minWidth: 0 }}>
        {isLoading ? <Cargando /> : !data?.length ? <Vacio>{historial ? 'Todavía no se ha registrado ninguna salida.' : 'Todas las herramientas de instalación están en el taller.'}</Vacio> : (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Herramienta</TableCell><TableCell>Responsable</TableCell><TableCell>Destino</TableCell>
                  <TableCell>Salió</TableCell><TableCell>Debe volver</TableCell><TableCell>Estado</TableCell><TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {data.map((s) => (
                  <TableRow key={s.id} hover>
                    <TableCell>{s.herramienta}<Typography variant="caption" color="text.secondary" component="div" sx={{ fontFamily: 'monospace' }}>{s.codigo}</Typography></TableCell>
                    <TableCell>{s.responsable}</TableCell>
                    <TableCell>{s.destino}</TableCell>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFecha(s.fechaSalida)}</TableCell>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFecha(s.fechaPrevista)}</TableCell>
                    <TableCell>
                      {s.fechaDevolucion
                        ? <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><Typography variant="body2">Devuelta el {fmtFecha(s.fechaDevolucion)}</Typography>{s.estadoDevolucion && <ChipEstado estado={s.estadoDevolucion} />}</Stack>
                        : s.diasRetraso > 0
                          ? <Chip size="small" color="error" label={`${s.diasRetraso} día${s.diasRetraso === 1 ? '' : 's'} de retraso`} />
                          : <Chip size="small" color="info" variant="outlined" label="Fuera del taller" />}
                    </TableCell>
                    <TableCell align="right">{!s.fechaDevolucion && <Button size="small" onClick={() => setDevolviendo(s)}>Registrar devolución</Button>}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </Paper>
      {salida && <SalidaDialog onClose={() => setSalida(false)} />}
      {devolviendo && <DevolucionDialog salida={devolviendo} onClose={() => setDevolviendo(null)} />}
    </Pagina>
  )
}

function invalidar(qc: ReturnType<typeof useQueryClient>) {
  for (const k of [['instalacion'], ['articulos'], ['panel'], ['movimientos'], ['alertas'], ['menu-contadores']]) qc.invalidateQueries({ queryKey: k })
}

function SalidaDialog({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const manana = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10)
  const [articuloId, setArticuloId] = useState<number | ''>('')
  const [responsableId, setResponsableId] = useState<number | ''>('')
  const [destino, setDestino] = useState('')
  const [prevista, setPrevista] = useState(manana)
  const herramientas = useQuery({
    queryKey: ['articulos', 'instalacion'],
    queryFn: async () => (await api.get<Paginado<ArticuloResumen>>('/articulos', { params: { tipo: 'Instalacion', tamano: 200 } })).data.items,
  })
  const personas = useQuery({
    queryKey: ['instalacion', 'responsables'],
    queryFn: async () => (await api.get<{ id: number; nombre: string }[]>('/instalacion/responsables')).data,
  })
  const registrar = useMutation({
    mutationFn: () => api.post('/instalacion', { articuloId: articuloId || 0, responsableId: responsableId || 0, destino, fechaPrevista: prevista }),
    onSuccess: () => { invalidar(qc); notificar('Salida registrada. La herramienta queda como no disponible.'); onClose() },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); registrar.mutate() }

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>Registrar salida de herramienta</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {registrar.isError && <Alert severity="error">{mensajeError(registrar.error)}</Alert>}
          <TextField id="herramienta" select label="Herramienta" required value={articuloId} onChange={(e) => setArticuloId(Number(e.target.value))}>
            {herramientas.data?.map((h) => (
              <MenuItem key={h.id} value={h.id} disabled={h.stockActual < 1}>
                {h.nombre} — {h.stockActual < 1 ? 'todas fuera' : `${h.stockActual} disponible${h.stockActual === 1 ? '' : 's'}`}
              </MenuItem>
            ))}
          </TextField>
          <TextField id="responsable" select label="¿Quién se la lleva?" required value={responsableId} onChange={(e) => setResponsableId(Number(e.target.value))}>
            {personas.data?.map((p) => <MenuItem key={p.id} value={p.id}>{p.nombre}</MenuItem>)}
          </TextField>
          <TextField id="destino" label="Destino" required value={destino} onChange={(e) => setDestino(e.target.value)} placeholder="Ej.: casa de la familia Mora, San Pablo de Heredia" helperText="Solo como referencia; el sistema no guarda datos de clientes." />
          <TextField id="prevista" label="Fecha prevista de devolución" type="date" required value={prevista} onChange={(e) => setPrevista(e.target.value)} slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: new Date().toISOString().slice(0, 10) } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={registrar.isPending}>Registrar salida</Button>
      </DialogActions>
    </Dialog>
  )
}

function DevolucionDialog({ salida, onClose }: { salida: SalidaHerramienta; onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [estado, setEstado] = useState<EstadoArticulo>('Buena')
  const devolver = useMutation({
    mutationFn: () => api.post(`/instalacion/${salida.id}/devolucion`, { estado }),
    onSuccess: () => { invalidar(qc); notificar(`${salida.herramienta} volvió al taller.`); onClose() },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); devolver.mutate() }
  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>Devolución</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Typography><b>{salida.herramienta}</b> · {salida.responsable}<br /><Typography component="span" variant="body2" color="text.secondary">{salida.destino}</Typography></Typography>
          {devolver.isError && <Alert severity="error">{mensajeError(devolver.error)}</Alert>}
          <TextField id="estado-devolucion" select label="¿En qué estado regresó?" value={estado} onChange={(e) => setEstado(e.target.value as EstadoArticulo)}
            helperText="Si regresó desgastada o dañada, se actualiza el estado de la herramienta.">
            {(['Nueva', 'Buena', 'Desgastada', 'Danada'] as EstadoArticulo[]).map((k) => <MenuItem key={k} value={k}>{nombreEstado[k]}</MenuItem>)}
          </TextField>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={devolver.isPending}>Registrar devolución</Button>
      </DialogActions>
    </Dialog>
  )
}
