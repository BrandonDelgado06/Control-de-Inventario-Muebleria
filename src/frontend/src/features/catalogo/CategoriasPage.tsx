import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Divider, FormControlLabel,
  IconButton, MenuItem, Paper, Stack, TextField, Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DeleteOutlined from '@mui/icons-material/DeleteOutlined'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type Categoria, type TipoDato, type TipoInventario } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, Pagina, Vacio } from '../../components/Pagina'
import { nombreTipoInventario } from '../../lib/formato'
import { useCategorias } from './ArticulosPage'

const nombreTipoDato: Record<TipoDato, string> = { Texto: 'Texto', Numero: 'Número', Fecha: 'Fecha', Lista: 'Lista de opciones' }

/** RF-07: categorías con campos personalizados, sin tocar el código (RNF-08). */
export function CategoriasPage() {
  const { tiene } = useAuth()
  const puedeEditar = tiene(Permisos.CatalogosGestionar)
  const [editando, setEditando] = useState<Categoria | 'nueva' | null>(null)
  const { data, isLoading } = useCategorias()

  return (
    <Pagina
      titulo="Categorías"
      subtitulo="Cada categoría puede pedir sus propios datos: número de grano, espesor, especie de madera…"
      acciones={puedeEditar && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditando('nueva')}>Nueva categoría</Button>}
    >
      {isLoading ? <Cargando /> : !data?.length ? <Vacio>No hay categorías.</Vacio> : (
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
          {data.map((c) => (
            <Paper key={c.id} sx={{ p: 2, display: 'grid', gap: 1.5, alignContent: 'start' }}>
              <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start', gap: 1 }}>
                <Box>
                  <Typography variant="h2">{c.nombre}</Typography>
                  <Typography variant="body2" color="text.secondary">{nombreTipoInventario[c.tipoInventario]} · {c.cantidadArticulos} artículo(s)</Typography>
                </Box>
                {puedeEditar && <Button size="small" onClick={() => setEditando(c)}>Editar</Button>}
              </Stack>
              {c.campos.length === 0 ? (
                <Typography variant="body2" color="text.secondary">Sin campos adicionales.</Typography>
              ) : (
                <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 0.75 }}>
                  {c.campos.map((f) => (
                    <Chip key={f.id} size="small" variant="outlined" label={`${f.nombre}${f.obligatorio ? ' *' : ''}`} title={nombreTipoDato[f.tipoDato]} />
                  ))}
                </Stack>
              )}
            </Paper>
          ))}
        </Box>
      )}
      {editando && <CategoriaDialog categoria={editando === 'nueva' ? undefined : editando} onClose={() => setEditando(null)} />}
    </Pagina>
  )
}

interface CampoForm { clave: number; id?: number; nombre: string; tipoDato: TipoDato; obligatorio: boolean; opciones: string }

function CategoriaDialog({ categoria, onClose }: { categoria?: Categoria; onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [nombre, setNombre] = useState(categoria?.nombre ?? '')
  const [tipo, setTipo] = useState<TipoInventario>(categoria?.tipoInventario ?? 'MateriaPrima')
  const [campos, setCampos] = useState<CampoForm[]>(
    (categoria?.campos ?? []).map((c) => ({ clave: c.id, id: c.id, nombre: c.nombre, tipoDato: c.tipoDato, obligatorio: c.obligatorio, opciones: c.opciones.join(', ') })),
  )
  const cambiarCampo = (clave: number, cambio: Partial<CampoForm>) =>
    setCampos((cs) => cs.map((c) => (c.clave === clave ? { ...c, ...cambio } : c)))

  const guardar = useMutation({
    mutationFn: () => {
      const cuerpo = {
        nombre, tipoInventario: tipo,
        campos: campos.map((c) => ({
          id: c.id ?? null, nombre: c.nombre, tipoDato: c.tipoDato, obligatorio: c.obligatorio,
          opciones: c.tipoDato === 'Lista' ? c.opciones.split(',').map((o) => o.trim()).filter(Boolean) : null,
        })),
      }
      return categoria ? api.put(`/categorias/${categoria.id}`, cuerpo) : api.post('/categorias', cuerpo)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['categorias'] })
      notificar(categoria ? 'Categoría actualizada.' : 'Categoría creada.')
      onClose()
    },
  })
  const enviar = (e: FormEvent) => { e.preventDefault(); guardar.mutate() }

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="md" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>{categoria ? `Editar ${categoria.nombre}` : 'Nueva categoría'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {guardar.isError && <Alert severity="error">{mensajeError(guardar.error)}</Alert>}
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
            <TextField id="nombre" label="Nombre" required value={nombre} onChange={(e) => setNombre(e.target.value)} />
            <TextField id="tipo" select label="Módulo de inventario" value={tipo} onChange={(e) => setTipo(e.target.value as TipoInventario)}>
              {Object.entries(nombreTipoInventario).map(([k, v]) => <MenuItem key={k} value={k}>{v}</MenuItem>)}
            </TextField>
          </Box>

          <Divider textAlign="left"><Typography variant="caption" color="text.secondary">Campos personalizados</Typography></Divider>
          {campos.length === 0 && <Typography variant="body2" color="text.secondary">Esta categoría no pide datos adicionales.</Typography>}
          {campos.map((c) => (
            <Box key={c.clave} sx={{ display: 'grid', gap: 1.5, alignItems: 'center', gridTemplateColumns: { xs: '1fr auto', sm: '2fr 1.4fr auto auto' } }}>
              <TextField id={`campo-nombre-${c.clave}`} label="Nombre del campo" required value={c.nombre} onChange={(e) => cambiarCampo(c.clave, { nombre: e.target.value })} />
              <TextField id={`campo-tipo-${c.clave}`} select label="Tipo" value={c.tipoDato} onChange={(e) => cambiarCampo(c.clave, { tipoDato: e.target.value as TipoDato })}>
                {Object.entries(nombreTipoDato).map(([k, v]) => <MenuItem key={k} value={k}>{v}</MenuItem>)}
              </TextField>
              <FormControlLabel sx={{ m: 0 }} control={<Checkbox id={`campo-obl-${c.clave}`} checked={c.obligatorio} onChange={(e) => cambiarCampo(c.clave, { obligatorio: e.target.checked })} />} label="Obligatorio" />
              <IconButton aria-label="Quitar campo" onClick={() => setCampos((cs) => cs.filter((x) => x.clave !== c.clave))}><DeleteOutlined /></IconButton>
              {c.tipoDato === 'Lista' && (
                <TextField id={`campo-opciones-${c.clave}`} label="Opciones (separadas por coma)" required value={c.opciones}
                  onChange={(e) => cambiarCampo(c.clave, { opciones: e.target.value })} sx={{ gridColumn: '1 / -1' }} placeholder="60, 80, 120, 220" />
              )}
            </Box>
          ))}
          <Box>
            <Button startIcon={<AddIcon />} onClick={() => setCampos((cs) => [...cs, { clave: -Date.now(), nombre: '', tipoDato: 'Texto', obligatorio: false, opciones: '' }])}>
              Agregar campo
            </Button>
          </Box>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={guardar.isPending}>Guardar</Button>
      </DialogActions>
    </Dialog>
  )
}
