import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import {
  Box, Chip, InputAdornment, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import SearchIcon from '@mui/icons-material/Search'
import { api } from '../../api/cliente'
import type { AcabadoAgrupado, LoteAcabado } from '../../api/tipos'
import { Cargando, ChipStock, Pagina, Vacio } from '../../components/Pagina'
import { fmtFecha, fmtNumero } from '../../lib/formato'

function Vence({ l }: { l: LoteAcabado }) {
  if (l.diasParaVencer === null) return <>—</>
  const d = l.diasParaVencer
  const color = d < 0 ? 'error' : d <= 30 ? 'warning' : 'default'
  const texto = d < 0 ? `Venció hace ${-d} d` : d === 0 ? 'Vence hoy' : `Faltan ${d} d`
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', whiteSpace: 'nowrap' }}>
      <span>{fmtFecha(l.fechaVencimiento)}</span>
      <Chip size="small" variant={color === 'default' ? 'outlined' : 'filled'} color={color} label={texto} />
    </Stack>
  )
}

/** RF-13: existencias de productos de acabado por producto y lote; el lote que vence primero va arriba. */
export function AcabadosPage() {
  const navegar = useNavigate()
  const [texto, setTexto] = useState('')
  const { data, isLoading } = useQuery({
    queryKey: ['acabados', texto],
    queryFn: async () => (await api.get<AcabadoAgrupado[]>('/articulos/acabados', { params: { texto: texto || undefined } })).data,
  })

  return (
    <Pagina titulo="Acabados por lote" subtitulo="Lacas, tintes, selladores y pegamentos agrupados por producto. Use primero el lote que vence antes.">
      <Paper sx={{ p: 2 }}>
        <TextField id="buscar" fullWidth placeholder="Buscar por producto, código o lote…" value={texto} onChange={(e) => setTexto(e.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }} />
      </Paper>

      {isLoading ? <Cargando /> : !data?.length ? <Paper><Vacio>No hay productos de acabado con ese criterio.</Vacio></Paper> : data.map((g) => (
        <Paper key={`${g.producto}-${g.unidadMedida}`} sx={{ minWidth: 0 }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ p: 2, pb: 1, justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
            <Box>
              <Typography variant="h2">{g.producto}</Typography>
              <Typography variant="body2" color="text.secondary">
                {g.lotes.length === 1 ? '1 lote' : `${g.lotes.length} lotes`} · total {fmtNumero(g.existenciaTotal)} {g.unidadMedida}
              </Typography>
            </Box>
            <Stack direction="row" spacing={1}>
              {g.lotesVencidos > 0 && <Chip size="small" color="error" label={g.lotesVencidos === 1 ? '1 vencido' : `${g.lotesVencidos} vencidos`} />}
              {g.lotesPorVencer > 0 && <Chip size="small" color="warning" label={g.lotesPorVencer === 1 ? '1 por vencer' : `${g.lotesPorVencer} por vencer`} />}
            </Stack>
          </Stack>
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Lote</TableCell><TableCell>Código</TableCell><TableCell>Presentación</TableCell><TableCell>Ubicación</TableCell>
                  <TableCell>Existencia</TableCell><TableCell>Vencimiento</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {g.lotes.map((l, i) => (
                  <TableRow key={l.articuloId} hover sx={{ cursor: 'pointer' }} onClick={() => navegar(`/articulos/${l.articuloId}`)}>
                    <TableCell sx={{ fontWeight: i === 0 && l.existencia > 0 ? 600 : undefined, whiteSpace: 'nowrap' }}>
                      {l.lote ?? 'Sin lote'}
                      {i === 0 && g.lotes.length > 1 && l.existencia > 0 && <Typography component="span" variant="caption" color="primary.main"> · usar primero</Typography>}
                    </TableCell>
                    <TableCell sx={{ fontFamily: 'monospace', whiteSpace: 'nowrap' }}>{l.codigo}</TableCell>
                    <TableCell>{l.presentacion ?? '—'}</TableCell>
                    <TableCell>{l.ubicacion}</TableCell>
                    <TableCell><ChipStock actual={l.existencia} minimo={l.stockMinimo} unidad={g.unidadMedida} /></TableCell>
                    <TableCell><Vence l={l} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        </Paper>
      ))}
    </Pagina>
  )
}
