import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  Box, MenuItem, Paper, Table, TableBody, TableCell, TableHead, TablePagination, TableRow, TextField,
} from '@mui/material'
import { api } from '../../api/cliente'
import type { Paginado, RegistroBitacora } from '../../api/tipos'
import { Cargando, Pagina, Vacio } from '../../components/Pagina'
import { fmtFechaHora } from '../../lib/formato'

/** RF-03 (registro automático) y RF-04 (consulta con filtros). Solo lectura. */
export function BitacoraPage() {
  const [modulo, setModulo] = useState('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [pagina, setPagina] = useState(0)
  const [tamano, setTamano] = useState(50)

  const modulos = useQuery({ queryKey: ['bitacora-modulos'], queryFn: async () => (await api.get<string[]>('/bitacora/modulos')).data })
  const params = { modulo: modulo || undefined, desde: desde || undefined, hasta: hasta || undefined, pagina: pagina + 1, tamano }
  const { data, isLoading } = useQuery({
    queryKey: ['bitacora', params],
    queryFn: async () => (await api.get<Paginado<RegistroBitacora>>('/bitacora', { params })).data,
    placeholderData: keepPreviousData,
  })

  return (
    <Pagina titulo="Bitácora" subtitulo="Quién hizo cada cambio, cuándo y en qué módulo. Nadie puede editarla.">
      <Paper sx={{ p: 2 }}>
        <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' } }}>
          <TextField id="modulo" select label="Módulo" value={modulo} onChange={(e) => { setModulo(e.target.value); setPagina(0) }}>
            <MenuItem value="">Todos</MenuItem>
            {modulos.data?.map((m) => <MenuItem key={m} value={m}>{m}</MenuItem>)}
          </TextField>
          <TextField id="desde" type="date" label="Desde" value={desde} onChange={(e) => { setDesde(e.target.value); setPagina(0) }} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField id="hasta" type="date" label="Hasta" value={hasta} onChange={(e) => { setHasta(e.target.value); setPagina(0) }} slotProps={{ inputLabel: { shrink: true } }} />
        </Box>
      </Paper>
      <Paper sx={{ minWidth: 0 }}>
        {isLoading ? <Cargando /> : !data?.items.length ? <Vacio>No hay registros con esos filtros.</Vacio> : (
          <>
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead><TableRow><TableCell>Fecha</TableCell><TableCell>Usuario</TableCell><TableCell>Módulo</TableCell><TableCell>Acción</TableCell></TableRow></TableHead>
                <TableBody>
                  {data.items.map((b) => (
                    <TableRow key={b.id}>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(b.fechaHora)}</TableCell>
                      <TableCell>{b.usuario}</TableCell>
                      <TableCell>{b.modulo}</TableCell>
                      <TableCell>{b.accion}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
            <TablePagination
              component="div" count={data.total} page={pagina} rowsPerPage={tamano}
              onPageChange={(_, p) => setPagina(p)}
              onRowsPerPageChange={(e) => { setTamano(Number(e.target.value)); setPagina(0) }}
              rowsPerPageOptions={[25, 50, 100]}
              labelRowsPerPage="Filas por página"
              labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
            />
          </>
        )}
      </Paper>
    </Pagina>
  )
}
