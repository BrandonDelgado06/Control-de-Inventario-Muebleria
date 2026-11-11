import { useState } from 'react'
import { Link as RouterLink } from 'react-router'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  Box, Button, Chip, Link, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TablePagination, TableRow, TextField,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { api } from '../../api/cliente'
import { Permisos, type Movimiento, type Paginado, type TipoMovimiento } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { Cargando, Pagina, Vacio } from '../../components/Pagina'
import { fmtFechaHora, fmtNumero, nombreTipoMovimiento } from '../../lib/formato'
import { RegistrarMovimientoDialog } from './RegistrarMovimientoDialog'

const colorTipo: Record<TipoMovimiento, 'success' | 'warning' | 'info' | 'error'> = {
  Entrada: 'success', Salida: 'warning', Ajuste: 'info', Baja: 'error', Merma: 'error',
}

export function ChipTipo({ tipo }: { tipo: TipoMovimiento }) {
  return <Chip size="small" variant="outlined" color={colorTipo[tipo]} label={nombreTipoMovimiento[tipo]} />
}

interface Filtros { articuloId?: number; tipo?: TipoMovimiento | ''; desde?: string; hasta?: string }

/** RF-22: historial de movimientos. Se reutiliza en el detalle de cada artículo. */
export function HistorialMovimientos({ articuloId, tipo, desde, hasta }: Filtros) {
  const [pagina, setPagina] = useState(0)
  const [tamano, setTamano] = useState(25)
  const params = { articuloId, tipo: tipo || undefined, desde: desde || undefined, hasta: hasta || undefined, pagina: pagina + 1, tamano }
  const { data, isLoading } = useQuery({
    queryKey: ['movimientos', params],
    queryFn: async () => (await api.get<Paginado<Movimiento>>('/movimientos', { params })).data,
    placeholderData: keepPreviousData,
  })

  return (
    <Paper sx={{ minWidth: 0 }}>
      {isLoading ? <Cargando /> : !data?.items.length ? <Vacio>No hay movimientos registrados.</Vacio> : (
        <>
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Fecha</TableCell><TableCell>Tipo</TableCell>
                  {!articuloId && <TableCell>Artículo</TableCell>}
                  <TableCell align="right">Cantidad</TableCell><TableCell align="right">Existencia</TableCell>
                  <TableCell>Motivo</TableCell><TableCell>Responsable</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.items.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(m.fechaHora)}</TableCell>
                    <TableCell><ChipTipo tipo={m.tipo} /></TableCell>
                    {!articuloId && (
                      <TableCell><Link component={RouterLink} to={`/articulos/${m.articuloId}`} underline="hover">{m.articulo}</Link></TableCell>
                    )}
                    <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                      {m.tipo === 'Entrada' ? '+' : m.tipo === 'Ajuste' ? '±' : '−'}{fmtNumero(m.cantidad)} {m.unidadMedida}
                    </TableCell>
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
  )
}

export function MovimientosPage() {
  const { tiene } = useAuth()
  const [tipo, setTipo] = useState<TipoMovimiento | ''>('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [nuevo, setNuevo] = useState(false)

  return (
    <Pagina
      titulo="Entradas y salidas"
      subtitulo="Cada movimiento actualiza la existencia del artículo y queda registrado con su responsable."
      acciones={tiene(Permisos.MovimientosRegistrar) && (
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setNuevo(true)}>Registrar movimiento</Button>
      )}
    >
      <Paper sx={{ p: 2 }}>
        <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' } }}>
          <TextField id="filtro-tipo" select label="Tipo" value={tipo} onChange={(e) => setTipo(e.target.value as TipoMovimiento | '')}>
            <MenuItem value="">Todos</MenuItem>
            {(['Entrada', 'Salida', 'Ajuste'] as const).map((t) => <MenuItem key={t} value={t}>{nombreTipoMovimiento[t]}</MenuItem>)}
          </TextField>
          <TextField id="desde" type="date" label="Desde" value={desde} onChange={(e) => setDesde(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField id="hasta" type="date" label="Hasta" value={hasta} onChange={(e) => setHasta(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
        </Box>
      </Paper>
      <HistorialMovimientos tipo={tipo} desde={desde} hasta={hasta} />
      {nuevo && <RegistrarMovimientoDialog onClose={() => setNuevo(false)} />}
    </Pagina>
  )
}
