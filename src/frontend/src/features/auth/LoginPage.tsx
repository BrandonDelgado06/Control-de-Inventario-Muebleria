import { useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react'
import {
  Alert, Box, Button, CircularProgress, IconButton, InputAdornment, Stack, TextField, Tooltip, Typography,
} from '@mui/material'
import PersonOutlined from '@mui/icons-material/PersonOutlined'
import LockOutlined from '@mui/icons-material/LockOutlined'
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined'
import VisibilityOffOutlined from '@mui/icons-material/VisibilityOffOutlined'
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined'
import NotificationsActiveOutlined from '@mui/icons-material/NotificationsActiveOutlined'
import AssignmentOutlined from '@mui/icons-material/AssignmentOutlined'
import { useAuth } from '../../app/AuthContext'
import { BotonModo } from '../../app/ModoTema'
import { mensajeError } from '../../api/cliente'

/**
 * Vetas de madera dibujadas en SVG (sin imágenes externas: el taller no tiene internet).
 * Líneas onduladas que se abren alrededor de dos nudos, como en una tabla de pino.
 */
const vetas = (() => {
  const lineas: string[] = []
  for (let i = 0; i < 22; i++) {
    const y = 20 + i * 28
    const a = 10 + (i % 5) * 4          // amplitud distinta en cada veta
    const d = i % 2 === 0 ? 1 : -1
    lineas.push(`<path d='M-40 ${y} C 120 ${y - a * d}, 240 ${y + a * d}, 380 ${y} S 620 ${y - a * d}, 760 ${y}'/>`)
  }
  const nudos = `<ellipse cx='520' cy='170' rx='46' ry='16'/><ellipse cx='520' cy='170' rx='30' ry='9'/>` +
    `<ellipse cx='170' cy='470' rx='38' ry='13'/><ellipse cx='170' cy='470' rx='22' ry='7'/>`
  const svg = `<svg xmlns='http://www.w3.org/2000/svg' width='720' height='640' viewBox='0 0 720 640'><g fill='none' stroke='white' stroke-width='1.3'>${lineas.join('')}${nudos}</g></svg>`
  return `url("data:image/svg+xml,${encodeURIComponent(svg)}")`
})()

/** Una línea del panel de marca: ícono + qué ofrece el sistema. */
function Ventaja({ icono, titulo, texto }: { icono: ReactNode; titulo: string; texto: string }) {
  return (
    <Stack direction="row" spacing={1.75} sx={{ alignItems: 'flex-start' }}>
      <Box sx={{ p: 1, borderRadius: 1.5, bgcolor: 'rgba(255,255,255,0.12)', display: 'flex', color: '#F3D9A4' }}>{icono}</Box>
      <Box>
        <Typography sx={{ fontWeight: 600, color: '#FFFFFF' }}>{titulo}</Typography>
        <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.72)' }}>{texto}</Typography>
      </Box>
    </Stack>
  )
}

/**
 * Inicio de sesión (RF-01). Dos paneles: a la izquierda la identidad del taller, a la derecha el formulario.
 * En pantallas pequeñas el panel de marca se reduce a un encabezado.
 */
export function LoginPage() {
  const { iniciar, expirada } = useAuth()
  const [usuario, setUsuario] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [verClave, setVerClave] = useState(false)
  const [mayusculas, setMayusculas] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setEnviando(true)
    try {
      await iniciar(usuario.trim(), contrasena)
    } catch (err) {
      setError(mensajeError(err))
    } finally {
      setEnviando(false)
    }
  }

  const revisarMayusculas = (e: KeyboardEvent) => setMayusculas(e.getModifierState?.('CapsLock') ?? false)

  return (
    <Box sx={{
      minHeight: '100vh', display: 'grid', bgcolor: 'background.default',
      gridTemplateColumns: { xs: '1fr', md: 'minmax(380px, 5fr) 6fr' },
      gridTemplateRows: { xs: 'auto 1fr', md: '1fr' },   // en celular el encabezado verde ocupa solo lo necesario
    }}>
      {/* ---------------------------------------------------------------- Panel de marca */}
      <Box
        component="aside"
        sx={(t) => ({
          position: 'relative', overflow: 'hidden', color: '#FFFFFF',
          display: 'flex', flexDirection: 'column', justifyContent: 'space-between', gap: 4,
          px: { xs: 3, sm: 5, lg: 7 }, py: { xs: 3, md: 6 },
          background: t.palette.mode === 'dark'
            ? 'linear-gradient(155deg, #17443A 0%, #0E2A24 100%)'
            : 'linear-gradient(155deg, #23806A 0%, #15493D 100%)',
          // Vetas de madera muy suaves.
          '&::before': {
            content: '""', position: 'absolute', inset: 0, opacity: t.palette.mode === 'dark' ? 0.06 : 0.09, pointerEvents: 'none',
            backgroundImage: vetas, backgroundSize: 'cover', backgroundPosition: 'center',
          },
          '&::after': {
            content: '""', position: 'absolute', width: 420, height: 420, right: -160, top: -140, borderRadius: '50%',
            background: 'radial-gradient(circle, rgba(243,217,164,0.22) 0%, transparent 70%)', pointerEvents: 'none',
          },
        })}
      >
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', position: 'relative' }}>
          <Box sx={{ width: 44, height: 44, borderRadius: 2, bgcolor: '#F3D9A4', color: '#15493D', display: 'grid', placeItems: 'center', fontWeight: 800, fontSize: '1.05rem' }}>IM</Box>
          <Box>
            <Typography sx={{ fontWeight: 800, lineHeight: 1.1, letterSpacing: '-0.01em' }}>Inventario</Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)' }}>Mueblería</Typography>
          </Box>
        </Stack>

        <Box sx={{ position: 'relative', display: { xs: 'none', md: 'block' } }}>
          <Typography component="h1" sx={{ fontSize: { md: '2rem', lg: '2.4rem' }, fontWeight: 800, lineHeight: 1.15, letterSpacing: '-0.02em', maxWidth: 440 }}>
            Todo el taller,<br />en orden.
          </Typography>
          <Typography sx={{ mt: 1.5, color: 'rgba(255,255,255,0.78)', maxWidth: 420 }}>
            Materia prima, herramientas y productos de acabado en un solo lugar, con su existencia siempre al día.
          </Typography>
          <Stack spacing={2.5} sx={{ mt: 4, maxWidth: 420 }}>
            <Ventaja icono={<Inventory2Outlined fontSize="small" />} titulo="Existencias al día" texto="Cada entrada y salida actualiza el stock y queda registrada." />
            <Ventaja icono={<NotificationsActiveOutlined fontSize="small" />} titulo="Alertas a tiempo" texto="Stock mínimo, productos por vencer y herramientas sin devolver." />
            <Ventaja icono={<AssignmentOutlined fontSize="small" />} titulo="Solicitudes ordenadas" texto="El taller pide, la bodega aprueba y entrega." />
          </Stack>
        </Box>

        <Typography variant="caption" sx={{ position: 'relative', color: 'rgba(255,255,255,0.6)', display: { xs: 'none', md: 'block' } }}>
          Sistema de uso interno · Funciona en la red del taller, sin internet
        </Typography>
      </Box>

      {/* ---------------------------------------------------------------- Formulario */}
      <Box component="main" sx={{ position: 'relative', display: 'grid', placeItems: 'center', px: { xs: 2.5, sm: 4 }, py: { xs: 4, md: 6 } }}>
        <Box sx={{ position: 'absolute', top: 12, right: 12 }}><BotonModo /></Box>

        <Box component="form" onSubmit={enviar} sx={{ width: '100%', maxWidth: 400 }}>
          <Stack spacing={2.5}>
            <Box>
              <Typography component="h2" sx={{ fontSize: '1.7rem', fontWeight: 700, letterSpacing: '-0.01em' }}>Iniciar sesión</Typography>
              <Typography color="text.secondary" sx={{ mt: 0.5 }}>Ingresa con tu usuario del taller.</Typography>
            </Box>

            {expirada && !error && <Alert severity="info">Tu sesión se cerró por inactividad. Vuelve a ingresar.</Alert>}
            {error && <Alert severity="error">{error}</Alert>}

            <TextField
              id="usuario" label="Usuario" autoComplete="username" autoFocus size="medium"
              value={usuario} onChange={(e) => setUsuario(e.target.value)}
              slotProps={{ input: { startAdornment: <InputAdornment position="start"><PersonOutlined color="action" /></InputAdornment> } }}
            />
            <TextField
              id="contrasena" label="Contraseña" type={verClave ? 'text' : 'password'} autoComplete="current-password" size="medium"
              value={contrasena} onChange={(e) => setContrasena(e.target.value)}
              onKeyUp={revisarMayusculas} onKeyDown={revisarMayusculas}
              helperText={mayusculas ? 'Las mayúsculas están activadas.' : ' '}
              slotProps={{
                formHelperText: { sx: { color: 'warning.main', mx: 0 } },
                input: {
                  startAdornment: <InputAdornment position="start"><LockOutlined color="action" /></InputAdornment>,
                  endAdornment: (
                    <InputAdornment position="end">
                      <Tooltip title={verClave ? 'Ocultar contraseña' : 'Mostrar contraseña'}>
                        <IconButton edge="end" onClick={() => setVerClave((v) => !v)} aria-label={verClave ? 'Ocultar contraseña' : 'Mostrar contraseña'}>
                          {verClave ? <VisibilityOffOutlined /> : <VisibilityOutlined />}
                        </IconButton>
                      </Tooltip>
                    </InputAdornment>
                  ),
                },
              }}
            />

            <Button type="submit" variant="contained" size="large" disabled={enviando || !usuario.trim() || !contrasena}
              sx={{ py: 1.4, fontSize: '1rem' }}
              startIcon={enviando ? <CircularProgress size={18} color="inherit" /> : undefined}>
              {enviando ? 'Ingresando…' : 'Ingresar'}
            </Button>

            <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
              ¿Olvidaste tu contraseña? Pídele al administrador que la cambie desde <b>Usuarios</b>.
            </Typography>
          </Stack>
        </Box>

        <Typography variant="caption" color="text.secondary" sx={{ position: 'absolute', bottom: 16 }}>
          Control de Inventario Mueblería · v0.4
        </Typography>
      </Box>
    </Box>
  )
}
