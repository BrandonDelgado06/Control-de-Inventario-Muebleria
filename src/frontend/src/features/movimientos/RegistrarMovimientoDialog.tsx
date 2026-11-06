import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Autocomplete, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField,
  ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type ArticuloResumen, type Movimiento, type Paginado, type TipoMovimiento } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { fmtNumero } from '../../lib/formato'

const motivosSugeridos: Record<string, string[]> = {
  Entrada: ['Compra', 'Devolución de taller', 'Donación'],
  Salida: ['Uso en taller', 'Mueble por encargo', 'Instalación'],
  Ajuste: ['Conteo físico', 'Corrección de registro'],
}

/** RF-19 (entrada), RF-20 (salida) y RF-21 (ajuste, solo administrador). */
export function RegistrarMovimientoDialog({ articulo: fijo, onClose }: { articulo?: ArticuloResumen; onClose: () => void }) {
  const { tiene } = useAuth()
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [articulo, setArticulo] = useState<ArticuloResumen | null>(fijo ?? null)
  const [busqueda, setBusqueda] = useState('')
  const [tipo, setTipo] = useState<TipoMovimiento>('Entrada')
  const [cantidad, setCantidad] = useState('')
  const [motivo, setMotivo] = useState('')

  const opciones = useQuery({
    queryKey: ['articulos', 'buscar', busqueda],
    queryFn: async () => (await api.get<Paginado<ArticuloResumen>>('/articulos', { params: { texto: busqueda, tamano: 15 } })).data.items,
    enabled: !fijo,
  })

  const n = Number(cantidad)
  const stock = articulo?.stockActual ?? 0
  const nuevo = !cantidad || Number.isNaN(n) ? null : tipo === 'Entrada' ? stock + n : tipo === 'Salida' ? stock - n : n
  const insuficiente = nuevo !== null && nuevo < 0

  const registrar = useMutation({
    mutationFn: async () => (await api.post<Movimiento>('/movimientos', { articuloId: articulo?.id ?? 0, tipo, cantidad: n, motivo })).data,
    onSuccess: (m) => {
      for (const k of [['articulos'], ['articulo', m.articuloId], ['movimientos'], ['panel']]) qc.invalidateQueries({ queryKey: k })
      notificar(`${m.tipo} registrada: ${m.articulo} queda en ${fmtNumero(m.stockNuevo)} ${m.unidadMedida}.`)
      onClose()
    },
  })

  function enviar(e: FormEvent) {
    e.preventDefault()
    registrar.mutate()
  }

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>Registrar movimiento</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2.5}>
          {registrar.isError && <Alert severity="error">{mensajeError(registrar.error)}</Alert>}
          {fijo ? (
            <Typography><b>{fijo.nombre}</b> <Typography component="span" color="text.secondary" sx={{ fontFamily: 'monospace' }}>{fijo.codigo}</Typography></Typography>
          ) : (
            <Autocomplete
              id="articulo"
              options={opciones.data ?? []}
              value={articulo}
              onChange={(_, v) => setArticulo(v)}
              onInputChange={(_, v) => setBusqueda(v)}
              getOptionLabel={(o) => `${o.codigo} · ${o.nombre}`}
              isOptionEqualToValue={(a, b) => a.id === b.id}
              filterOptions={(x) => x}
              loading={opciones.isFetching}
              noOptionsText="Sin resultados"
              renderInput={(p) => <TextField {...p} label="Artículo" required placeholder="Busca por código, nombre u otro nombre" />}
            />
          )}

          <ToggleButtonGroup exclusive fullWidth color="primary" value={tipo} onChange={(_, v) => v && setTipo(v)}>
            <ToggleButton value="Entrada">Entrada</ToggleButton>
            <ToggleButton value="Salida">Salida</ToggleButton>
            {tiene(Permisos.MovimientosAjustar) && <ToggleButton value="Ajuste">Ajuste</ToggleButton>}
          </ToggleButtonGroup>

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: '1fr 1fr' }}>
            <TextField id="cantidad" required type="number" label={tipo === 'Ajuste' ? 'Existencia correcta' : 'Cantidad'}
              value={cantidad} onChange={(e) => setCantidad(e.target.value)}
              slotProps={{ htmlInput: { min: 0, step: 'any' } }}
              helperText={articulo ? `Unidad: ${articulo.unidadMedida}` : ' '} />
            <Box sx={{ alignSelf: 'center' }}>
              {articulo && (
                <Typography color={insuficiente ? 'error' : 'text.secondary'} sx={{ fontVariantNumeric: 'tabular-nums' }}>
                  {fmtNumero(stock)} → <b>{nuevo === null ? '…' : fmtNumero(nuevo)}</b> {articulo.unidadMedida}
                  {insuficiente && <><br />No alcanza la existencia.</>}
                </Typography>
              )}
            </Box>
          </Box>

          <Autocomplete
            id="motivo"
            freeSolo
            options={motivosSugeridos[tipo] ?? []}
            inputValue={motivo}
            onInputChange={(_, v) => setMotivo(v)}
            renderInput={(p) => <TextField {...p} label="Motivo" required helperText={tipo === 'Ajuste' ? 'Obligatorio en los ajustes: explica por qué cambia la existencia.' : ' '} />}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={registrar.isPending || !articulo || !cantidad || !motivo.trim() || insuficiente}>
          {registrar.isPending ? 'Guardando…' : 'Registrar'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
