import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { CssBaseline, IconButton, ThemeProvider, Tooltip, type PaletteMode } from '@mui/material'
import DarkModeOutlined from '@mui/icons-material/DarkModeOutlined'
import LightModeOutlined from '@mui/icons-material/LightModeOutlined'
import { crearTema } from './tema'

const CLAVE = 'inventario.modo'

/** Lee la preferencia guardada; si no hay, usa la del sistema operativo. */
function modoInicial(): PaletteMode {
  try {
    const guardado = localStorage.getItem(CLAVE)
    if (guardado === 'light' || guardado === 'dark') return guardado
  } catch { /* navegador sin almacenamiento: se usa la preferencia del sistema */ }
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

const Ctx = createContext<{ modo: PaletteMode; alternar: () => void }>({ modo: 'light', alternar: () => {} })

export function ModoTemaProvider({ children }: { children: ReactNode }) {
  const [modo, setModo] = useState<PaletteMode>(modoInicial)
  const tema = useMemo(() => crearTema(modo), [modo])

  const alternar = useCallback(() => {
    setModo((m) => {
      const nuevo = m === 'light' ? 'dark' : 'light'
      try { localStorage.setItem(CLAVE, nuevo) } catch { /* sin almacenamiento: solo dura esta sesión */ }
      return nuevo
    })
  }, [])

  // Para que las barras de desplazamiento y los controles nativos del navegador sigan el modo.
  useEffect(() => { document.documentElement.style.colorScheme = modo }, [modo])

  return (
    <Ctx.Provider value={{ modo, alternar }}>
      <ThemeProvider theme={tema}>
        <CssBaseline enableColorScheme />
        {children}
      </ThemeProvider>
    </Ctx.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export const useModoTema = () => useContext(Ctx)

/** Botón de la barra superior para pasar de modo claro a oscuro y viceversa. */
export function BotonModo() {
  const { modo, alternar } = useModoTema()
  const oscuro = modo === 'dark'
  return (
    <Tooltip title={oscuro ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'}>
      <IconButton onClick={alternar} aria-label={oscuro ? 'Activar modo claro' : 'Activar modo oscuro'}>
        {oscuro ? <LightModeOutlined /> : <DarkModeOutlined />}
      </IconButton>
    </Tooltip>
  )
}
