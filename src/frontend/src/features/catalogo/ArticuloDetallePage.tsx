import { useRef, useState } from 'react'
import { Link as RouterLink, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box, Breadcrumbs, Button, Chip, IconButton, Link, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Tooltip, Typography,
} from '@mui/material'
import EditOutlined from '@mui/icons-material/EditOutlined'
import AddAPhotoOutlined from '@mui/icons-material/AddAPhotoOutlined'
import DeleteOutlined from '@mui/icons-material/DeleteOutlined'
import ImageOutlined from '@mui/icons-material/ImageOutlined'
import SwapVertOutlined from '@mui/icons-material/SwapVertOutlined'
import { api, mensajeError } from '../../api/cliente'
import { Permisos, type ArticuloDetalle, type HistorialEstado } from '../../api/tipos'
import { useAuth } from '../../app/AuthContext'
import { useNotificar } from '../../components/Notificaciones'
import { Cargando, ChipEstado, ChipStock, Pagina } from '../../components/Pagina'
import { fmtFecha, fmtFechaHora, fmtNumero, nombreTipoInventario } from '../../lib/formato'
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

const TIPOS_FOTO = 'image/jpeg,image/png,image/webp'
const MAX_FOTO = 3 * 1024 * 1024

/** RF-08: fotografía del artículo. Se guarda en el servidor; aquí se ve, se cambia o se quita. */
function FotoArticulo({ id, nombre, tieneFoto, editable }: { id: number; nombre: string; tieneFoto: boolean; editable: boolean }) {
  const qc = useQueryClient()
  const notificar = useNotificar()
  const entrada = useRef<HTMLInputElement>(null)
  const [version, setVersion] = useState(0)   // obliga al navegador a pedir la foto nueva

  const listo = (texto: string) => {
    setVersion((v) => v + 1)
    qc.invalidateQueries({ queryKey: ['articulo', id] })
    notificar(texto)
  }
  const subir = useMutation({
    mutationFn: async (archivo: File) => {
      const datos = new FormData()
      datos.append('archivo', archivo)
      await api.post(`/articulos/${id}/foto`, datos)
    },
    onSuccess: () => listo('Foto guardada.'),
    onError: (e) => notificar(mensajeError(e), 'error'),
  })
  const quitar = useMutation({
    mutationFn: () => api.delete(`/articulos/${id}/foto`),
    onSuccess: () => listo('Foto eliminada.'),
    onError: (e) => notificar(mensajeError(e), 'error'),
  })

  function elegir(e: React.ChangeEvent<HTMLInputElement>) {
    const archivo = e.target.files?.[0]
    e.target.value = ''
    if (!archivo) return
    if (archivo.size > MAX_FOTO) { notificar('La foto pesa más de 3 MB.', 'error'); return }
    subir.mutate(archivo)
  }

  return (
    <Paper sx={{ position: 'relative', overflow: 'hidden', aspectRatio: '4 / 3', display: 'grid', placeItems: 'center', bgcolor: 'action.hover' }}>
      {tieneFoto ? (
        <Box component="img" src={`/api/articulos/${id}/foto?v=${version}`} alt={`Foto de ${nombre}`}
          sx={{ width: '100%', height: '100%', objectFit: 'cover' }} />
      ) : (
        <Stack sx={{ alignItems: 'center', color: 'text.secondary', p: 2, textAlign: 'center' }} spacing={0.5}>
          <ImageOutlined sx={{ fontSize: 40 }} />
          <Typography variant="body2">Sin fotografía</Typography>
        </Stack>
      )}
      {editable && (
        <Stack direction="row" spacing={0.5} sx={{ position: 'absolute', right: 8, bottom: 8 }}>
          <input ref={entrada} type="file" accept={TIPOS_FOTO} hidden onChange={elegir} />
          <Tooltip title={tieneFoto ? 'Cambiar foto' : 'Subir foto (JPG, PNG o WEBP, hasta 3 MB)'}>
            <IconButton aria-label={tieneFoto ? 'Cambiar foto' : 'Subir foto'} onClick={() => entrada.current?.click()} disabled={subir.isPending}
              sx={{ bgcolor: 'background.paper', boxShadow: 1, '&:hover': { bgcolor: 'background.paper' } }}>
              <AddAPhotoOutlined fontSize="small" />
            </IconButton>
          </Tooltip>
          {tieneFoto && (
            <Tooltip title="Quitar foto">
              <IconButton aria-label="Quitar foto" onClick={() => quitar.mutate()} disabled={quitar.isPending}
                sx={{ bgcolor: 'background.paper', boxShadow: 1, '&:hover': { bgcolor: 'background.paper' } }}>
                <DeleteOutlined fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Stack>
      )}
    </Paper>
  )
}

/** RF-11: cada cambio de estado (por edición, devolución de instalación o baja) con quién lo hizo. */
function HistorialEstados({ id }: { id: number }) {
  const { data, isLoading } = useQuery({
    queryKey: ['articulo', id, 'estados'],
    queryFn: async () => (await api.get<HistorialEstado[]>(`/articulos/${id}/estados`)).data,
  })
  if (isLoading) return <Cargando />
  if (!data?.length) return <Paper><Typography color="text.secondary" sx={{ p: 2 }}>El estado no ha cambiado desde que se registró.</Typography></Paper>
  return (
    <Paper sx={{ minWidth: 0, overflowX: 'auto' }}>
      <Table size="small">
        <TableHead><TableRow><TableCell>Fecha</TableCell><TableCell>Cambio</TableCell><TableCell>Responsable</TableCell></TableRow></TableHead>
        <TableBody>
          {data.map((h) => (
            <TableRow key={h.id}>
              <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtFechaHora(h.fechaHora)}</TableCell>
              <TableCell sx={{ whiteSpace: 'nowrap' }}><ChipEstado estado={h.estadoAnterior} /> → <ChipEstado estado={h.estadoNuevo} /></TableCell>
              <TableCell>{h.usuario}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Paper>
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
      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', md: '260px 1fr' }, alignItems: 'start' }}>
      <FotoArticulo id={id} nombre={a.nombre} tieneFoto={data.tieneFoto} editable={tiene(Permisos.ArticulosEditar)} />
      <Paper sx={{ p: 2.5, minWidth: 0 }}>
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
      </Box>

      <Typography variant="h2">Historial de estados</Typography>
      <HistorialEstados id={id} />

      <Typography variant="h2">Historial de movimientos</Typography>
      <HistorialMovimientos articuloId={id} />

      {editar && <ArticuloFormDialog detalle={data} onClose={() => setEditar(false)} />}
      {mover && <RegistrarMovimientoDialog articulo={a} onClose={() => setMover(false)} />}
    </Pagina>
  )
}
