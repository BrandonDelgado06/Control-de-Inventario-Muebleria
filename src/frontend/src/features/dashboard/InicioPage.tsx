import { Link as RouterLink } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Box, Link, Paper, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'
import { api } from '../../api/cliente'
import type { Resumen } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { Cargando, ChipStock, Pagina, Vacio } from '../../components/Pagina'
import { fmtFechaHora, fmtNumero, nombreTipoMovimiento } from '../../lib/formato'
import { ChipTipo } from '../movimientos/MovimientosPage'

function Indicador({ valor, texto, tono }: { valor: number; texto: string; tono?: 'warning' | 'error' }) {
  return (
    <Paper sx={{ p: 2, minWidth: 0, borderLeft: tono ? 4 : 1, borderLeftColor: tono ? `${tono}.main` : 'divider' }}>
      <Typography sx={{ fontSize: '2rem', fontWeight: 700, fontVariantNumeric: 'tabular-nums', lineHeight: 1.1 }}>{fmtNumero(valor)}</Typography>
      <Typography color="text.secondary">{texto}</Typography>
    </Paper>
  )
}

export function InicioPage() {
  const { sesion } = useAuth()
  const { data, isLoading } = useQuery({
    queryKey: ['panel'],
    queryFn: async () => (await api.get<Resumen>('/panel')).data,
  })

  return (
    <Pagina titulo={`Hola, ${sesion?.nombre.split(' ')[0]}`} subtitulo="Resumen del inventario del taller.">
      {isLoading || !data ? <Cargando /> : (
        <>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' } }}>
            <Indicador valor={data.totalArticulos} texto="artículos registrados" />
            <Indicador valor={data.bajoMinimo} texto="en o bajo el mínimo" tono={data.bajoMinimo > 0 ? 'warning' : undefined} />
            <Indicador valor={data.porVencer} texto="productos por vencer" tono={data.porVencer > 0 ? 'error' : undefined} />
            <Indicador valor={data.movimientosHoy} texto="movimientos hoy" />
          </Box>

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' } }}>
            <Paper sx={{ minWidth: 0 }}>
              <Typography variant="h2" sx={{ p: 2, pb: 1 }}>Hay que reponer</Typography>
              {data.articulosBajoMinimo.length === 0 ? <Vacio>Todos los artículos están sobre su mínimo.</Vacio> : (
                <Box sx={{ overflowX: 'auto' }}>
                  <Table size="small">
                    <TableHead><TableRow><TableCell>Artículo</TableCell><TableCell>Existencia</TableCell><TableCell>Mínimo</TableCell></TableRow></TableHead>
                    <TableBody>
                      {data.articulosBajoMinimo.map((a) => (
                        <TableRow key={a.id} hover>
                          <TableCell><Link component={RouterLink} to={`/articulos/${a.id}`} underline="hover">{a.nombre}</Link></TableCell>
                          <TableCell><ChipStock actual={a.stockActual} minimo={a.stockMinimo} unidad={a.unidadMedida} /></TableCell>
                          <TableCell sx={{ fontVariantNumeric: 'tabular-nums' }}>{fmtNumero(a.stockMinimo)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
            </Paper>

            <Paper sx={{ minWidth: 0 }}>
              <Typography variant="h2" sx={{ p: 2, pb: 1 }}>Últimos movimientos</Typography>
              {data.ultimosMovimientos.length === 0 ? <Vacio>Todavía no hay movimientos.</Vacio> : (
                <Box sx={{ overflowX: 'auto' }}>
                  <Table size="small">
                    <TableHead><TableRow><TableCell>Fecha</TableCell><TableCell>Tipo</TableCell><TableCell>Artículo</TableCell><TableCell align="right">Cantidad</TableCell></TableRow></TableHead>
                    <TableBody>
                      {data.ultimosMovimientos.map((m) => (
                        <TableRow key={m.id}>
                          <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(m.fechaHora)}</TableCell>
                          <TableCell><ChipTipo tipo={m.tipo} /></TableCell>
                          <TableCell>{m.articulo}</TableCell>
                          <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }} title={nombreTipoMovimiento[m.tipo]}>
                            {fmtNumero(m.cantidad)} {m.unidadMedida}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
            </Paper>
          </Box>
        </>
      )}
    </Pagina>
  )
}
