import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Stack, Table, TableBody, TableCell,
  TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import BackupOutlined from '@mui/icons-material/BackupOutlined'
import { api, mensajeError } from '../../api/cliente'
import type { RespaldoInfo } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, Pagina, Vacio } from '../../components/Pagina'
import { fmtBytes } from '../../lib/descargar'
import { fmtFechaHora } from '../../lib/formato'

const nombreArchivo = (ruta: string) => ruta.split(/[\\/]/).pop() ?? ruta

/** RF-38 y RF-39 (RNF-07). */
export function RespaldosPage() {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [restaurando, setRestaurando] = useState<RespaldoInfo | null>(null)
  const { data, isLoading } = useQuery({
    queryKey: ['respaldos'],
    queryFn: async () => (await api.get<{ carpeta: string; respaldos: RespaldoInfo[] }>('/respaldos')).data,
  })
  const generar = useMutation({
    mutationFn: async () => (await api.post<RespaldoInfo>('/respaldos')).data,
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['respaldos'] }); qc.invalidateQueries({ queryKey: ['alertas'] }); qc.invalidateQueries({ queryKey: ['menu-contadores'] })
      notificar(`Respaldo creado: ${nombreArchivo(r.archivo)} (${fmtBytes(r.tamanoBytes)}).`)
    },
    onError: (e) => notificar(mensajeError(e), 'error'),
  })
  const ultimo = data?.respaldos[0]
  const dias = ultimo ? Math.floor((Date.now() - new Date(ultimo.fecha).getTime()) / 86_400_000) : null

  return (
    <Pagina
      titulo="Respaldo y restauración"
      subtitulo="Copias de seguridad de la base de datos. Haz al menos una por semana y cópiala a una USB o disco externo."
      acciones={<Button variant="contained" startIcon={<BackupOutlined />} onClick={() => generar.mutate()} disabled={generar.isPending}>{generar.isPending ? 'Generando…' : 'Generar respaldo ahora'}</Button>}
    >
      {isLoading || !data ? <Cargando /> : <>
        {dias === null
          ? <Alert severity="warning">Todavía no hay ninguna copia de seguridad. Genera la primera ahora.</Alert>
          : dias >= 7
            ? <Alert severity="warning">El último respaldo tiene {dias} días. Se recomienda al menos uno por semana (RNF-07).</Alert>
            : <Alert severity="success">Último respaldo: {fmtFechaHora(ultimo!.fecha)}{dias === 0 ? ' (hoy)' : ` (hace ${dias} día${dias === 1 ? '' : 's'})`}.</Alert>}

        <Paper sx={{ p: 2 }}>
          <Typography variant="h2" sx={{ mb: 0.5 }}>Dónde quedan los archivos</Typography>
          <Typography variant="body2" color="text.secondary">SQL Server guarda cada copia en esta carpeta del equipo del taller. Para protegerla de una falla del equipo, cópiala a una USB o un disco externo.</Typography>
          <Box component="code" sx={{ display: 'block', mt: 1, p: 1, bgcolor: 'action.hover', borderRadius: 1, fontSize: '0.85rem', overflowWrap: 'anywhere' }}>{data.carpeta}</Box>
        </Paper>

        <Paper sx={{ minWidth: 0 }}>
          {!data.respaldos.length ? <Vacio>No hay respaldos registrados.</Vacio> : (
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead><TableRow><TableCell>Fecha</TableCell><TableCell>Archivo</TableCell><TableCell>Tipo</TableCell><TableCell align="right">Tamaño</TableCell><TableCell /></TableRow></TableHead>
                <TableBody>
                  {data.respaldos.map((r) => (
                    <TableRow key={r.id} hover>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(r.fecha)}</TableCell>
                      <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{nombreArchivo(r.archivo)}</TableCell>
                      <TableCell>{r.descripcion ?? '—'}</TableCell>
                      <TableCell align="right">{fmtBytes(r.tamanoBytes)}</TableCell>
                      <TableCell align="right"><Button size="small" color="warning" onClick={() => setRestaurando(r)}>Restaurar</Button></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
        </Paper>
      </>}
      {restaurando && <RestaurarDialog respaldo={restaurando} onClose={() => setRestaurando(null)} />}
    </Pagina>
  )
}

function RestaurarDialog({ respaldo, onClose }: { respaldo: RespaldoInfo; onClose: () => void }) {
  const { cerrar } = useAuth()
  const notificar = useNotificar()
  const [confirmacion, setConfirmacion] = useState('')
  const restaurar = useMutation({
    mutationFn: () => api.post('/respaldos/restaurar', { respaldoId: respaldo.id, confirmacion }),
    onSuccess: async () => {
      notificar('Base de datos restaurada. Vuelve a iniciar sesión.', 'info')
      await cerrar().catch(() => {})
    },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); restaurar.mutate() }
  return (
    <Dialog open onClose={restaurar.isPending ? undefined : onClose} fullWidth maxWidth="sm" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>Restaurar la base de datos</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity="error">
            Todos los datos actuales se reemplazan por los del {fmtFechaHora(respaldo.fecha)}. Lo que se registró después de esa fecha se pierde.
          </Alert>
          <Typography variant="body2">Antes de restaurar, el sistema guarda automáticamente una copia del estado actual, por si necesitas volver atrás. Al terminar tendrás que iniciar sesión otra vez.</Typography>
          {restaurar.isError && <Alert severity="error">{mensajeError(restaurar.error)}</Alert>}
          <TextField id="confirmacion" label="Escribe RESTAURAR para confirmar" value={confirmacion} onChange={(e) => setConfirmacion(e.target.value)} autoComplete="off" />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={restaurar.isPending}>Cancelar</Button>
        <Button type="submit" color="error" variant="contained" disabled={restaurar.isPending || confirmacion !== 'RESTAURAR'}>
          {restaurar.isPending ? 'Restaurando…' : 'Restaurar'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
