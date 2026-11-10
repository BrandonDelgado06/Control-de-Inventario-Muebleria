import type { ReactNode } from 'react'
import { Link as RouterLink } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { alpha, Box, Button, Link, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography, useTheme } from '@mui/material'
import { BarChart } from '@mui/x-charts/BarChart'
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined'
import WarningAmberOutlined from '@mui/icons-material/WarningAmberOutlined'
import HandymanOutlined from '@mui/icons-material/HandymanOutlined'
import AssignmentOutlined from '@mui/icons-material/AssignmentOutlined'
import { api } from '../../api/cliente'
import { Permisos, type Resumen } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { Cargando, ChipStock, Pagina, Vacio } from '../../components/Pagina'
import { fmtFechaHora, fmtNumero } from '../../lib/formato'
import { ChipTipo } from '../movimientos/MovimientosPage'

function saludo() {
  const h = new Date().getHours()
  return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches'
}

/** RF-36: indicador con su ícono; el borde de color avisa que hay algo que atender. */
function Indicador({ valor, texto, detalle, icono, tono, a }: {
  valor: number; texto: string; detalle?: string; icono: ReactNode; tono?: 'warning' | 'error'; a?: string
}) {
  const contenido = (
    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start' }}>
      <Box sx={{ p: 1, borderRadius: 1.5, display: 'flex', bgcolor: (t) => alpha(tono ? t.palette[tono].main : t.palette.primary.main, 0.15), color: tono ? `${tono}.main` : 'primary.main' }}>{icono}</Box>
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontSize: '1.9rem', fontWeight: 700, fontVariantNumeric: 'tabular-nums', lineHeight: 1.1 }}>{fmtNumero(valor)}</Typography>
        <Typography color="text.secondary" sx={{ lineHeight: 1.3 }}>{texto}</Typography>
        {detalle && <Typography variant="caption" color={tono ? `${tono}.main` : 'text.secondary'}>{detalle}</Typography>}
      </Box>
    </Stack>
  )
  return (
    <Paper sx={{ p: 2, minWidth: 0, borderLeft: 4, borderLeftColor: tono ? `${tono}.main` : 'primary.main', textDecoration: 'none', color: 'inherit', '&:hover': a ? { boxShadow: 2 } : {} }}
      component={a ? RouterLink : 'div'} {...(a ? { to: a } : {})}>
      {contenido}
    </Paper>
  )
}

export function InicioPage() {
  const { sesion, tiene } = useAuth()
  const theme = useTheme()
  const { data, isLoading } = useQuery({ queryKey: ['panel'], queryFn: async () => (await api.get<Resumen>('/panel')).data })
  const fecha = new Intl.DateTimeFormat('es-CR', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date())

  return (
    <Pagina
      titulo={`${saludo()}, ${sesion?.nombre.split(' ')[0]}`}
      subtitulo={<span style={{ textTransform: 'capitalize' }}>{fecha}</span>}
      acciones={tiene(Permisos.SolicitudesCrear) && !tiene(Permisos.SolicitudesAprobar) && (
        <Button variant="contained" component={RouterLink} to="/solicitudes">Pedir material</Button>
      )}
    >
      {isLoading || !data ? <Cargando /> : (
        <>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(4, 1fr)' } }}>
            <Indicador valor={data.totalArticulos} texto="artículos registrados" detalle={`${data.movimientosHoy} movimientos hoy`} icono={<Inventory2Outlined />} a="/articulos" />
            <Indicador valor={data.alertasActivas} texto="alertas activas" icono={<WarningAmberOutlined />}
              detalle={[data.bajoMinimo && `${data.bajoMinimo} bajo mínimo`, data.porVencer && `${data.porVencer} por vencer`, data.herramientasAtrasadas && `${data.herramientasAtrasadas} sin devolver`].filter(Boolean).join(' · ') || 'Todo en orden'}
              tono={data.alertasActivas ? 'warning' : undefined} a={tiene(Permisos.AlertasVer) ? '/alertas' : undefined} />
            <Indicador valor={data.herramientasFuera} texto="herramientas fuera del taller" icono={<HandymanOutlined />}
              detalle={data.herramientasAtrasadas ? `${data.herramientasAtrasadas} con retraso` : undefined}
              tono={data.herramientasAtrasadas ? 'error' : undefined} a={tiene(Permisos.HerramientasPrestar) ? '/instalacion' : undefined} />
            <Indicador valor={data.solicitudesPendientes} texto="solicitudes pendientes" icono={<AssignmentOutlined />}
              tono={data.solicitudesPendientes ? 'warning' : undefined} a={tiene(Permisos.SolicitudesCrear) ? '/solicitudes' : undefined} />
          </Box>

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', lg: '3fr 2fr' } }}>
            <Paper sx={{ minWidth: 0, p: 2 }}>
              <Typography variant="h2">Consumo de materiales</Typography>
              <Typography variant="body2" color="text.secondary">Salidas por categoría en los últimos 30 días (RF-37).</Typography>
              {data.consumoPorCategoria.length === 0 ? <Vacio>Todavía no hay salidas en los últimos 30 días.</Vacio> : (
                <BarChart
                  key={theme.palette.mode}   // el gráfico guarda sus colores al montarse: se redibuja al cambiar de modo
                  height={260}
                  dataset={data.consumoPorCategoria}
                  xAxis={[{ scaleType: 'band', dataKey: 'categoria', tickLabelStyle: { fontSize: 12 }, categoryGapRatio: data.consumoPorCategoria.length < 4 ? 0.7 : 0.3 }]}
                  yAxis={[{ tickMinStep: 1 }]}
                  series={[{ dataKey: 'salidas', label: 'Salidas', color: theme.palette.primary.main }]}
                  hideLegend
                  margin={{ left: 0, right: 10, top: 20, bottom: 10 }}
                  borderRadius={6}
                />
              )}
            </Paper>

            <Paper sx={{ minWidth: 0 }}>
              <Stack direction="row" sx={{ p: 2, pb: 1, justifyContent: 'space-between', alignItems: 'center' }}>
                <Typography variant="h2">Hay que reponer</Typography>
                {tiene(Permisos.AlertasVer) && <Button size="small" component={RouterLink} to="/alertas">Ver alertas</Button>}
              </Stack>
              {data.articulosBajoMinimo.length === 0 ? <Vacio>Todos los artículos están sobre su mínimo.</Vacio> : (
                <Table size="small">
                  <TableBody>
                    {data.articulosBajoMinimo.map((a) => (
                      <TableRow key={a.id} hover>
                        <TableCell><Link component={RouterLink} to={`/articulos/${a.id}`} underline="hover">{a.nombre}</Link></TableCell>
                        <TableCell align="right"><ChipStock actual={a.stockActual} minimo={a.stockMinimo} unidad={a.unidadMedida} /></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Paper>
          </Box>

          <Paper sx={{ minWidth: 0 }}>
            <Stack direction="row" sx={{ p: 2, pb: 1, justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography variant="h2">Últimos movimientos</Typography>
              <Button size="small" component={RouterLink} to="/movimientos">Ver todos</Button>
            </Stack>
            {data.ultimosMovimientos.length === 0 ? <Vacio>Todavía no hay movimientos.</Vacio> : (
              <Box sx={{ overflowX: 'auto' }}>
                <Table size="small">
                  <TableHead><TableRow><TableCell>Fecha</TableCell><TableCell>Tipo</TableCell><TableCell>Artículo</TableCell><TableCell align="right">Cantidad</TableCell><TableCell>Motivo</TableCell><TableCell>Responsable</TableCell></TableRow></TableHead>
                  <TableBody>
                    {data.ultimosMovimientos.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(m.fechaHora)}</TableCell>
                        <TableCell><ChipTipo tipo={m.tipo} /></TableCell>
                        <TableCell>{m.articulo}</TableCell>
                        <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>{fmtNumero(m.cantidad)} {m.unidadMedida}</TableCell>
                        <TableCell>{m.motivo}</TableCell>
                        <TableCell>{m.usuario}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </>
      )}
    </Pagina>
  )
}
