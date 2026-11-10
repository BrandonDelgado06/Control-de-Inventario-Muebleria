import { StrictMode, type ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '@fontsource/roboto/400.css'
import '@fontsource/roboto/500.css'
import '@fontsource/roboto/700.css'
import { ModoTemaProvider } from './app/ModoTema'
import { AuthProvider, useAuth } from './app/AuthContext'
import { Layout } from './app/Layout'
import { NotificacionesProvider } from './components/Notificaciones'
import { Cargando } from './components/Pagina'
import { Permisos } from './api/tipos'
import { LoginPage } from './features/auth/LoginPage'
import { InicioPage } from './features/dashboard/InicioPage'
import { ArticulosPage } from './features/catalogo/ArticulosPage'
import { ArticuloDetallePage } from './features/catalogo/ArticuloDetallePage'
import { MovimientosPage } from './features/movimientos/MovimientosPage'
import { UbicacionesPage } from './features/ubicaciones/UbicacionesPage'
import { CategoriasPage } from './features/catalogo/CategoriasPage'
import { UsuariosPage } from './features/usuarios/UsuariosPage'
import { BitacoraPage } from './features/bitacora/BitacoraPage'
import { SolicitudesPage } from './features/solicitudes/SolicitudesPage'
import { InstalacionPage } from './features/instalacion/InstalacionPage'
import { AlertasPage } from './features/alertas/AlertasPage'
import { ReportesPage } from './features/reportes/ReportesPage'
import { RespaldosPage } from './features/respaldos/RespaldosPage'

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 15_000 } },
})

/** Sin sesión → login. Sin permiso → inicio. La API valida igual cada petición (RNF-02). */
function Protegida({ permiso, children }: { permiso?: string; children: ReactNode }) {
  const { sesion, cargando, tiene } = useAuth()
  if (cargando) return <Cargando />
  if (!sesion) return <Navigate to="/login" replace />
  if (permiso && !tiene(permiso)) return <Navigate to="/" replace />
  return children
}

function App() {
  const { sesion, cargando } = useAuth()
  if (cargando) return <Cargando />
  return (
    <Routes>
      <Route path="/login" element={sesion ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route element={<Protegida><Layout /></Protegida>}>
        <Route index element={<InicioPage />} />
        <Route path="articulos" element={<Protegida permiso={Permisos.ArticulosVer}><ArticulosPage /></Protegida>} />
        <Route path="articulos/:id" element={<Protegida permiso={Permisos.ArticulosVer}><ArticuloDetallePage /></Protegida>} />
        <Route path="movimientos" element={<Protegida permiso={Permisos.ArticulosVer}><MovimientosPage /></Protegida>} />
        <Route path="ubicaciones" element={<Protegida permiso={Permisos.ArticulosVer}><UbicacionesPage /></Protegida>} />
        <Route path="categorias" element={<Protegida permiso={Permisos.ArticulosVer}><CategoriasPage /></Protegida>} />
        <Route path="usuarios" element={<Protegida permiso={Permisos.UsuariosGestionar}><UsuariosPage /></Protegida>} />
        <Route path="bitacora" element={<Protegida permiso={Permisos.BitacoraVer}><BitacoraPage /></Protegida>} />
        <Route path="solicitudes" element={<Protegida permiso={Permisos.SolicitudesCrear}><SolicitudesPage /></Protegida>} />
        <Route path="instalacion" element={<Protegida permiso={Permisos.HerramientasPrestar}><InstalacionPage /></Protegida>} />
        <Route path="alertas" element={<Protegida permiso={Permisos.AlertasVer}><AlertasPage /></Protegida>} />
        <Route path="reportes" element={<Protegida permiso={Permisos.ReportesVer}><ReportesPage /></Protegida>} />
        <Route path="respaldos" element={<Protegida permiso={Permisos.RespaldosGestionar}><RespaldosPage /></Protegida>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ModoTemaProvider>
      <QueryClientProvider client={queryClient}>
        <NotificacionesProvider>
          <BrowserRouter>
            <AuthProvider>
              <App />
            </AuthProvider>
          </BrowserRouter>
        </NotificacionesProvider>
      </QueryClientProvider>
    </ModoTemaProvider>
  </StrictMode>,
)
