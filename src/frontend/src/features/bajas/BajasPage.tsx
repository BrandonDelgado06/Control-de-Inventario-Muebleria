import { useState, type FormEvent } from 'react'
import { Link as RouterLink } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Autocomplete, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Link, MenuItem, Paper, Stack, Table,
  TableBody, TableCell, TableHead, TablePagination, TableRow, TextField, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material'
import DeleteSweepOutlined from '@mui/icons-material/DeleteSweepOutlined'
import BuildCircleOutlined from '@mui/icons-material/BuildCircleOutlined'
import { api, mensajeError } from '../../api/cliente'
import type { ArticuloResumen, CausaMerma, Movimiento, Paginado } from '../../api/tipos'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, ChipEstado, Pagina, Vacio } from '../../components/Pagina'
import { diasHasta, fmtFecha, fmtFechaHora, fmtNumero } from '../../lib/formato'
import { ChipTipo } from '../movimientos/MovimientosPage'

const causas: { valor: CausaMerma; texto: string; ayuda: string }[] = [
  { valor: 'Desperdicio', texto: 'Desperdicio', ayuda: 'Recortes, sobrantes o material que se perdió al trabajar.' },
  { valor: 'Danio', texto: 'Daño', ayuda: 'Material golpeado, mojado o quebrado que ya no sirve.' },
  { valor: 'Vencimiento', texto: 'Vencimiento', ayuda: 'Productos de acabado que pasaron su fecha.' },
]

const esHerramienta = (a: ArticuloResumen) => a.tipoInventario === 'Herramienta' || a.tipoInventario === 'Instalacion'

/** RF-26 (merma) y RF-27 (baja de herramienta) en un solo diálogo: cambia según lo que se elija. */
function RegistrarBajaDialog({ modo, onClose }: { modo: 'merma' | 'herramienta'; onClose: () => void }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [articulo, setArticulo] = useState<ArticuloResumen | null>(null)
  const [busqueda, setBusqueda] = useState('')
  const [cantidad, setCantidad] = useState(modo === 'herramienta' ? '1' : '')
  const [causa, setCausa] = useState<CausaMerma>('Desperdicio')
  const [texto, setTexto] = useState('')

  const opciones = useQuery({
    queryKey: ['articulos', 'buscar', busqueda],
    queryFn: async () => (await api.get<Paginado<ArticuloResumen>>('/articulos', { params: { texto: busqueda, tamano: 25 } })).data.items,
  })
  // Merma: cualquier material (también lijas o brocas). Baja: solo herramientas.
  const lista = (opciones.data ?? []).filter((a) => modo === 'merma' || esHerramienta(a))

  const n = Number(cantidad)
  const queda = articulo && cantidad && !Number.isNaN(n) ? articulo.stockActual - n : null
  const insuficiente = queda !== null && queda < 0

  const guardar = useMutation({
    mutationFn: async () => (await api.post<Movimiento>(`/bajas/${modo}`, modo === 'merma'
      ? { articuloId: articulo?.id ?? 0, cantidad: n, causa, detalle: texto.trim() || null }
      : { articuloId: articulo?.id ?? 0, cantidad: n, motivo: texto })).data,
    onSuccess: (m) => {
      for (const k of [['bajas'], ['articulos'], ['articulo', m.articuloId], ['movimientos'], ['panel'], ['alertas']]) qc.invalidateQueries({ queryKey: k })
      notificar(modo === 'merma'
        ? `Merma registrada: ${m.articulo} queda en ${fmtNumero(m.stockNuevo)} ${m.unidadMedida}.`
        : m.stockNuevo === 0 ? `${m.articulo} quedó dada de baja.` : `Baja registrada: quedan ${fmtNumero(m.stockNuevo)} ${m.unidadMedida} de ${m.articulo}.`)
      onClose()
    },
  })

  function enviar(e: FormEvent) {
    e.preventDefault()
    guardar.mutate()
  }

  const vencido = !!articulo?.fechaVencimiento && diasHasta(articulo.fechaVencimiento) <= 0

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm" slotProps={{ paper: { component: 'form', onSubmit: enviar } as object }}>
      <DialogTitle>{modo === 'merma' ? 'Registrar merma' : 'Dar de baja una herramienta'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2.5}>
          {guardar.isError && <Alert severity="error">{mensajeError(guardar.error)}</Alert>}
          <Autocomplete
            id="articulo"
            options={lista}
            value={articulo}
            onChange={(_, v) => setArticulo(v)}
            onInputChange={(_, v) => setBusqueda(v)}
            getOptionLabel={(o) => `${o.codigo} · ${o.nombre}`}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            filterOptions={(x) => x}
            loading={opciones.isFetching}
            noOptionsText={modo === 'merma' ? 'Sin materiales con ese nombre' : 'Sin herramientas con ese nombre'}
            renderOption={({ key, ...p }, o) => (
              <li key={key} {...p}>
                <Box sx={{ flex: 1 }}>{o.codigo} · {o.nombre}</Box>
                <Typography variant="body2" color="text.secondary">{fmtNumero(o.stockActual)} {o.unidadMedida}</Typography>
              </li>
            )}
            renderInput={(p) => <TextField {...p} label={modo === 'merma' ? 'Material' : 'Herramienta'} required placeholder="Busca por código o nombre" />}
          />

          {articulo && (
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 1 }}>
              <Typography variant="body2" color="text.secondary">Existencia: <b>{fmtNumero(articulo.stockActual)} {articulo.unidadMedida}</b></Typography>
              {modo === 'herramienta' && <ChipEstado estado={articulo.estado} />}
              {articulo.fechaVencimiento && (
                <Typography variant="body2" color={vencido ? 'error' : 'text.secondary'}>Vence: {fmtFecha(articulo.fechaVencimiento)}</Typography>
              )}
            </Stack>
          )}

          {modo === 'merma' && (
            <Box>
              <ToggleButtonGroup exclusive fullWidth color="primary" value={causa} onChange={(_, v) => v && setCausa(v)}>
                {causas.map((c) => <ToggleButton key={c.valor} value={c.valor}>{c.texto}</ToggleButton>)}
              </ToggleButtonGroup>
              <Typography variant="caption" color="text.secondary">{causas.find((c) => c.valor === causa)?.ayuda}</Typography>
            </Box>
          )}

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: '1fr 1fr' }}>
            <TextField id="cantidad" required type="number" label={modo === 'merma' ? 'Cantidad perdida' : 'Unidades'}
              value={cantidad} onChange={(e) => setCantidad(e.target.value)}
              slotProps={{ htmlInput: { min: modo === 'merma' ? 0.01 : 1, step: modo === 'merma' ? 'any' : 1 } }}
              helperText={articulo ? `Unidad: ${articulo.unidadMedida}` : ' '} />
            <Box sx={{ alignSelf: 'center' }}>
              {queda !== null && (
                <Typography color={insuficiente ? 'error' : 'text.secondary'} sx={{ fontVariantNumeric: 'tabular-nums' }}>
                  Quedan <b>{fmtNumero(Math.max(queda, 0))}</b> {articulo?.unidadMedida}
                  {insuficiente && <><br />No alcanza la existencia.</>}
                  {modo === 'herramienta' && queda === 0 && <><br />La herramienta pasará a "De baja".</>}
                </Typography>
              )}
            </Box>
          </Box>

          <TextField id="motivo" label={modo === 'merma' ? 'Detalle (opcional)' : 'Motivo de la baja'} required={modo === 'herramienta'}
            value={texto} onChange={(e) => setTexto(e.target.value)} multiline minRows={2}
            slotProps={{ htmlInput: { maxLength: modo === 'merma' ? 110 : 130 } }}
            placeholder={modo === 'merma' ? 'Ej.: se mojó en la bodega' : 'Ej.: motor quemado, no tiene reparación'} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button type="submit" variant="contained" color="error"
          disabled={guardar.isPending || !articulo || !cantidad || insuficiente || (modo === 'herramienta' && !texto.trim())}>
          {guardar.isPending ? 'Guardando…' : modo === 'merma' ? 'Registrar merma' : 'Dar de baja'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}

/** RF-26 y RF-27: material perdido y herramientas que ya no sirven, con su historial. */
export function BajasPage() {
  const [dialogo, setDialogo] = useState<'merma' | 'herramienta' | null>(null)
  const [tipo, setTipo] = useState<'' | 'Merma' | 'Baja'>('')
  const [pagina, setPagina] = useState(0)
  const [tamano, setTamano] = useState(25)
  const params = { tipo: tipo || undefined, pagina: pagina + 1, tamano }
  const { data, isLoading } = useQuery({
    queryKey: ['bajas', params],
    queryFn: async () => (await api.get<Paginado<Movimiento>>('/bajas', { params })).data,
    placeholderData: keepPreviousData,
  })

  return (
    <Pagina
      titulo="Bajas y mermas"
      subtitulo="Material que se perdió (desperdicio, daño o vencimiento) y herramientas que ya no se pueden usar."
      acciones={<>
        <Button variant="contained" color="error" startIcon={<DeleteSweepOutlined />} onClick={() => setDialogo('merma')}>Registrar merma</Button>
        <Button variant="outlined" color="error" startIcon={<BuildCircleOutlined />} onClick={() => setDialogo('herramienta')}>Dar de baja herramienta</Button>
      </>}
    >
      <Paper sx={{ p: 2 }}>
        <TextField id="filtro-tipo" select label="Mostrar" value={tipo} onChange={(e) => { setTipo(e.target.value as typeof tipo); setPagina(0) }} sx={{ minWidth: 240 }}
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}>
          <MenuItem value="">Bajas y mermas</MenuItem>
          <MenuItem value="Merma">Solo mermas</MenuItem>
          <MenuItem value="Baja">Solo bajas de herramientas</MenuItem>
        </TextField>
      </Paper>

      <Paper sx={{ minWidth: 0 }}>
        {isLoading ? <Cargando /> : !data?.items.length ? <Vacio>Todavía no se han registrado bajas ni mermas.</Vacio> : (
          <>
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Fecha</TableCell><TableCell>Tipo</TableCell><TableCell>Artículo</TableCell>
                    <TableCell align="right">Cantidad</TableCell><TableCell align="right">Existencia</TableCell>
                    <TableCell>Motivo</TableCell><TableCell>Responsable</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.items.map((m) => (
                    <TableRow key={m.id}>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(m.fechaHora)}</TableCell>
                      <TableCell><ChipTipo tipo={m.tipo} /></TableCell>
                      <TableCell><Link component={RouterLink} to={`/articulos/${m.articuloId}`} underline="hover">{m.articulo}</Link></TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>−{fmtNumero(m.cantidad)} {m.unidadMedida}</TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap', color: 'text.secondary' }}>
                        {fmtNumero(m.stockAnterior)} → <b style={{ color: 'inherit' }}>{fmtNumero(m.stockNuevo)}</b>
                      </TableCell>
                      <TableCell>{m.motivo}</TableCell>
                      <TableCell>{m.usuario}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
            <TablePagination
              component="div" count={data.total} page={pagina} rowsPerPage={tamano}
              onPageChange={(_, p) => setPagina(p)}
              onRowsPerPageChange={(e) => { setTamano(Number(e.target.value)); setPagina(0) }}
              rowsPerPageOptions={[10, 25, 50]}
              labelRowsPerPage="Filas por página"
              labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
            />
          </>
        )}
      </Paper>

      {dialogo && <RegistrarBajaDialog modo={dialogo} onClose={() => setDialogo(null)} />}
    </Pagina>
  )
}
