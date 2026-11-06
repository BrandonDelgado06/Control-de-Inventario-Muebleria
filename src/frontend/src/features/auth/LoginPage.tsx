import { useState, type FormEvent } from 'react'
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material'
import { useAuth } from '../../app/AuthContext'
import { mensajeError } from '../../api/cliente'

export function LoginPage() {
  const { iniciar, expirada } = useAuth()
  const [usuario, setUsuario] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setEnviando(true)
    try {
      await iniciar(usuario, contrasena)
    } catch (err) {
      setError(mensajeError(err))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', p: 2 }}>
      <Paper component="form" onSubmit={enviar} sx={{ p: 4, width: '100%', maxWidth: 380 }}>
        <Stack spacing={2.5}>
          <Box>
            <Typography variant="h1">Inventario Mueblería</Typography>
            <Typography color="text.secondary">Ingresa con tu usuario del taller.</Typography>
          </Box>
          {expirada && !error && <Alert severity="info">Tu sesión se cerró por inactividad. Vuelve a ingresar.</Alert>}
          {error && <Alert severity="error">{error}</Alert>}
          <TextField id="usuario" label="Usuario" autoComplete="username" autoFocus value={usuario} onChange={(e) => setUsuario(e.target.value)} />
          <TextField id="contrasena" label="Contraseña" type="password" autoComplete="current-password" value={contrasena} onChange={(e) => setContrasena(e.target.value)} />
          <Button type="submit" variant="contained" size="large" disabled={enviando || !usuario || !contrasena}>
            {enviando ? 'Ingresando…' : 'Ingresar'}
          </Button>
        </Stack>
      </Paper>
    </Box>
  )
}
