import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// En desarrollo, /api se redirige a la API de .NET (mismo origen → la cookie de sesión funciona).
// El build se copia a wwwroot de la API, que lo sirve en producción.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': 'http://localhost:5000' },
  },
  build: {
    outDir: '../backend/Muebleria.Api/wwwroot',
    emptyOutDir: true,
  },
})
