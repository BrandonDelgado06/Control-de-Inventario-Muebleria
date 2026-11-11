import { useState } from 'react'
import { Link as RouterLink, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Breadcrumbs, Button, Chip, Link, Paper, Stack, Typography } from '@mui/material'
import EditOutlined from '@mui/icons-material/EditOutlined'
import SwapVertOutlined from '@mui/icons-material/SwapVertOutlined'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type ArticuloDetalle } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, ChipEstado, ChipStock, Pagina } from '../../components/Pagina'
import { fmtFecha, fmtNumero, nombreTipoInventario } from '../../lib/formato'
import { HistorialMovimientos } from '../movimientos/MovimientosPage'
import { RegistrarMovimientoDialog } from '../movimientos/RegistrarMovimientoDialog'
import { ArticuloFormDialog } from './ArticuloFormDialog'

function Dato({ etiqueta, children }: { etiqueta: string; children: React.ReactNode }) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="caption" color="text.secondary">{etiqueta}</Typography>
      <Typography component="div">{children}</Typography>
    </Box>
  )
}

export function ArticuloDetallePage() {
  const id = Number(useParams().id)
  const { tiene } = useAuth()
  const qc = useQueryClient()
  const notificar = useNotificar()
  const [editar, setEditar] = useState(false)
  const [mover, setMover] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['articulo', id],
    queryFn: async () => (await api.get<ArticuloDetalle>(`/articulos/${id}`)).data,
  })
  const activar = useMutation({
    mutationFn: (activo: boolean) => api.put(`/articulos/${id}/activo`, activo, { headers: { 'Content-Type': 'application/json' } }),
    onSuccess: (_, activo) => {
      qc.invalidateQueries({ queryKey: ['articulo', id] })
      qc.invalidateQueries({ queryKey: ['articulos'] })
      notificar(activo ? 'Artículo reactivado.' : 'Artículo desactivado. Su historial se conserva.')
    },
    onError: (e) => notificar(mensajeError(e), 'error'),
  })

  if (isLoading || !data) return <Cargando />
  const a = data.articulo

  return (
    <Pagina
      titulo={a.nombre}
      subtitulo={
        <Breadcrumbs component="span" sx={{ fontSize: 'inherit' }}>
          <Link component={RouterLink} to="/articulos" underline="hover" color="inherit">Artículos</Link>
          <span style={{ fontFamily: 'monospace' }}>{a.codigo}</span>
        </Breadcrumbs>
      }
      acciones={<>
        {tiene(Permisos.MovimientosRegistrar) && a.activo && (
          <Button variant="contained" startIcon={<SwapVertOutlined />} onClick={() => setMover(true)}>Registrar movimiento</Button>
        )}
        {tiene(Permisos.ArticulosEditar) && <>
          <Button variant="outlined" startIcon={<EditOutlined />} onClick={() => setEditar(true)}>Editar</Button>
          <Button color={a.activo ? 'error' : 'primary'} onClick={() => activar.mutate(!a.activo)} disabled={activar.isPending}>
            {a.activo ? 'Desactivar' : 'Reactivar'}
          </Button>
        </>}
      </>}
    >
      <Paper sx={{ p: 2.5 }}>
        <Box sx={{ display: 'grid', gap: 2.5, gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' } }}>
          <Dato etiqueta="Existencia"><ChipStock actual={a.stockActual} minimo={a.stockMinimo} unidad={a.unidadMedida} /></Dato>
          <Dato etiqueta="Stock mínimo">{fmtNumero(a.stockMinimo)} {a.unidadMedida}</Dato>
          <Dato etiqueta="Estado"><ChipEstado estado={a.estado} /></Dato>
          <Dato etiqueta="Activo">{a.activo ? 'Sí' : 'No, desactivado'}</Dato>
          <Dato etiqueta="Categoría">{a.categoria}<Typography variant="caption" color="text.secondary" component="div">{nombreTipoInventario[a.tipoInventario]}</Typography></Dato>
          <Dato etiqueta="Ubicación">{a.ubicacion}</Dato>
          {a.tipoInventario === 'Acabado' && <>
            <Dato etiqueta="Lote">{a.lote ?? '—'}{data.presentacion && ` · ${data.presentacion}`}</Dato>
            <Dato etiqueta="Vence">{fmtFecha(a.fechaVencimiento)}</Dato>
          </>}
          {data.campos.map((c) => <Dato key={c.campoId} etiqueta={c.campo}>{c.valor ?? '—'}</Dato>)}
        </Box>
        {data.alias.length > 0 && (
          <Stack direction="row" spacing={1} sx={{ mt: 2.5, alignItems: 'center', flexWrap: 'wrap', rowGap: 1 }}>
            <Typography variant="caption" color="text.secondary">También le dicen:</Typography>
            {data.alias.map((x) => <Chip key={x} size="small" label={x} />)}
          </Stack>
        )}
      </Paper>

      <Typography variant="h2">Historial de movimientos</Typography>
      <HistorialMovimientos articuloId={id} />

      {editar && <ArticuloFormDialog detalle={data} onClose={() => setEditar(false)} />}
      {mover && <RegistrarMovimientoDialog articulo={a} onClose={() => setMover(false)} />}
    </Pagina>
  )
}
