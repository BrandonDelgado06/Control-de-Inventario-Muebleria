import { useState, type FormEvent } from 'react'
import { Link as RouterLink } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Link, Paper,
  Stack, Switch, Table, TableBody, TableCell, TableHead, TableRow, TextField,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type ArticuloResumen, type Ubicacion } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, ChipStock, Pagina, Vacio } from '../../components/Pagina'
import { useUbicaciones } from '../catalogo/ArticulosPage'

/** RF-05: crear, editar y desactivar ubicaciones. RF-06: ver qué hay en cada una. */
export function UbicacionesPage() {
  const { tiene } = useAuth()
  const puedeEditar = tiene(Permisos.CatalogosGestionar)
  const [inactivas, setInactivas] = useState(false)
  const [editando, setEditando] = useState<Ubicacion | 'nueva' | null>(null)
  const [viendo, setViendo] = useState<Ubicacion | null>(null)
  const { data, isLoading } = useUbicaciones(inactivas)

  return (
    <Pagina
      titulo="Ubicaciones"
      subtitulo="Bodegas, estantes y zonas del taller donde se guarda cada artículo."
      acciones={puedeEditar && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditando('nueva')}>Nueva ubicación</Button>}
    >
      <FormControlLabel control={<Checkbox id="inactivas" checked={inactivas} onChange={(e) => setInactivas(e.target.checked)} />} label="Incluir desactivadas" />
      <Paper sx={{ minWidth: 0 }}>
        {isLoading ? <Cargando /> : !data?.length ? <Vacio>No hay ubicaciones.</Vacio> : (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead><TableRow><TableCell>Nombre</TableCell><TableCell>Descripción</TableCell><TableCell align="right">Artículos</TableCell><TableCell /></TableRow></TableHead>
              <TableBody>
                {data.map((u) => (
                  <TableRow key={u.id} sx={{ opacity: u.activo ? 1 : 0.55 }}>
                    <TableCell sx={{ fontWeight: 500 }}>{u.nombre}{!u.activo && ' · desactivada'}</TableCell>
                    <TableCell>{u.descripcion ?? '—'}</TableCell>
                    <TableCell align="right">
                      <Link component="button" type="button" underline="hover" onClick={() => setViendo(u)}>{u.cantidadArticulos}</Link>
                    </TableCell>
                    <TableCell align="right">{puedeEditar && <Button size="small" onClick={() => setEditando(u)}>Editar</Button>}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </Paper>
      {editando && <UbicacionDialog ubicacion={editando === 'nueva' ? undefined : editando} onClose={() => setEditando(null)} />}
      {viendo && <ArticulosEnUbicacion ubicacion={viendo} onClose={() => setViendo(null)} />}
    </Pagina>
  )
}

function UbicacionDialog({ ubicacion, onClose }: { ubicacion?: Ubicacion; onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [nombre, setNombre] = useState(ubicacion?.nombre ?? '')
  const [descripcion, setDescripcion] = useState(ubicacion?.descripcion ?? '')
  const [activo, setActivo] = useState(ubicacion?.activo ?? true)
  const guardar = useMutation({
    mutationFn: () => {
      const cuerpo = { nombre, descripcion, activo }
      return ubicacion ? api.put(`/ubicaciones/${ubicacion.id}`, cuerpo) : api.post('/ubicaciones', cuerpo)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ubicaciones'] })
      notificar(ubicacion ? 'Ubicación actualizada.' : 'Ubicación creada.')
      onClose()
    },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); guardar.mutate() }

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>{ubicacion ? 'Editar ubicación' : 'Nueva ubicación'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {guardar.isError && <Alert severity="error">{mensajeError(guardar.error)}</Alert>}
          <TextField id="nombre" label="Nombre" required value={nombre} onChange={(e) => setNombre(e.target.value)} placeholder="Ej.: Estante 3" />
          <TextField id="descripcion" label="Descripción" multiline minRows={2} value={descripcion} onChange={(e) => setDescripcion(e.target.value)} />
          {ubicacion && <FormControlLabel control={<Switch id="activo" checked={activo} onChange={(e) => setActivo(e.target.checked)} />} label="Activa" />}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={guardar.isPending}>Guardar</Button>
      </DialogActions>
    </Dialog>
  )
}

function ArticulosEnUbicacion({ ubicacion, onClose }: { ubicacion: Ubicacion; onClose: () => void }) {
  const { data, isLoading } = useQuery({
    queryKey: ['ubicacion-articulos', ubicacion.id],
    queryFn: async () => (await api.get<ArticuloResumen[]>(`/ubicaciones/${ubicacion.id}/articulos`)).data,
  })
  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{ubicacion.nombre}</DialogTitle>
      <DialogContent dividers sx={{ p: 0 }}>
        {isLoading ? <Cargando /> : !data?.length ? <Vacio>No hay artículos en esta ubicación.</Vacio> : (
          <Table size="small">
            <TableBody>
              {data.map((a) => (
                <TableRow key={a.id}>
                  <TableCell><Link component={RouterLink} to={`/articulos/${a.id}`} underline="hover">{a.nombre}</Link></TableCell>
                  <TableCell align="right"><ChipStock actual={a.stockActual} minimo={a.stockMinimo} unidad={a.unidadMedida} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Cerrar</Button></DialogActions>
    </Dialog>
  )
}
