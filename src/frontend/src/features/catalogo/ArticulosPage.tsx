import { useState } from 'react'
import { useNavigate } from 'react-router'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  Box, Button, Checkbox, FormControlLabel, InputAdornment, MenuItem, Paper, Stack, Table, TableBody, TableCell,
  TableHead, TablePagination, TableRow, TextField, Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import SearchIcon from '@mui/icons-material/Search'
import { api } from '../../api/cliente'
import { Permisos, type ArticuloResumen, type Categoria, type Paginado, type TipoInventario, type Ubicacion } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { Cargando, ChipEstado, ChipStock, Pagina, Vacio } from '../../components/Pagina'
import { diasHasta, fmtFecha, nombreTipoInventario } from '../../lib/formato'
import { ArticuloFormDialog } from './ArticuloFormDialog'

export function useCategorias(incluirInactivas = false) {
  return useQuery({
    queryKey: ['categorias', incluirInactivas],
    queryFn: async () => (await api.get<Categoria[]>('/categorias', { params: { incluirInactivas } })).data,
  })
}

export function useUbicaciones(incluirInactivas = false) {
  return useQuery({
    queryKey: ['ubicaciones', incluirInactivas],
    queryFn: async () => (await api.get<Ubicacion[]>('/ubicaciones', { params: { incluirInactivas } })).data,
  })
}

export function ArticulosPage() {
  const { tiene } = useAuth()
  const navegar = useNavigate()
  const [texto, setTexto] = useState('')
  const [categoriaId, setCategoriaId] = useState<number | ''>('')
  const [ubicacionId, setUbicacionId] = useState<number | ''>('')
  const [tipo, setTipo] = useState<TipoInventario | ''>('')
  const [bajoMinimo, setBajoMinimo] = useState(false)
  const [inactivos, setInactivos] = useState(false)
  const [pagina, setPagina] = useState(0)
  const [tamano, setTamano] = useState(25)
  const [nuevo, setNuevo] = useState(false)

  const categorias = useCategorias()
  const ubicaciones = useUbicaciones()
  const filtros = { texto, categoriaId, ubicacionId, tipo, bajoMinimo, incluirInactivos: inactivos, pagina: pagina + 1, tamano }
  const { data, isLoading } = useQuery({
    queryKey: ['articulos', filtros],
    queryFn: async () => (await api.get<Paginado<ArticuloResumen>>('/articulos', {
      params: Object.fromEntries(Object.entries(filtros).filter(([, v]) => v !== '' && v !== false)),
    })).data,
    placeholderData: keepPreviousData,
  })
  const cambiar = <T,>(set: (v: T) => void) => (v: T) => { set(v); setPagina(0) }

  return (
    <Pagina
      titulo="Artículos"
      subtitulo="Materia prima, herramientas, productos de acabado y herramientas de instalación."
      acciones={tiene(Permisos.ArticulosEditar) && (
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setNuevo(true)}>Nuevo artículo</Button>
      )}
    >
      <Paper sx={{ p: 2 }}>
        <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: '2fr 1fr 1fr 1fr' } }}>
          <TextField id="buscar" placeholder="Buscar por código, nombre u otro nombre…" value={texto}
            onChange={(e) => cambiar(setTexto)(e.target.value)}
            slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }} />
          <TextField id="filtro-tipo" select label="Tipo" value={tipo} onChange={(e) => cambiar(setTipo)(e.target.value as TipoInventario | '')}>
            <MenuItem value="">Todos</MenuItem>
            {Object.entries(nombreTipoInventario).map(([k, v]) => <MenuItem key={k} value={k}>{v}</MenuItem>)}
          </TextField>
          <TextField id="filtro-categoria" select label="Categoría" value={categoriaId} onChange={(e) => cambiar(setCategoriaId)(e.target.value === '' ? '' : Number(e.target.value))}>
            <MenuItem value="">Todas</MenuItem>
            {categorias.data?.map((c) => <MenuItem key={c.id} value={c.id}>{c.nombre}</MenuItem>)}
          </TextField>
          <TextField id="filtro-ubicacion" select label="Ubicación" value={ubicacionId} onChange={(e) => cambiar(setUbicacionId)(e.target.value === '' ? '' : Number(e.target.value))}>
            <MenuItem value="">Todas</MenuItem>
            {ubicaciones.data?.map((u) => <MenuItem key={u.id} value={u.id}>{u.nombre}</MenuItem>)}
          </TextField>
        </Box>
        <Stack direction="row" sx={{ mt: 1, flexWrap: 'wrap' }}>
          <FormControlLabel control={<Checkbox id="bajo-minimo" checked={bajoMinimo} onChange={(e) => cambiar(setBajoMinimo)(e.target.checked)} />} label="Solo en o bajo el mínimo" />
          <FormControlLabel control={<Checkbox id="inactivos" checked={inactivos} onChange={(e) => cambiar(setInactivos)(e.target.checked)} />} label="Incluir desactivados" />
        </Stack>
      </Paper>

      <Paper sx={{ minWidth: 0 }}>
        {isLoading ? <Cargando /> : !data?.items.length ? <Vacio>No hay artículos con esos filtros.</Vacio> : (
          <>
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Código</TableCell><TableCell>Nombre</TableCell><TableCell>Categoría</TableCell>
                    <TableCell>Ubicación</TableCell><TableCell>Existencia</TableCell><TableCell>Estado</TableCell><TableCell>Vence</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.items.map((a) => (
                    <TableRow key={a.id} hover sx={{ cursor: 'pointer', opacity: a.activo ? 1 : 0.55 }} onClick={() => navegar(`/articulos/${a.id}`)}>
                      <TableCell sx={{ fontFamily: 'monospace', whiteSpace: 'nowrap' }}>{a.codigo}</TableCell>
                      <TableCell>{a.nombre}{!a.activo && <Typography component="span" variant="caption" color="text.secondary"> · desactivado</Typography>}</TableCell>
                      <TableCell>{a.categoria}</TableCell>
                      <TableCell>{a.ubicacion}</TableCell>
                      <TableCell><ChipStock actual={a.stockActual} minimo={a.stockMinimo} unidad={a.unidadMedida} /></TableCell>
                      <TableCell><ChipEstado estado={a.estado} /></TableCell>
                      <TableCell sx={{ whiteSpace: 'nowrap', color: a.fechaVencimiento && diasHasta(a.fechaVencimiento) <= 30 ? 'error.main' : undefined }}>
                        {fmtFecha(a.fechaVencimiento)}
                      </TableCell>
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

      {nuevo && <ArticuloFormDialog onClose={() => setNuevo(false)} onGuardado={(id) => navegar(`/articulos/${id}`)} />}
    </Pagina>
  )
}
