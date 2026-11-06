# Control de Inventario Mueblería

Sistema web local para controlar la materia prima, las herramientas manuales y sus accesorios, los productos de acabado y las herramientas de instalación de una mueblería.

Proyecto final de **ISB-32 Programación Avanzada**, Universidad Internacional San Isidro Labrador.

| | |
|---|---|
| Backend | ASP.NET Core 10 (C# 14) + EF Core |
| Frontend | React 19 + TypeScript + Vite + MUI |
| Base de datos | SQL Server 2025 (local) |
| Entrega | 4 de diciembre de 2026 |

## Documentación

- [Propuesta técnica](docs/propuesta-tecnica.md): arquitectura, estructura de carpetas, modelo de datos y permisos.
- [Roadmap](docs/roadmap.md): sprints, trazabilidad de RF y riesgos.
- [Guía de trabajo en GitHub](CONTRIBUTING.md): ramas, commits, PR y configuración del repositorio.
- Diagramas: [casos de uso](docs/diagramas/casos-de-uso.png) · [entidad-relación](docs/diagramas/entidad-relacion.png)

## Probar el sistema

```powershell
powershell -ExecutionPolicy Bypass -File scripts\iniciar.ps1
```

Abre dos ventanas, **Inventario - API** e **Inventario - Web**, y luego el navegador en http://localhost:5173. Para detener el sistema, cierra esas dos ventanas. Los usuarios de prueba son `admin`, `encargado` y `trabajador`; sus contraseñas están en `src/backend/Muebleria.Api/appsettings.Development.json`, en la sección `Seed`.

## Requisitos

| Herramienta | Versión |
|---|---|
| SDK de .NET | 10.0.100 o superior (fijado en `global.json`) |
| Node.js | 22 LTS o superior |
| SQL Server | 2025 Express o Developer |
| Visual Studio Code | Con las extensiones de `.vscode/extensions.json` |

## Primeros pasos

```powershell
git clone https://github.com/BrandonDelgado06/Control-de-Inventario-Muebleria.git
cd Control-de-Inventario-Muebleria
git checkout develop
```

Solo la primera vez (Sprint 0), una persona genera los proyectos y los sube:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\crear-proyectos.ps1
```

Para desarrollar:

```powershell
# Base de datos (una vez)
sqlcmd -S .\SQLEXPRESS -E -i database\scripts\01-usuario-app.sql
dotnet ef database update -p src\backend\Muebleria.Infrastructure -s src\backend\Muebleria.Api

# Backend
dotnet run --project src\backend\Muebleria.Api

# Frontend (otra terminal)
cd src\frontend
npm run dev
```

La cadena de conexión local se guarda con *user secrets*, nunca en el repositorio:

```powershell
dotnet user-secrets set "ConnectionStrings:Inventario" "Server=.\SQLEXPRESS;Database=MuebleriaInventario;Trusted_Connection=True;TrustServerCertificate=True" --project src\backend\Muebleria.Api
```

## Qué pasa al arrancar la API en desarrollo

- Aplica las migraciones pendientes y crea la base `MuebleriaInventario` si no existe.
- Crea los roles, los permisos, la configuración y el usuario `admin`.
- Con `Seed:DatosEjemplo = true` (solo en `appsettings.Development.json`) agrega los usuarios de prueba `encargado` y `trabajador`, 4 ubicaciones, 7 categorías con campos personalizados y 14 artículos del taller.
- Las contraseñas de prueba están en `src/backend/Muebleria.Api/appsettings.Development.json`, sección `Seed`. En producción no se crean datos de ejemplo y la contraseña del administrador se define en `appsettings.Production.json` del servidor.

Para empezar desde cero, borra la base de datos y vuelve a arrancar la API:

```powershell
sqlcmd -S . -E -C -Q "DROP DATABASE MuebleriaInventario"
```

Documentación de la API (solo en desarrollo): http://localhost:5000/scalar
