import type { ReactNode } from 'react'
import { Box, Chip, CircularProgress, Stack, Typography } from '@mui/material'
import type { EstadoArticulo } from '../api/tipos'
import { nombreEstado } from '../lib/formato'

export function Pagina({ titulo, subtitulo, acciones, children }: {
  titulo: string
  subtitulo?: ReactNode
  acciones?: ReactNode
  children: ReactNode
}) {
  return (
    <Stack spacing={2.5}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'space-between', alignItems: { sm: 'flex-end' } }}>
        <Box>
          <Typography variant="h1">{titulo}</Typography>
          {subtitulo && <Typography component="div" color="text.secondary" sx={{ mt: 0.5 }}>{subtitulo}</Typography>}
        </Box>
        {acciones && <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>{acciones}</Stack>}
      </Stack>
      {children}
    </Stack>
  )
}

export function Cargando() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', py: 6 }}>
      <CircularProgress size={28} />
    </Box>
  )
}

export function Vacio({ children }: { children: ReactNode }) {
  return <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>{children}</Typography>
}

const colorEstado: Record<EstadoArticulo, 'default' | 'success' | 'warning' | 'error'> = {
  Nueva: 'success', Buena: 'default', Desgastada: 'warning', Danada: 'error', DeBaja: 'error',
}

export function ChipEstado({ estado }: { estado: EstadoArticulo }) {
  return <Chip size="small" variant="outlined" color={colorEstado[estado]} label={nombreEstado[estado]} />
}

export function ChipStock({ actual, minimo, unidad }: { actual: number; minimo: number; unidad: string }) {
  const bajo = actual <= minimo
  return (
    <Chip
      size="small"
      color={actual === 0 ? 'error' : bajo ? 'warning' : 'default'}
      variant={bajo ? 'filled' : 'outlined'}
      label={`${new Intl.NumberFormat('es-CR', { maximumFractionDigits: 2 }).format(actual)} ${unidad}`}
      sx={{ fontVariantNumeric: 'tabular-nums' }}
    />
  )
}
