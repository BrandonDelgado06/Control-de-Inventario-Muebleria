import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, EVENTO_SESION_EXPIRADA } from '../api/cliente'
import type { Sesion } from '../api/tipos'

interface AuthValor {
  sesion: Sesion | null
  cargando: boolean
  expirada: boolean
  iniciar: (usuario: string, contrasena: string) => Promise<void>
  cerrar: () => Promise<void>
  tiene: (permiso: string) => boolean
}

const AuthCtx = createContext<AuthValor | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<Sesion | null>(null)
  const [cargando, setCargando] = useState(true)
  const [expirada, setExpirada] = useState(false)
  const qc = useQueryClient()

  useEffect(() => {
    api.get<Sesion>('/auth/sesion')
      .then((r) => setSesion(r.data))
      .catch(() => setSesion(null))
      .finally(() => setCargando(false))
  }, [])

  useEffect(() => {
    const alExpirar = () => {
      setSesion(null)
      setExpirada(true)
      qc.clear()
    }
    window.addEventListener(EVENTO_SESION_EXPIRADA, alExpirar)
    return () => window.removeEventListener(EVENTO_SESION_EXPIRADA, alExpirar)
  }, [qc])

  const iniciar = useCallback(async (usuario: string, contrasena: string) => {
    const r = await api.post<Sesion>('/auth/login', { usuario, contrasena })
    setExpirada(false)
    setSesion(r.data)
  }, [])

  const cerrar = useCallback(async () => {
    await api.post('/auth/logout')
    qc.clear()
    setSesion(null)
  }, [qc])

  const valor = useMemo<AuthValor>(() => ({
    sesion, cargando, expirada, iniciar, cerrar,
    tiene: (p) => sesion?.permisos.includes(p) ?? false,
  }), [sesion, cargando, expirada, iniciar, cerrar])

  return <AuthCtx.Provider value={valor}>{children}</AuthCtx.Provider>
}

export function useAuth() {
  const v = useContext(AuthCtx)
  if (!v) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return v
}
