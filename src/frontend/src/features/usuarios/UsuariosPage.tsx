import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Paper,
  Stack, Switch, Table, TableBody, TableCell, TableHead, TableRow, TextField,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { api, mensajeError } from '../../api/cliente'
import type { Rol, Usuario } from '../../api/tipos'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, Pagina } from '../../components/Pagina'

/** RF-02: el administrador crea, edita, activa y desactiva cuentas y asigna roles. */
export function UsuariosPage() {
  const [editando, setEditando] = useState<Usuario | 'nuevo' | null>(null)
  const usuarios = useQuery({ queryKey: ['usuarios'], queryFn: async () => (await api.get<Usuario[]>('/usuarios')).data })
  const roles = useQuery({ queryKey: ['roles'], queryFn: async () => (await api.get<Rol[]>('/usuarios/roles')).data })

  return (
    <Pagina
      titulo="Usuarios"
      subtitulo="Cada persona entra con su propio usuario y solo ve lo que su rol permite."
      acciones={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditando('nuevo')}>Nuevo usuario</Button>}
    >
      <Paper sx={{ minWidth: 0 }}>
        {usuarios.isLoading ? <Cargando /> : (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead><TableRow><TableCell>Nombre</TableCell><TableCell>Usuario</TableCell><TableCell>Rol</TableCell><TableCell>Estado</TableCell><TableCell /></TableRow></TableHead>
              <TableBody>
                {usuarios.data?.map((u) => (
                  <TableRow key={u.id} sx={{ opacity: u.activo ? 1 : 0.55 }}>
                    <TableCell sx={{ fontWeight: 500 }}>{u.nombre}</TableCell>
                    <TableCell sx={{ fontFamily: 'monospace' }}>{u.usuario}</TableCell>
                    <TableCell>{u.rol}</TableCell>
                    <TableCell><Chip size="small" variant="outlined" color={u.activo ? 'success' : 'default'} label={u.activo ? 'Activo' : 'Inactivo'} /></TableCell>
                    <TableCell align="right"><Button size="small" onClick={() => setEditando(u)}>Editar</Button></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </Paper>
      {editando && roles.data && (
        <UsuarioDialog usuario={editando === 'nuevo' ? undefined : editando} roles={roles.data} onClose={() => setEditando(null)} />
      )}
    </Pagina>
  )
}

function UsuarioDialog({ usuario, roles, onClose }: { usuario?: Usuario; roles: Rol[]; onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [nombre, setNombre] = useState(usuario?.nombre ?? '')
  const [login, setLogin] = useState(usuario?.usuario ?? '')
  const [rolId, setRolId] = useState<number | ''>(usuario?.rolId ?? '')
  const [activo, setActivo] = useState(usuario?.activo ?? true)
  const [contrasena, setContrasena] = useState('')

  const guardar = useMutation({
    mutationFn: async () => {
      if (usuario) await api.put(`/usuarios/${usuario.id}`, { nombre, rolId: rolId || 0, activo, nuevaContrasena: contrasena || null })
      else await api.post('/usuarios', { nombre, usuario: login, contrasena, rolId: rolId || 0 })
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['usuarios'] })
      notificar(usuario ? 'Usuario actualizado.' : 'Usuario creado.')
      onClose()
    },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); guardar.mutate() }

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>{usuario ? `Editar ${usuario.usuario}` : 'Nuevo usuario'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {guardar.isError && <Alert severity="error">{mensajeError(guardar.error)}</Alert>}
          <TextField id="nombre" label="Nombre completo" required value={nombre} onChange={(e) => setNombre(e.target.value)} />
          <TextField id="login" label="Usuario" required disabled={!!usuario} value={login} onChange={(e) => setLogin(e.target.value)}
            helperText={usuario ? 'El usuario no se puede cambiar.' : 'Letras, números, punto o guion. Ej.: jperez'} />
          <TextField id="rol" select label="Rol" required value={rolId} onChange={(e) => setRolId(Number(e.target.value))}>
            {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.nombre}</MenuItem>)}
          </TextField>
          <TextField id="contrasena" type="password" autoComplete="new-password" required={!usuario}
            label={usuario ? 'Nueva contraseña' : 'Contraseña'} value={contrasena} onChange={(e) => setContrasena(e.target.value)}
            helperText={usuario ? 'Déjala vacía para no cambiarla. Mínimo 8 caracteres.' : 'Mínimo 8 caracteres.'} />
          {usuario && <FormControlLabel control={<Switch id="activo" checked={activo} onChange={(e) => setActivo(e.target.checked)} />} label="Cuenta activa" />}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={guardar.isPending}>Guardar</Button>
      </DialogActions>
    </Dialog>
  )
}
