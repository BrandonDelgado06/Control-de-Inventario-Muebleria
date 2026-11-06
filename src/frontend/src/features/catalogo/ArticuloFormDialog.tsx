import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Divider, MenuItem, Stack, TextField, Typography,
} from '@mui/material'
import { api, mensajeError } from '../../api/cliente'
import type { ArticuloDetalle, EstadoArticulo } from '../../api/tipos'
import { useNotificar } from '../../components/Notificaciones'
import { nombreEstado, nombreTipoInventario } from '../../lib/formato'
import { useCategorias, useUbicaciones } from './ArticulosPage'

interface Props {
  detalle?: ArticuloDetalle
  onClose: () => void
  onGuardado?: (id: number) => void
}

/** RF-08 (registro) y RF-09 (edición). La existencia solo cambia con movimientos. */
export function ArticuloFormDialog({ detalle, onClose, onGuardado }: Props) {
  const a = detalle?.articulo
  const editando = !!a
  const qc = useQueryClient()
  const notificar = useNotificar()
  const categorias = useCategorias()
  const ubicaciones = useUbicaciones()
  const unidades = useQuery({ queryKey: ['unidades'], queryFn: async () => (await api.get<string[]>('/articulos/unidades')).data })

  const [codigo, setCodigo] = useState(a?.codigo ?? '')
  const [nombre, setNombre] = useState(a?.nombre ?? '')
  const [categoriaId, setCategoriaId] = useState<number | ''>(a?.categoriaId ?? '')
  const [ubicacionId, setUbicacionId] = useState<number | ''>(a?.ubicacionId ?? '')
  const [unidad, setUnidad] = useState(a?.unidadMedida ?? 'pieza')
  const [stockMinimo, setStockMinimo] = useState(String(a?.stockMinimo ?? 0))
  const [stockInicial, setStockInicial] = useState('0')
  const [estado, setEstado] = useState<EstadoArticulo>(a?.estado ?? 'Buena')
  const [lote, setLote] = useState(a?.lote ?? '')
  const [vence, setVence] = useState(a?.fechaVencimiento ?? '')
  const [presentacion, setPresentacion] = useState(detalle?.presentacion ?? '')
  const [alias, setAlias] = useState((detalle?.alias ?? []).join(', '))
  const [valores, setValores] = useState<Record<number, string>>(
    Object.fromEntries((detalle?.campos ?? []).filter((c) => c.valor).map((c) => [c.campoId, c.valor!])),
  )

  const categoria = categorias.data?.find((c) => c.id === categoriaId)
  const esAcabado = categoria?.tipoInventario === 'Acabado'

  const guardar = useMutation({
    mutationFn: async () => {
      const cuerpo = {
        codigo, nombre, categoriaId: categoriaId || 0, ubicacionId: ubicacionId || 0, unidadMedida: unidad,
        stockMinimo: Number(stockMinimo || 0), estado,
        lote: esAcabado ? lote : null, fechaVencimiento: esAcabado && vence ? vence : null, presentacion: presentacion || null,
        alias: alias.split(',').map((s) => s.trim()).filter(Boolean),
        valores: Object.fromEntries((categoria?.campos ?? []).map((c) => [c.id, valores[c.id] ?? ''])),
        stockInicial: editando ? null : Number(stockInicial || 0),
      }
      const r = editando
        ? await api.put<ArticuloDetalle>(`/articulos/${a!.id}`, cuerpo)
        : await api.post<ArticuloDetalle>('/articulos', cuerpo)
      return r.data
    },
    onSuccess: (d) => {
      qc.invalidateQueries({ queryKey: ['articulos'] })
      qc.invalidateQueries({ queryKey: ['articulo', d.articulo.id] })
      qc.invalidateQueries({ queryKey: ['panel'] })
      notificar(editando ? 'Artículo actualizado.' : `Artículo ${d.articulo.codigo} registrado.`)
      onClose()
      onGuardado?.(d.articulo.id)
    },
  })

  function enviar(e: FormEvent) {
    e.preventDefault()
    guardar.mutate()
  }

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="md" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>{editando ? `Editar ${a!.codigo}` : 'Nuevo artículo'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {guardar.isError && <Alert severity="error">{mensajeError(guardar.error)}</Alert>}
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 2fr' } }}>
            <TextField id="codigo" label="Código" required value={codigo} onChange={(e) => setCodigo(e.target.value)} helperText="Único. Ej.: LIJ-220" />
            <TextField id="nombre" label="Nombre" required value={nombre} onChange={(e) => setNombre(e.target.value)} />
          </Box>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
            <TextField id="categoria" select label="Categoría" required value={categoriaId} onChange={(e) => setCategoriaId(Number(e.target.value))}>
              {categorias.data?.map((c) => (
                <MenuItem key={c.id} value={c.id}>{c.nombre} <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 1 }}>{nombreTipoInventario[c.tipoInventario]}</Typography></MenuItem>
              ))}
            </TextField>
            <TextField id="ubicacion" select label="Ubicación" required value={ubicacionId} onChange={(e) => setUbicacionId(Number(e.target.value))}>
              {ubicaciones.data?.map((u) => <MenuItem key={u.id} value={u.id}>{u.nombre}</MenuItem>)}
            </TextField>
          </Box>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr 1fr', sm: 'repeat(4, 1fr)' } }}>
            <TextField id="unidad" select label="Unidad" value={unidad} onChange={(e) => setUnidad(e.target.value)}>
              {(unidades.data ?? [unidad]).map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
            </TextField>
            <TextField id="stock-minimo" label="Stock mínimo" type="number" value={stockMinimo} onChange={(e) => setStockMinimo(e.target.value)} slotProps={{ htmlInput: { min: 0, step: 'any' } }} />
            {!editando && (
              <TextField id="stock-inicial" label="Existencia inicial" type="number" value={stockInicial} onChange={(e) => setStockInicial(e.target.value)} slotProps={{ htmlInput: { min: 0, step: 'any' } }} helperText="Se registra como entrada" />
            )}
            <TextField id="estado" select label="Estado" value={estado} onChange={(e) => setEstado(e.target.value as EstadoArticulo)}>
              {(Object.keys(nombreEstado) as EstadoArticulo[]).map((k) => <MenuItem key={k} value={k}>{nombreEstado[k]}</MenuItem>)}
            </TextField>
          </Box>

          {esAcabado && (
            <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' } }}>
              <TextField id="presentacion" select label="Presentación" value={presentacion} onChange={(e) => setPresentacion(e.target.value)}>
                <MenuItem value="litro">Litro</MenuItem><MenuItem value="galón">Galón</MenuItem><MenuItem value="cuarto">Cuarto de galón</MenuItem>
              </TextField>
              <TextField id="lote" label="Lote" value={lote} onChange={(e) => setLote(e.target.value)} />
              <TextField id="vence" label="Fecha de vencimiento" type="date" required value={vence} onChange={(e) => setVence(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
            </Box>
          )}

          <TextField id="alias" label="Otros nombres" value={alias} onChange={(e) => setAlias(e.target.value)}
            helperText="Cómo le dicen en el taller, separados por coma. Sirven para encontrarlo al buscar." />

          {categoria && categoria.campos.length > 0 && (
            <>
              <Divider textAlign="left"><Typography variant="caption" color="text.secondary">Campos de {categoria.nombre}</Typography></Divider>
              <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
                {categoria.campos.map((c) => {
                  const comun = {
                    id: `campo-${c.id}`, label: c.nombre, required: c.obligatorio, value: valores[c.id] ?? '',
                    onChange: (e: { target: { value: string } }) => setValores((v) => ({ ...v, [c.id]: e.target.value })),
                  }
                  if (c.tipoDato === 'Lista') return (
                    <TextField key={c.id} select {...comun}>
                      {!c.obligatorio && <MenuItem value="">—</MenuItem>}
                      {c.opciones.map((o) => <MenuItem key={o} value={o}>{o}</MenuItem>)}
                    </TextField>
                  )
                  if (c.tipoDato === 'Fecha') return <TextField key={c.id} type="date" slotProps={{ inputLabel: { shrink: true } }} {...comun} />
                  if (c.tipoDato === 'Numero') return <TextField key={c.id} type="number" slotProps={{ htmlInput: { step: 'any' } }} {...comun} />
                  return <TextField key={c.id} {...comun} />
                })}
              </Box>
            </>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={guardar.isPending}>{guardar.isPending ? 'Guardando…' : 'Guardar'}</Button>
      </DialogActions>
    </Dialog>
  )
}
