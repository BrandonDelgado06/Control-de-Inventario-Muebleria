# Frontend: Inventario Mueblería

React 19 + TypeScript + Vite + MUI. Cada módulo vive en `src/features/<modulo>`.

```powershell
npm install
npm run dev     # http://localhost:5173 (necesita la API corriendo en el puerto 5000)
npm run build   # compila hacia ../backend/Muebleria.Api/wwwroot
npm run lint
```

En desarrollo, Vite redirige `/api` a `http://localhost:5000`, así que la cookie de sesión funciona sin CORS.
