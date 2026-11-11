import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Autocomplete, Box, Button, Card, CardActionArea, CardContent, Chip, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableHead,
  TableRow, TextField, Typography,
} from '@mui/material'
import PictureAsPdfOutlined from '@mui/icons-material/PictureAsPdfOutlined'
import GridOnOutlined from '@mui/icons-material/GridOnOutlined'
import { api, mensajeError } from '../../api/cliente'
import type { ArticuloResumen, Paginado, Reporte, TipoColumna, TipoReporte } from '../../api/tipos'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, Pagina, Vacio } from '../../components/Pagina'
import { descargar } from '../../lib/descargar'
import { fmtFecha, fmtFechaHora, fmtNumero, nombreTipoMovimiento } from '../../lib/formato'
import { useCategorias, useUbicaciones } from '../catalogo/ArticulosPage'

const hoy = () => new Date().toISOString().slice(0, 10)
const haceDias = (n: number) => new Date(Date.now() - n * 86_400_000).toISOString().slice(0, 10)

function celda(v: string | number | null, tipo: TipoColumna) {
  if (v === null || v === undefined) return '—'
  if (tipo === 'Numero') return fmtNumero(Number(v))
  if (tipo === 'Fecha') return fmtFecha(String(v))
  if (tipo === 'FechaHora') return fmtFechaHora(String(v))
  return String(v)
}

/** RF-33 a RF-35: ver el reporte en pantalla y exportarlo a PDF o Excel. */
export function ReportesPage() {
  const notificar = useNotificar()
  const tipos = useQuery({ queryKey: ['reportes'], queryFn: async () => (await api.get<TipoReporte[]>('/reportes')).data })
  const categorias = useCategorias()
  const ubicaciones = useUbicaciones()
  const [clave, setClave] = useState('existencias')
  const [desde, setDesde] = useState(haceDias(29))
  const [hasta, setHasta] = useState(hoy())
  const [categoriaId, setCategoriaId] = useState<number | ''>('')
  const [ubicacionId, setUbicacionId] = useState<number | ''>('')
  const [tipo, setTipo] = useState('')
  const [articulo, setArticulo] = useState<ArticuloResumen | null>(null)
  const [busqueda, setBusqueda] = useState('')
  const [usuarioId, setUsuarioId] = useState<number | ''>('')
  const [exportando, setExportando] = useState<string | null>(null)

  const actual = tipos.data?.find((t) => t.clave === clave)
  const usa = (f: string) => actual?.filtros.includes(f) ?? false
  const articulos = useQuery({
    queryKey: ['articulos', 'buscar', busqueda, 'inactivos'],
    queryFn: async () => (await api.get<Paginado<ArticuloResumen>>('/articulos', { params: { texto: busqueda, tamano: 15, incluirInactivos: true } })).data.items,
    enabled: usa('articulo'),
  })
  const responsables = useQuery({
    queryKey: ['reportes', 'responsables'],
    queryFn: async () => (await api.get<{ id: number; nombre: string }[]>('/reportes/responsables')).data,
    enabled: usa('usuario'),
  })
  const params = Object.fromEntries(Object.entries({
    desde: usa('periodo') ? desde : undefined, hasta: usa('periodo') ? hasta : undefined,
    categoriaId: usa('categoria') ? categoriaId : undefined, ubicacionId: usa('ubicacion') ? ubicacionId : undefined,
    tipo: usa('tipo') ? tipo : undefined,
    articuloId: usa('articulo') ? articulo?.id : undefined,
    usuarioId: usa('usuario') ? usuarioId : undefined,
  }).filter(([, v]) => v !== undefined && v !== ''))

  const reporte = useQuery({
    queryKey: ['reporte', clave, params],
    queryFn: async () => (await api.get<Reporte>(`/reportes/${clave}`, { params })).data,
    enabled: !!actual,
  })

  async function exportar(formato: 'pdf' | 'xlsx') {
    setExportando(formato)
    try { await descargar(`/reportes/${clave}/exportar`, { ...params, formato }); notificar(`Reporte descargado en ${formato === 'pdf' ? 'PDF' : 'Excel'}.`) }
    catch (e) { notificar(mensajeError(e), 'error') }
    finally { setExportando(null) }
  }

  return (
    <Pagina titulo="Reportes" subtitulo="Elige un reporte, ajusta los filtros y expórtalo a PDF o Excel para imprimirlo o compartirlo.">
      <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' } }}>
        {tipos.data?.map((t) => (
          <Card key={t.clave} variant="outlined" sx={{ borderColor: t.clave === clave ? 'primary.main' : 'divider', borderWidth: t.clave === clave ? 2 : 1 }}>
            <CardActionArea onClick={() => setClave(t.clave)} sx={{ height: '100%' }}>
              <CardContent sx={{ p: 1.5 }}>
                <Stack direction="row" sx={{ justifyContent: 'space-between', gap: 1 }}>
                  <Typography sx={{ fontWeight: 600 }}>{t.titulo}</Typography>
                  <Chip size="small" variant="outlined" label={t.requerimiento} />
                </Stack>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>{t.descripcion}</Typography>
              </CardContent>
            </CardActionArea>
          </Card>
        ))}
      </Box>

      {actual && (
        <Paper sx={{ p: 2 }}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ alignItems: { md: 'center' } }}>
            <Box sx={{ display: 'grid', gap: 1.5, flex: 1, gridTemplateColumns: { xs: '1fr', sm: 'repeat(auto-fit, minmax(160px, 1fr))' } }}>
              {usa('periodo') && <>
                <TextField id="desde" type="date" label="Desde" value={desde} onChange={(e) => setDesde(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
                <TextField id="hasta" type="date" label="Hasta" value={hasta} onChange={(e) => setHasta(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
              </>}
              {usa('categoria') && (
                <TextField id="categoria" select label="Categoría" value={categoriaId} onChange={(e) => setCategoriaId(e.target.value === '' ? '' : Number(e.target.value))}>
                  <MenuItem value="">Todas</MenuItem>
                  {categorias.data?.map((c) => <MenuItem key={c.id} value={c.id}>{c.nombre}</MenuItem>)}
                </TextField>
              )}
              {usa('ubicacion') && (
                <TextField id="ubicacion" select label="Ubicación" value={ubicacionId} onChange={(e) => setUbicacionId(e.target.value === '' ? '' : Number(e.target.value))}>
                  <MenuItem value="">Todas</MenuItem>
                  {ubicaciones.data?.map((u) => <MenuItem key={u.id} value={u.id}>{u.nombre}</MenuItem>)}
                </TextField>
              )}
              {usa('tipo') && (
                <TextField id="tipo" select label="Tipo de movimiento" value={tipo} onChange={(e) => setTipo(e.target.value)}>
                  <MenuItem value="">Todos</MenuItem>
                  {Object.entries(nombreTipoMovimiento).map(([k, v]) => <MenuItem key={k} value={k}>{v}</MenuItem>)}
                </TextField>
              )}
              {usa('articulo') && (
                <Autocomplete
                  id="articulo"
                  options={articulos.data ?? []}
                  value={articulo}
                  onChange={(_, v) => setArticulo(v)}
                  onInputChange={(_, v) => setBusqueda(v)}
                  getOptionLabel={(o) => `${o.codigo} · ${o.nombre}`}
                  isOptionEqualToValue={(a, b) => a.id === b.id}
                  filterOptions={(x) => x}
                  loading={articulos.isFetching}
                  noOptionsText="Sin resultados"
                  renderInput={(p) => <TextField {...p} label="Artículo" placeholder="Todos" />}
                />
              )}
              {usa('usuario') && (
                <TextField id="usuario" select label="Responsable" value={usuarioId} onChange={(e) => setUsuarioId(e.target.value === '' ? '' : Number(e.target.value))}>
                  <MenuItem value="">Todos</MenuItem>
                  {responsables.data?.map((u) => <MenuItem key={u.id} value={u.id}>{u.nombre}</MenuItem>)}
                </TextField>
              )}
            </Box>
            <Stack direction="row" spacing={1}>
              <Button variant="contained" startIcon={<PictureAsPdfOutlined />} onClick={() => exportar('pdf')} disabled={!!exportando}>{exportando === 'pdf' ? 'Generando…' : 'PDF'}</Button>
              <Button variant="outlined" startIcon={<GridOnOutlined />} onClick={() => exportar('xlsx')} disabled={!!exportando}>{exportando === 'xlsx' ? 'Generando…' : 'Excel'}</Button>
            </Stack>
          </Stack>
        </Paper>
      )}

      <Paper sx={{ minWidth: 0 }}>
        {reporte.isLoading || !reporte.data ? <Cargando /> : (
          <>
            <Box sx={{ p: 2, pb: 1 }}>
              <Typography variant="h2">{reporte.data.titulo}</Typography>
              <Typography variant="body2" color="text.secondary">{[reporte.data.descripcion, ...reporte.data.filtros].join(' · ')}</Typography>
            </Box>
            {reporte.data.filas.length === 0 ? <Vacio>No hay datos para los filtros elegidos.</Vacio> : (
              <Box sx={{ overflowX: 'auto', maxHeight: 560 }}>
                <Table size="small" stickyHeader>
                  <TableHead>
                    <TableRow>{reporte.data.columnas.map((c) => <TableCell key={c.titulo} align={c.tipo === 'Numero' ? 'right' : 'left'}>{c.titulo}</TableCell>)}</TableRow>
                  </TableHead>
                  <TableBody>
                    {reporte.data.filas.map((f, i) => (
                      <TableRow key={i} hover>
                        {f.map((v, j) => {
                          const t = reporte.data!.columnas[j].tipo
                          return <TableCell key={j} align={t === 'Numero' ? 'right' : 'left'} sx={{ whiteSpace: t === 'Texto' ? 'normal' : 'nowrap', fontVariantNumeric: 'tabular-nums' }}>{celda(v, t)}</TableCell>
                        })}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </>
        )}
      </Paper>
    </Pagina>
  )
}
