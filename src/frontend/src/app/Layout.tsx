import { useState, type ReactNode } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import {
  AppBar, Avatar, Badge, Box, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText,
  ListSubheader, Toolbar, Tooltip, Typography, useMediaQuery, useTheme,
} from '@mui/material'
import MenuIcon from '@mui/icons-material/Menu'
import HomeOutlined from '@mui/icons-material/HomeOutlined'
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined'
import SwapVertOutlined from '@mui/icons-material/SwapVertOutlined'
import HandymanOutlined from '@mui/icons-material/HandymanOutlined'
import PlaceOutlined from '@mui/icons-material/PlaceOutlined'
import CategoryOutlined from '@mui/icons-material/CategoryOutlined'
import PeopleOutlined from '@mui/icons-material/PeopleOutlined'
import HistoryOutlined from '@mui/icons-material/HistoryOutlined'
import AssignmentOutlined from '@mui/icons-material/AssignmentOutlined'
import NotificationsOutlined from '@mui/icons-material/NotificationsOutlined'
import AssessmentOutlined from '@mui/icons-material/AssessmentOutlined'
import BackupOutlined from '@mui/icons-material/BackupOutlined'
import LogoutOutlined from '@mui/icons-material/LogoutOutlined'
import { useAuth } from './AuthContext'
import { BotonModo } from './ModoTema'
import { fondoPrimario } from './tema'
import { api } from '../api/cliente'
import { Permisos, type ResumenAlertas } from '../api/tipos'

const ANCHO = 252

interface Item { a: string; texto: string; icono: ReactNode; permiso?: string; contador?: 'alertas' | 'solicitudes' }

const grupos: { titulo: string; items: Item[] }[] = [
  {
    titulo: 'Inventario',
    items: [
      { a: '/', texto: 'Inicio', icono: <HomeOutlined /> },
      { a: '/articulos', texto: 'Artículos', icono: <Inventory2Outlined />, permiso: Permisos.ArticulosVer },
      { a: '/movimientos', texto: 'Entradas y salidas', icono: <SwapVertOutlined />, permiso: Permisos.ArticulosVer },
      { a: '/instalacion', texto: 'Herramientas fuera', icono: <HandymanOutlined />, permiso: Permisos.HerramientasPrestar },
      { a: '/solicitudes', texto: 'Solicitudes', icono: <AssignmentOutlined />, permiso: Permisos.SolicitudesCrear, contador: 'solicitudes' },
    ],
  },
  {
    titulo: 'Consulta y control',
    items: [
      { a: '/alertas', texto: 'Alertas', icono: <NotificationsOutlined />, permiso: Permisos.AlertasVer, contador: 'alertas' },
      { a: '/reportes', texto: 'Reportes', icono: <AssessmentOutlined />, permiso: Permisos.ReportesVer },
      { a: '/respaldos', texto: 'Respaldos', icono: <BackupOutlined />, permiso: Permisos.RespaldosGestionar },
    ],
  },
  {
    titulo: 'Configuración',
    items: [
      { a: '/ubicaciones', texto: 'Ubicaciones', icono: <PlaceOutlined />, permiso: Permisos.ArticulosVer },
      { a: '/categorias', texto: 'Categorías', icono: <CategoryOutlined />, permiso: Permisos.ArticulosVer },
      { a: '/usuarios', texto: 'Usuarios', icono: <PeopleOutlined />, permiso: Permisos.UsuariosGestionar },
      { a: '/bitacora', texto: 'Bitácora', icono: <HistoryOutlined />, permiso: Permisos.BitacoraVer },
    ],
  },
]

/** Contadores del menú: alertas activas y solicitudes por atender. Se refrescan cada minuto. */
function useContadores() {
  const { tiene } = useAuth()
  return useQuery({
    queryKey: ['menu-contadores'],
    queryFn: async () => ({
      alertas: tiene(Permisos.AlertasVer) ? (await api.get<ResumenAlertas>('/alertas/resumen')).data.total : 0,
      solicitudes: tiene(Permisos.SolicitudesAprobar) ? (await api.get<number>('/solicitudes/pendientes')).data : 0,
    }),
    refetchInterval: 60_000,
  })
}

function iniciales(nombre?: string) {
  return (nombre ?? '').split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]).join('').toUpperCase()
}

export function Layout() {
  const { sesion, tiene, cerrar } = useAuth()
  const theme = useTheme()
  const escritorio = useMediaQuery(theme.breakpoints.up('md'))
  const [abierto, setAbierto] = useState(false)
  const { pathname } = useLocation()
  const navegar = useNavigate()
  const contadores = useContadores()

  const menu = (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <Toolbar sx={{ px: 2.5, gap: 1.5 }}>
        <Box sx={{ width: 34, height: 34, borderRadius: 1.5, bgcolor: 'primary.main', color: 'primary.contrastText', display: 'grid', placeItems: 'center', fontWeight: 800 }}>IM</Box>
        <Box>
          <Typography sx={{ fontWeight: 800, letterSpacing: '-0.01em', lineHeight: 1.1 }}>Inventario</Typography>
          <Typography variant="body2" color="text.secondary">Mueblería</Typography>
        </Box>
      </Toolbar>
      <Divider />
      <Box sx={{ flex: 1, overflowY: 'auto', py: 0.5 }}>
        {grupos.map((g) => {
          const visibles = g.items.filter((i) => !i.permiso || tiene(i.permiso))
          if (visibles.length === 0) return null
          return (
            <List key={g.titulo} dense subheader={<ListSubheader sx={{ lineHeight: '34px', bgcolor: 'transparent' }}>{g.titulo}</ListSubheader>}>
              {visibles.map((i) => {
                const n = i.contador ? contadores.data?.[i.contador] ?? 0 : 0
                return (
                  <ListItemButton
                    key={i.a} component={NavLink} to={i.a} end={i.a === '/'}
                    selected={i.a === '/' ? pathname === '/' : pathname.startsWith(i.a)}
                    onClick={() => setAbierto(false)}
                    sx={{ mx: 1, borderRadius: 1.5, '&.Mui-selected': { bgcolor: fondoPrimario(0.14), color: 'primary.main' }, '&.Mui-selected:hover': { bgcolor: fondoPrimario(0.2) } }}
                  >
                    <ListItemIcon sx={{ minWidth: 36, color: 'inherit' }}>{i.icono}</ListItemIcon>
                    <ListItemText primary={i.texto} />
                    {n > 0 && <Badge color={i.contador === 'alertas' ? 'error' : 'warning'} badgeContent={n} max={99} sx={{ mr: 1.5 }} />}
                  </ListItemButton>
                )
              })}
            </List>
          )
        })}
      </Box>
      <Divider />
      <Typography variant="caption" color="text.secondary" sx={{ px: 2.5, py: 1.5 }}>Control de Inventario Mueblería · v0.3</Typography>
    </Box>
  )

  const totalAlertas = contadores.data?.alertas ?? 0

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" color="inherit" elevation={0}
        sx={{ borderBottom: 1, borderColor: 'divider', width: { md: `calc(100% - ${ANCHO}px)` }, ml: { md: `${ANCHO}px` } }}>
        <Toolbar sx={{ gap: 1 }}>
          {!escritorio && <IconButton edge="start" onClick={() => setAbierto(true)} aria-label="Abrir menú"><MenuIcon /></IconButton>}
          <Box sx={{ flex: 1 }} />
          <BotonModo />
          {tiene(Permisos.AlertasVer) && (
            <Tooltip title={totalAlertas ? `${totalAlertas} alertas activas` : 'Sin alertas'}>
              <IconButton onClick={() => navegar('/alertas')} aria-label="Ver alertas">
                <Badge color="error" badgeContent={totalAlertas} max={99}><NotificationsOutlined /></Badge>
              </IconButton>
            </Tooltip>
          )}
          <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />
          <Avatar sx={{ width: 34, height: 34, bgcolor: 'secondary.main', fontSize: '0.85rem' }}>{iniciales(sesion?.nombre)}</Avatar>
          <Box sx={{ display: { xs: 'none', sm: 'block' }, mr: 1 }}>
            <Typography variant="body2" sx={{ fontWeight: 600, lineHeight: 1.2 }}>{sesion?.nombre}</Typography>
            <Typography variant="caption" color="text.secondary">{sesion?.rol}</Typography>
          </Box>
          <Tooltip title="Cerrar sesión">
            <IconButton onClick={cerrar} aria-label="Cerrar sesión"><LogoutOutlined /></IconButton>
          </Tooltip>
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: ANCHO }, flexShrink: { md: 0 } }}>
        <Drawer variant={escritorio ? 'permanent' : 'temporary'} open={escritorio || abierto} onClose={() => setAbierto(false)}
          slotProps={{ paper: { sx: { width: ANCHO, boxSizing: 'border-box' } } }}>
          {menu}
        </Drawer>
      </Box>

      <Box component="main" sx={{ flex: 1, minWidth: 0, p: { xs: 2, sm: 3 }, maxWidth: 1400 }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  )
}
