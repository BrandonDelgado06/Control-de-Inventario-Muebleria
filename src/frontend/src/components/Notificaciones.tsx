import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import { Alert, Snackbar } from '@mui/material'

type Tipo = 'success' | 'error' | 'info'
const Ctx = createContext<(mensaje: string, tipo?: Tipo) => void>(() => {})

export function NotificacionesProvider({ children }: { children: ReactNode }) {
  const [aviso, setAviso] = useState<{ mensaje: string; tipo: Tipo; clave: number } | null>(null)
  const notificar = useCallback((mensaje: string, tipo: Tipo = 'success') => {
    setAviso({ mensaje, tipo, clave: Date.now() })
  }, [])

  return (
    <Ctx.Provider value={notificar}>
      {children}
      <Snackbar
        key={aviso?.clave}
        open={!!aviso}
        autoHideDuration={aviso?.tipo === 'error' ? 7000 : 3500}
        onClose={() => setAviso(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity={aviso?.tipo ?? 'info'} variant="filled" onClose={() => setAviso(null)}>
          {aviso?.mensaje}
        </Alert>
      </Snackbar>
    </Ctx.Provider>
  )
}

export const useNotificar = () => useContext(Ctx)
